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
            // TODO: Figure this out.
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
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(sender.Id);
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
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(receiver.Id);
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
