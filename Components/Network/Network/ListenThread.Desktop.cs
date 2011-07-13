using Base;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Network
{
    public class ListenThread : Thread<ListenThread>
    {
        // Fields
        private bool                    m_waiting;
        private int                     m_port;
        private INetworkManager         m_networkManager;
        private TcpListener             m_listener;

        // Constructors
        public ListenThread (ILogger logger, INetworkManager networkManager, int port) 
            : base(logger)
        {
            m_port = port;
            m_networkManager = networkManager;

            try {
                m_listener = new TcpListener(IPAddress.Any, m_port);
                m_listener.Start();
            }
            catch (Exception ex) {
                // Being unable to listen is a fatal error.
                this.Fatal = true;

                FATAL("Failed to listen at {0}: {1}", m_listener.ToString(), ex.Message);
            }
        }

        // Public methods
        public override void Execute ()
        {
            // Listen until otherwise told to quit.
            if (!m_waiting) {
                m_listener.BeginAcceptSocket(new System.AsyncCallback(OnAcceptSocket), null);
                m_waiting = true;
            }

            Thread.Sleep(10);
        }

        // Private methods
        private void OnAcceptSocket (IAsyncResult result)
        {
            try {
                Socket socket = m_listener.EndAcceptSocket(result);

                TRACE("Accepted connection from {0}", socket.RemoteEndPoint.ToString());

                // Signal that we have a connection.
                m_networkManager.HandleConnect(socket);
            }
            catch (Exception ex) {
                WARN("Failed to accept socket: {0}", ex.Message);
            }

            // Start listening again.
            m_waiting = false;
        }
    }
}
