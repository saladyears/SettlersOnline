using Base;
using Database;
using Network;
using System.Collections.Generic;

namespace Login
{
    partial class Login : Log<Login>, IMessageReceiver
    {
        // Fields.
        private INetworkManager                 m_networkManager;
        private IDatabase                       m_database;
        private Dictionary<uint, LoginAttempt>  m_loginAttempts = new Dictionary<uint, LoginAttempt>();

        // Constructors.
        public Login (ILogger logger, INetworkManager networkManager, IDatabase database) 
            : base(logger)
        {
            m_networkManager = networkManager;
            m_database = database;
            
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
            else {
                WARN("{0} - Non-existent id", id);
            }
        }

        // Private methods.
        private void OnConnect (uint id)
        {
            TRACE("{0} - Connected", id);

            m_loginAttempts.Add(id, new LoginAttempt(this.Logger, m_networkManager, m_database, id));
        }

        private void OnDisconnect (uint id)
        {
            TRACE("{0} - Disconnected", id);

            m_loginAttempts.Remove(id);
        }
    }
}
