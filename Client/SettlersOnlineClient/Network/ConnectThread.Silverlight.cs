using System.Net.Sockets;

namespace Network
{
    public class ConnectThread
    {
        // Fields.
        INetworkManager m_networkManager;

        // Constructors.
        public ConnectThread (INetworkManager networkManager)
        {
            m_networkManager = networkManager;
        }
    }
}
