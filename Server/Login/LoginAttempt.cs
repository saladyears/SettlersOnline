using Base;
using Network;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;

namespace Login
{
    partial class Login
    {
        private class LoginAttempt : Log<LoginAttempt>
        {
            // Fields.
            private uint m_id;
            private INetworkManager m_networkManager;
            private AesManaged m_aes;

            // Constructors.
            public LoginAttempt (uint id, INetworkManager manager, ILogger logger)
                : base(logger)
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
                        ERROR("{0} - Invalid login state", m_id);

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
                    m_aes = new AesManaged();
                    m_aes.KeySize = 256;
                    m_aes.GenerateIV();
                    m_aes.GenerateKey();
                    m_networkManager.SetCryptoProvider(m_id, new AesProvider(m_aes));

                    TRACE("{0} - IV = {1}", m_id, BitConverter.ToString(m_aes.IV));
                    TRACE("{0} - Key = {1}", m_id, BitConverter.ToString(m_aes.Key));

                    // Create one outgoing buffer with both bits of data in it.
                    IEnumerable<byte> data = m_aes.Key.Concat(m_aes.IV);

                    loginMessage.Name = "";
                    loginMessage.Data = data.ToArray();

                    // Use an RSA provider as the override for this message.
                    m_networkManager.SendMessage(m_id, loginMessage, new RSACryptoProvider(clientProvider));
                }
                catch (Exception ex) {
                    ERROR("{0} - Key generation failure ({1})", m_id, ex.Message);

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
                    INFO("{0} - Non-existent user {1}", m_id, name);
                    m_networkManager.HandleDisconnect(m_id);
                }
                else {
                    bool validated = ValidateCredentials(password, user);

                    TRACE("{0} - Validated ({1})", m_id, validated);

                    // TODO: Deal with validation.
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
                for (; ; ) {
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
