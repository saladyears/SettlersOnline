using Network;
using NHibernate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Login
{
    class Login : IMessageReceiver
    {
        // Fields.
        private INetworkManager                 m_networkManager;
        private Dictionary<uint, LoginAttempt>  m_loginAttempts = new Dictionary<uint, LoginAttempt>();

        // TODO: Fix these hacks.
        public static ISessionFactory                 m_sessionFactory;
        public static ISession                        m_session;

        // Constructors.
        public Login (INetworkManager networkManager)
        {
            m_networkManager = networkManager;

            m_networkManager.OnConnect += new Connect(OnConnect);
            m_networkManager.OnDisconnect += new Disconnect(OnDisconnect);
            m_networkManager.AddReceiver(MessageType.Login, this);

            // Initialize NHibernate.
            //<property name="connection.connection_string">Database=settlersonline;Data Source=192.168.0.4,3306;User Id=settlersonline;Password=hecsEnk3</property>

            m_sessionFactory = new NHibernate.Cfg.Configuration().Configure().SetProperty("connection.connection_string", "Database=settlersonline;Data Source=192.168.0.4,3306;User Id=settlersonline;Password=hecsEnk3").BuildSessionFactory();
            m_session = m_sessionFactory.OpenSession();
        }

        // Public methods.
        public void ReceiveMessage (uint id, IMessage message)
        {
            LoginAttempt attempt = null;
            bool success = m_loginAttempts.TryGetValue(id, out attempt);
            if (success) {
                attempt.ReceiveMessage(message);
            }
        }

        // Private methods.
        private void OnConnect (uint id)
        {
            m_loginAttempts.Add(id, new LoginAttempt(id, m_networkManager));
        }

        private void OnDisconnect (uint id)
        {
            m_loginAttempts.Remove(id);
        }

        private class LoginAttempt
        {
            // Fields.
            private INetworkManager m_networkManager;
            private uint m_id;

            // Constructors.
            public LoginAttempt (uint id, INetworkManager manager)
            {
                this.State = Stage.ReceiveClientKey;

                m_id = id;
                m_networkManager = manager;
            }

            // Properties.
            private Stage State { get; set; }
            
            // Public methods.
            public void ReceiveMessage (IMessage message)
            {
                ContinueLogin(message);
            }

            // Private methods.
            private void ContinueLogin (IMessage message)
            {
                LoginMessage loginMessage = message as LoginMessage;

                switch (this.State) {
                    case Stage.ReceiveClientKey:
                        ReceiveClientKey(loginMessage);
                        break;
                    case Stage.ReceiveCredentials:
                        ReceiveCredentials(loginMessage);
                        break;
                    default:
                        // Something went wrong, abort.
                        m_networkManager.HandleDisconnect(m_id);
                        break;
                }
            }

            private void ReceiveClientKey (LoginMessage loginMessage)
            {
                try {
                    this.State = Stage.ReceiveCredentials;

                    // Create a service provider from the client's public key.
                    RSACryptoServiceProvider clientProvider = new RSACryptoServiceProvider();
                    clientProvider.FromXmlString(loginMessage.Name);

                    // Generate an IV (salt) and key to use with this client,
                    // along with an aes to provide the 2-way encryption.
                    AesManaged aes = new AesManaged();
                    aes.KeySize = 256;
                    aes.GenerateIV();
                    aes.GenerateKey();
                    m_networkManager.SetCryptoProvider(m_id, new AesProvider(aes));

                    // Create one outgoing buffer with both bits of data in it.
                    byte[] data = new byte[aes.Key.Length + aes.IV.Length];
                    Array.Copy(aes.Key, data, aes.Key.Length);
                    Array.Copy(aes.IV, 0, data, aes.Key.Length, aes.IV.Length);

                    loginMessage.Name = "";
                    loginMessage.Data = data;
                    
                    // Use an RSA provider as the override for this message.
                    m_networkManager.SendMessage(m_id, loginMessage, new RSACryptoProvider(clientProvider));
                }
                catch (Exception) {
                    // TODO: Log failure.
                    m_networkManager.HandleDisconnect(m_id);
                }
            }

            private void ReceiveCredentials (LoginMessage loginMessage)
            {
                string name = loginMessage.Name;
                string password = Encoding.UTF8.GetString(loginMessage.Data);

                User user = null;

                // Look up the user in the database.
                try {
                    user = Login.m_session.Get<User>(name);
                }
                catch (Exception) {
                    // TODO: Log failure.
                }

                if (null == user) {
                    // TODO: Log non-existent user.
                    m_networkManager.HandleDisconnect(m_id);
                }
                else {
                    bool validated = ValidateCredentials(password, user);
                }
            }

            private bool ValidateCredentials (string password, User user)
            {
                bool valid = false;

                switch (user.HashMethod) {
                    case "Vanilla":
                        valid = ValidateVanilla(password, user);
                        break;
                    default:
                        break;
                }
                
                return valid;
            }

            private bool ValidateVanilla (string password, User user)
            {
                bool valid = false;

                // Convert the user password into a usable string.
                string compare = Encoding.ASCII.GetString(user.Password);
                string itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

                // The Vanilla PHP tests against all kinds of possible password
                // values.  We will only use the $P version as that it how the
                // hashes are created from a fresh Vanilla installation.
                for (;;) {
                    if ("$P$" != compare.Substring(0, 3)) {
                        break;
                    }

                    int countLog2 = itoa64.IndexOf(compare[3]);
                    if ((7 > countLog2) || (30 < countLog2)) {
                        break;
                    }

                    int count = 1 << countLog2;
                    byte[] salt = Encoding.ASCII.GetBytes(compare.Substring(4, 8));
                    byte[] pass = Encoding.ASCII.GetBytes(password);
                    byte[] hash = null;

                    IEnumerable<byte> data = salt.Concat(pass);
                    
                    using (MD5 md5 = MD5.Create()) {
                        hash = md5.ComputeHash(data.ToArray());
                        do {
                            data = hash.Concat(pass);
                            hash = md5.ComputeHash(data.ToArray());
                        }
                        while (0 != (--count));
                    }

                    // Convert the final hash to its ASCII string.
                    StringBuilder builder = new StringBuilder(compare.Substring(0, 12));
                    int i = 0;
                    do {
                        int value = hash[i++];
                        builder.Append(itoa64[value & 0x3f]);

                        if (i < hash.Length) {
                            value |= (hash[i] << 8);
                        }

                        builder.Append(itoa64[(value >> 6) & 0x3f]);

                        if (++i > hash.Length) {
                            break;
                        }

                        if (i < hash.Length) {
                            value |= (hash[i] << 16);
                        }

                        builder.Append(itoa64[(value >> 12) & 0x3f]);

                        if (++i > hash.Length) {
                            break;
                        }

                        builder.Append(itoa64[(value >> 18) & 0x3f]);
                    }
                    while (i < hash.Length);

                    string final = builder.ToString();
                    valid = (final == compare);

                    break;
                }
                
                return valid;
            }

            // Private types.
            private enum Stage
            {
                ReceiveClientKey,
                ReceiveCredentials,
            }        
        }
    }
}
