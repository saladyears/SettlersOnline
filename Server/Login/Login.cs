using Network;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Login
{
    class Login : IMessageReceiver
    {
        // Fields.
        private INetworkManager m_networkManager;
        private Dictionary<uint, LoginAttempt> m_loginAttempts = new Dictionary<uint, LoginAttempt>();

        // Constructors.
        public Login (INetworkManager networkManager)
        {
            m_networkManager = networkManager;

            m_networkManager.OnConnect += new Connect(OnConnect);
            m_networkManager.OnDisconnect += new Disconnect(OnDisconnect);
            m_networkManager.AddReceiver(MessageType.Login, this);
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

                // Login steps:
                //
                // 1.  Receive the client's public RSA key.
                // 2.  Send the server's public RSA key to the client.
                // 3.  Receive the client's encrypted login name and password.
                //     Note that the password is already hashed pre-encryption.
                // 4.  Decrypt the client's login name and password.
                // 5.  Verify name and password against the database.
                // 6.  If the password is not verified, notify the client and close
                //     the connection.
                // 7.  If the password is verified, notify the client and wait
                //     for the client to send its public key.
                // 8.  When the client's public key is received, generate a salt
                //     value for the client's communication with lobby or game
                //     servers.
                // 9.  Encrypt the salt value with the client's public key and send
                //     it to the client.
                // 10.  Check the database to see if the client is in any ongoing
                //     games.
                // 11. If the client is in an ongoing game, send it the port for
                //     the game server.  Notify the game server of the pending
                //     connection.
                // 12. If the client is not in an ongoing game, send it the port 
                //     for the lobby server.  Notify the lobby server of the 
                //     pending connection.
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

                // TODO: Validate against the database.
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
