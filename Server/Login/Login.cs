using Base;
using Network;
using System.Collections.Generic;

// FIXME: Move to Database thread.
using NHibernate;

namespace Login
{
    partial class Login : Log<Login>, IMessageReceiver
    {
        // Fields.
        private INetworkManager                 m_networkManager;
        private Dictionary<uint, LoginAttempt>  m_loginAttempts = new Dictionary<uint, LoginAttempt>();

        // FIXME: Move to Database thread.
        public static ISessionFactory                 m_sessionFactory;
        public static ISession                        m_session;

        // Constructors.
        public Login (ILogger logger, INetworkManager networkManager) : base(logger)
        {
            m_networkManager = networkManager;
            
            m_networkManager.OnConnect += new Connect(OnConnect);
            m_networkManager.OnDisconnect += new Disconnect(OnDisconnect);
            m_networkManager.AddReceiver(MessageType.Login, this);

            // FIXME: Move to Database thread.

            // Initialize NHibernate.
            m_sessionFactory = new NHibernate.Cfg.Configuration().Configure().BuildSessionFactory();
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
            else {
                WARN("{0} - Non-existent id", id);
            }
        }

        // Private methods.
        private void OnConnect (uint id)
        {
            TRACE("{0} - Connected", id);

            m_loginAttempts.Add(id, new LoginAttempt(id, m_networkManager, this.Logger));
        }

        private void OnDisconnect (uint id)
        {
            TRACE("{0} - Disconnected", id);

            m_loginAttempts.Remove(id);
        }
    }
}
