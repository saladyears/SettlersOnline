using Base;
using System;
using System.IO;
using System.Net.Sockets;

namespace Network
{
    public class ClientThread : MessageThread<ClientThread>
    {
        // Constructors.
        public ClientThread (ILogger logger)
            : base(logger)
        {
        }

        // Protected methods.
        protected override bool IsDisconnected (Socket socket)
        {
            // NOTE: It does not seem to be possible to detect a remote
            // disconnection in Silverlight.  This will probably have to be
            // detected by the Send/Receive functions.
            return false;
        }

        protected override void Send (Sender sender, int offset, int size)
        {
            Socket socket = sender.Socket;
            SocketAsyncEventArgs ea = sender.EventArgs;

            ea.Completed += OnSendCompleted;
            ea.UserToken = sender;

            // See if we're sending the size of the message or the message
            // itself.
            try {
                if (null != sender.Size) {
                    ea.SetBuffer(sender.Size, offset, size);
                }
                else {
                    ea.SetBuffer(sender.Stream.GetBuffer(), offset, size);
                }

                bool async = socket.SendAsync(ea);
                if (!async) {
                    CompleteSend(ea);
                }
            }
            catch (Exception ex) {
                INFO("SendAsync failed: {0}", ex.Message);
                HandleDisconnect(sender.Id, "send failed");
            }
        }

        protected override void Receive (Receiver receiver, int offset, int size)
        {
            Socket socket = receiver.Socket;
            SocketAsyncEventArgs ea = receiver.EventArgs;

            ea.Completed += OnReceiveCompleted;
            ea.UserToken = receiver;
            ea.SetBuffer(receiver.Stream.GetBuffer(), offset, size);

            try {
                bool async = socket.ReceiveAsync(ea);
                if (!async) {
                    CompleteReceive(ea);
                }
            }
            catch (Exception ex) {
                INFO("ReceiveAsync failed: {0}", ex.Message);
                HandleDisconnect(receiver.Id, "receive failed");
            }
        }

        // Private methods.
        private void OnSendCompleted (object sender, SocketAsyncEventArgs ea)
        {
            ea.Completed -= OnSendCompleted;
            CompleteSend(ea);
        }

        void OnReceiveCompleted (object sender, SocketAsyncEventArgs ea)
        {
            ea.Completed -= OnReceiveCompleted;
            CompleteReceive(ea);
        }

        private void CompleteSend (SocketAsyncEventArgs ea)
        {
            Sender sender = (Sender) ea.UserToken;
            HandleSend(sender, ea.BytesTransferred);
        }

        private void CompleteReceive (SocketAsyncEventArgs ea)
        {
            Receiver receiver = (Receiver) ea.UserToken;
            HandleReceive(receiver, ea.BytesTransferred);
        }
    }
}
