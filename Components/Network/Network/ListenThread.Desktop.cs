using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Network
{
    public class ListenThread
    {
        // Fields
        private volatile bool           m_stop;
        private bool                    m_waiting;
        private int                     m_port;
        private INetworkManager         m_socketHandler;
        private TcpListener             m_listener;

        // Constructors
        public ListenThread (INetworkManager socketHandler, int port)
        {
            m_port = port;
            m_socketHandler = socketHandler;
        }

        // Public methods
        public void Stop ()
        {
            m_stop = true;
        }

        public void Start ()
        {
            while (!m_stop) {
                try {
                    m_listener = new TcpListener(IPAddress.Any, m_port);
                    m_listener.Start();

                    // Listen until otherwise told to quit.
                    while (!m_stop) {
                        if (!m_waiting) {
                            m_listener.BeginAcceptSocket(new System.AsyncCallback(OnAcceptSocket), null);
                            m_waiting = true;
                        }

                        Thread.Sleep(10);
                    }
                }
                catch (Exception) {
                    // TODO: Log exception.
                }

                m_listener.Stop();
            }
        }

        // Private methods
        private void OnAcceptSocket (IAsyncResult result)
        {
            try {
                Socket socket = m_listener.EndAcceptSocket(result);

                // Signal that we have a connection.
                m_socketHandler.HandleConnect(socket);
            }
            catch (Exception) {
                // TODO: Log exception.
            }

            // Start listening again.
            m_waiting = false;
        }
    }
}
