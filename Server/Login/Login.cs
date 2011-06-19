using Network;
using System.Collections.Generic;

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

            // Constructors.
            public LoginAttempt (uint id, INetworkManager manager)
            {
                this.Id = id;
                this.State = Stage.SendPublicKey;

                m_networkManager = manager;

                ContinueLogin(null);
            }

            // Properties.
            private uint Id { get; set; }
            private Stage State { get; set; }

            // Public methods.
            public void ReceiveMessage (IMessage message)
            {
                ContinueLogin(message);
            }

            // Private methods.
            private void ContinueLogin (IMessage message)
            {
                // Login steps:
                //
                // 1.  Send the server's public RSA key to the client.
                // 2.  Receive the client's encrypted login name and password.
                //     Note that the password is already hashed pre-encryption.
                // 3.  Decrypt the client's login name and password.
                // 4.  Verify name and password against the database.
                // 5.  If the password is not verified, notify the client and close
                //     the connection.
                // 6.  If the password is verified, notify the client and wait
                //     for the client to send its public key.
                // 7.  When the client's public key is received, generate a salt
                //     value for the client's communication with lobby or game
                //     servers.
                // 8.  Encrypt the salt value with the client's public key and send
                //     it to the client.
                // 9.  Check the database to see if the client is in any ongoing
                //     games.
                // 10. If the client is in an ongoing game, send it the port for
                //     the game server.  Notify the game server of the pending
                //     connection.
                // 11. If the client is not in an ongoing game, send it the port 
                //     for the lobby server.  Notify the lobby server of the 
                //     pending connection.
                switch (this.State) {
                    case Stage.SendPublicKey:
                        break;
                    case Stage.ReceiveCredentials:
                        break;
                    default:
                        // Something went wrong, abort.
                        m_networkManager.HandleDisconnect(this.Id);
                        break;
                }
            }

            private void SendPublicKey (uint id)
            {
                // TODO: Get public key.
                byte[] key = new byte[1024];
                LoginMessage message = new LoginMessage("", key);

                m_networkManager.SendMessage(id, message);
            }

            // Private types.
            private enum Stage
            {
                SendPublicKey,
                ReceiveCredentials,
            }        
        }
    }
}
