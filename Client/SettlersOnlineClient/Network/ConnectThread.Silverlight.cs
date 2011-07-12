using Base;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Network
{
    public class ConnectThread : Thread<ConnectThread>
    {
        // Fields.
        private INetworkManager     m_networkManager;
        private DnsEndPoint         m_endPoint;
        private volatile bool       m_attemptConnect;

        // Constructors.
        public ConnectThread (ILogger logger, INetworkManager networkManager, DnsEndPoint endPoint) 
            : base(logger)
        {
            m_networkManager = networkManager;
            m_endPoint = endPoint;
            m_attemptConnect = true;
        }
        
        // Public methods.
        public override void Execute ()
        {
            if (m_attemptConnect) {
                // TODO: Notify connect window.

                SocketAsyncEventArgs ea = new SocketAsyncEventArgs();
                ea.Completed += OnCompleted;
                ea.RemoteEndPoint = m_endPoint;

                // Begin an async connect to our given DnsEndPoint.
                try {
                    TRACE("Attempting to connect to {0}", m_endPoint.ToString());

                    bool async = Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, ea);

                    // If it immediately connected, proceed.
                    if (!async) {
                        HandleCompletion(ea);
                    }
                }
                catch (Exception) {
                    // TODO: Notify event handler.
                }

                m_attemptConnect = false;
            }

            Thread.Sleep(100);
        }

        // Private methods.
        private void OnCompleted (object sender, SocketAsyncEventArgs ea)
        {
            HandleCompletion(ea);   
        }

        private void HandleCompletion (SocketAsyncEventArgs ea)
        {
            if (ea.SocketError == SocketError.Success) {
                ea.Completed -= OnCompleted;
                Socket socket = ea.ConnectSocket;

                m_networkManager.HandleConnect(socket);

                // TODO: Notify event handler.
            }
            else {
                // TODO: Notify event handler.
                m_attemptConnect = true;
            }
        }
    }
}
