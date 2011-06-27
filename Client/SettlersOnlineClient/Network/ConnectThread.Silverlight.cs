using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Network
{
    public class ConnectThread
    {
        // Fields.
        private INetworkManager     m_networkManager;
        private DnsEndPoint         m_endPoint;
        private volatile bool       m_stop;
        private volatile bool       m_connect;

        // Constructors.
        public ConnectThread (INetworkManager networkManager, DnsEndPoint endPoint)
        {
            m_networkManager = networkManager;
            m_endPoint = endPoint;
            m_connect = true;
        }
        
        // Public methods.
        public void Start ()
        {
            while (!m_stop) {
                if (m_connect) {
                    // TODO: Notify event handler.

                    SocketAsyncEventArgs ea = new SocketAsyncEventArgs();
                    ea.Completed += OnCompleted;
                    ea.RemoteEndPoint = m_endPoint;

                    // Begin an async connect to our given DnsEndPoint.
                    try {
                        bool async = Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, ea);

                        // If it immediately connected, proceed.
                        if (!async) {
                            HandleCompletion(ea);
                        }
                    }
                    catch (Exception) {
                        // TODO: Notify event handler.
                    }

                    m_connect = false;
                }

                Thread.Sleep(100);
            }
        }

        public void Stop ()
        {
            m_stop = true;
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
                m_connect = true;
            }
        }
    }
}
