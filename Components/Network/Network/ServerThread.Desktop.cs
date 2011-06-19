using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Network
{
    public class ServerThread : MessageThread
    {
        // Fields.
        private byte[] m_connected = new byte[1];
        
        // Protected methods.
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
                socket.BeginSend(stream.GetBuffer(), offset, size, SocketFlags.None, new AsyncCallback(OnSend), sender);
            }
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(sender.Id);
            }
        }

        protected override void Receive (Receiver receiver, int offset, int size)
        {
            Socket socket = receiver.Socket;
            MemoryStream stream = receiver.Stream;

            try {
                socket.BeginReceive(stream.GetBuffer(), offset, size, SocketFlags.None, new AsyncCallback(OnReceive), receiver);
            }
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(receiver.Id);
            }
        }

        // Private methods.
        private void OnSend (IAsyncResult ar)
        {
            Sender sender = (Sender) ar.AsyncState;

            try {
                int bytesSent = sender.Socket.EndSend(ar);
                sender.Sent += bytesSent;

                // If we haven't sent the whole buffer, try it again.
                if (sender.Sent < sender.Stream.Length) {
                    Send(sender, sender.Sent, (int) sender.Stream.Length - sender.Sent);
                }
            }
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(sender.Id);
            }
        }

        private void OnReceive (IAsyncResult ar)
        {
            Receiver receiver = (Receiver) ar.AsyncState;

            try {
                int bytesReceived = receiver.Socket.EndReceive(ar);

                // Check for 0 bytes received (the remote socket closed).
                if (0 == bytesReceived) {
                    throw new Exception();
                }

                receiver.Received += bytesReceived;

                int bytesRequired = (0 == receiver.Required) ? sizeof(int) : receiver.Required;
                int offset = 0;
                int size = sizeof(int);

                // If we didn't get what we need this trip, start it up again.
                if (receiver.Received < bytesRequired) {
                    offset = receiver.Received;
                    size = bytesRequired - receiver.Received;
                }
                // If we have a full buffer, we either have the size of the 
                // message, in which case we start receiving again, or we have the
                // full message, in which case we kick it off to the factory.
                else {
                    MemoryStream stream = receiver.Stream;
                    stream.Position = 0;

                    if (0 == receiver.Required) {
                        size = BitConverter.ToInt32(stream.GetBuffer(), 0);

                        // Make sure the stream buffer is large enough to hold the
                        // incoming message.
                        if (size > stream.Capacity) {
                            stream.Capacity = size;
                        }

                        receiver.Required = size;
                    }
                    else {
                        // TODO: Decrypt the message.

                        // Get the message type.
                        MessageType type = (MessageType) BitConverter.ToInt32(stream.GetBuffer(), 0);
                        stream.Position += sizeof(int);

                        // Create the message.
                        IMessage message = MessageFactory.CreateMessage(type, stream);

                        if (null != message) {
                            lock (m_receiverLock) {
                                HashSet<IMessageReceiver> receivers = null;
                                bool success = m_receivers.TryGetValue(type, out receivers);
                                if (success) {
                                    foreach (IMessageReceiver messageReceiver in receivers) {
                                        messageReceiver.ReceiveMessage(receiver.Id, message);
                                    }
                                }
                            }
                        }
                    }
                }

                // Start looking for the next chunk of message.
                Receive(receiver, offset, size);
            }
            catch (Exception) {
                // TODO: Log disconnect.
                HandleDisconnect(receiver.Id);
            }
        }
    }
}
