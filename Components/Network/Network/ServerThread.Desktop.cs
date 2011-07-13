using Base;
using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Network
{
    public class ServerThread : MessageThread<ServerThread>
    {
        #region Fields
        private byte[] m_connected = new byte[1];
        #endregion

        #region Constructors
        public ServerThread (ILogger logger)
            : base(logger)
        {
        }
        #endregion


        #region Protected methods
        protected override bool IsDisconnected (Socket socket)
        {
            bool disconnected = false;

            // According to the MSDN documentation, the way to check the
            // connection status of a socket is to attempt a zero-byte 
            // non-blocking send on it.
            bool blocking = socket.Blocking;

            try {
                socket.Blocking = false;
                socket.Send(m_connected, 0, SocketFlags.None);
            }
            catch (SocketException se) {
                // If it is not a WouldBlock, then we're not connected.
                if (se.SocketErrorCode != SocketError.WouldBlock) {
                    TRACE("Detected a disconnect");
                    disconnected = true;
                }
            }
            finally {
                socket.Blocking = blocking;
            }

            return disconnected;
        }

        protected override void Send (Sender sender, int offset, int size)
        {
            Socket socket = sender.Socket;
            MemoryStream stream = sender.Stream;

            try {
                // See if we're sending the size of the message, or the message
                // itself.
                if (null != sender.Size) {
                    socket.BeginSend(sender.Size, offset, size, SocketFlags.None, new AsyncCallback(OnSend), sender);
                }
                else {
                    socket.BeginSend(stream.GetBuffer(), offset, size, SocketFlags.None, new AsyncCallback(OnSend), sender);
                }
            }
            catch (Exception ex) {
                INFO("BeginSend failed: {0}", ex.Message);

                HandleDisconnect(sender.Id, "send failed");
            }
        }

        protected override void Receive (Receiver receiver, int offset, int size)
        {
            Socket socket = receiver.Socket;
            MemoryStream stream = receiver.Stream;

            try {
                socket.BeginReceive(stream.GetBuffer(), offset, size, SocketFlags.None, new AsyncCallback(OnReceive), receiver);
            }
            catch (Exception ex) {
                INFO("BeginReceive failed: {0}", ex.Message);

                HandleDisconnect(receiver.Id, "receive failed");
            }
        }
        #endregion

        #region Private methods
        private void OnSend (IAsyncResult ar)
        {
            Sender sender = (Sender) ar.AsyncState;
            int bytesSent = 0;
            bool success = true;

            try {
                bytesSent = sender.Socket.EndSend(ar);
            }
            catch (Exception ex) {
                INFO("EndSend failed: {0}", ex.Message);

                HandleDisconnect(sender.Id, "send failed");
                success = false;
            }

            if (success) {
                HandleSend(sender, bytesSent);
            }
        }

        private void OnReceive (IAsyncResult ar)
        {
            Receiver receiver = (Receiver) ar.AsyncState;
            int bytesReceived = 0;
            bool success = true;

            try {
                bytesReceived = receiver.Socket.EndReceive(ar);
            }
            catch (Exception ex) {
                INFO("EndReceive failed: {0}", ex.Message);

                HandleDisconnect(receiver.Id, "receive failed");
                success = false;
            }

            if (success) {
                HandleReceive(receiver, bytesReceived);
            }
        }
        #endregion
    }
}
