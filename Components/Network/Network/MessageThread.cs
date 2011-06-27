using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Network
{
    public abstract class MessageThread : INetworkManager
    {
        #region Constants
        private const int MAX_MESSAGE_SIZE = 4096;
        #endregion

        #region Fields
        private volatile bool   m_stop;
        
        // These fields require thread-safety.
        protected Dictionary<MessageType, HashSet<IMessageReceiver>> m_receivers = new Dictionary<MessageType, HashSet<IMessageReceiver>>();
        private Queue<Crypto> m_cryptoQueue = new Queue<Crypto>();
        private Queue<Socket> m_connectQueue = new Queue<Socket>();
        private Queue<Socket> m_disconnectQueue = new Queue<Socket>();
        private Queue<Queuer> m_sendQueue = new Queue<Queuer>();
        protected Object m_receiverLock = new Object();
        private Object m_cryptoLock = new Object();
        private Object m_connectLock = new Object();
        private Object m_disconnectLock = new Object();
        private Object m_sendLock = new Object();

        // These fields are only modified by this thread.
        private Dictionary<uint, Socket> m_idToSocket = new Dictionary<uint, Socket>();
        private Dictionary<Socket, Sender> m_socketSend = new Dictionary<Socket, Sender>();
        private Dictionary<Socket, Receiver> m_socketReceive = new Dictionary<Socket, Receiver>();
        private uint m_nextId;
        #endregion

        #region Events
        public event Connect OnConnect;
        public event Disconnect OnDisconnect;
        #endregion

        #region Abstract methods
        protected abstract bool IsDisconnected (Socket socket);
        protected abstract void Send (Sender sender, int offset, int size);
        protected abstract void Receive (Receiver receiver, int offset, int size);
        #endregion

        #region Public methods
        public void Stop ()
        {
            m_stop = true;
        }

        public void Start ()
        {
            while (!m_stop) {
                HandleConnects();
                HandleCryptos();
                HandleDisconnects();
                SendQueuedMessages();
                CheckForDisconnects();

                Thread.Sleep(10);
            }
        }

        public void HandleConnect (Socket socket)
        {
            lock (m_connectLock) {
                m_connectQueue.Enqueue(socket);
            }
        }

        public void HandleDisconnect (uint id)
        {
            Socket socket = null;
            bool success = m_idToSocket.TryGetValue(id, out socket);
            if (success) {
                lock (m_disconnectLock) {
                    m_disconnectQueue.Enqueue(socket);
                }
            }
        }

        public void AddReceiver (MessageType type, IMessageReceiver receiver)
        {
            HashSet<IMessageReceiver> receivers = null;

            lock (m_receiverLock) {
                bool success = m_receivers.TryGetValue(type, out receivers);
                if (!success) {
                    receivers = new HashSet<IMessageReceiver>();
                    m_receivers.Add(type, receivers);
                }

                receivers.Add(receiver);
            }
        }

        public void RemoveReceiver (MessageType type, IMessageReceiver receiver)
        {
            HashSet<IMessageReceiver> receivers = null;

            lock (m_receiverLock) {
                bool success = m_receivers.TryGetValue(type, out receivers);
                if (success) {
                    receivers.Remove(receiver);
                }
            }
        }

        public void SendMessage (uint id, IMessage message, ICryptoProvider cryptoOverride)
        {
            lock (m_sendLock) {
                m_sendQueue.Enqueue(new Queuer(id, message, cryptoOverride));
            }
        }

        public void SetCryptoProvider (uint id, ICryptoProvider cryptoProvider)
        {
            lock (m_cryptoLock) {
                m_cryptoQueue.Enqueue(new Crypto(id, cryptoProvider));
            }
        }
        #endregion

        #region Protected methods
        protected void HandleSend (Sender sender, int bytesSent)
        {
            sender.Sent += bytesSent;

            int offset = sender.Sent;
            int size = 0;

            // See if we're done sending the size of the message.
            if (null != sender.Size) {
                // If we are, send the actual message.
                if (sender.Sent >= sizeof(int)) {
                    sender.Size = null;
                    offset = 0;
                    size = (int) sender.Stream.Length;
                }
                // Otherwise, keep sending the size buffer.
                else {
                    size = sizeof(int) - sender.Sent;
                }
            }
            else if (sender.Sent < sender.Stream.Length) {
                size = (int) sender.Stream.Length - sender.Sent;
            }

            // If we haven't sent the whole buffer, try it again.
            if (0 < size) {
                Send(sender, offset, size);
            }
        }

        protected void HandleReceive (Receiver receiver, int bytesReceived)
        {
            // Check for 0 bytes received (the remote socket closed).
            if (0 == bytesReceived) {
                HandleDisconnect(receiver.Id);
                return;
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

                    // TODO: Make sure the stream buffer is not over our max.

                    receiver.Required = size;
                }
                else {
                    // Decrypt the message.
                    if (null != receiver.CryptoProvider) {
                        byte[] decryptBuffer = new byte[bytesRequired];
                        Array.Copy(stream.GetBuffer(), decryptBuffer, bytesRequired);
                        decryptBuffer = receiver.CryptoProvider.Decrypt(decryptBuffer);
                        decryptBuffer.CopyTo(stream.GetBuffer(), 0);
                    }

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

                    receiver.Required = 0;
                }

                receiver.Received = 0;
            }

            // Start looking for the next chunk of message.
            Receive(receiver, offset, size);
        }
        #endregion

        #region Private methods
        private void HandleConnects ()
        {
            lock (m_connectLock) {
                for (int i = 0; i < m_connectQueue.Count; ++i) {
                    Socket socket = m_connectQueue.Dequeue();

                    uint id = m_nextId++;

                    m_idToSocket.Add(id, socket);
                    m_socketSend.Add(socket, new Sender(id, socket));

                    Receiver receiver = new Receiver(id, socket);
                    m_socketReceive.Add(socket, receiver);

                    if (null != OnConnect) {
                        OnConnect(id);
                    }

                    // Immediately start listening on the socket.
                    Receive(receiver, 0, sizeof(int));
                }
            }
        }

        private void HandleCryptos ()
        {
            lock (m_cryptoLock) {
                for (int i = 0; i < m_cryptoQueue.Count; ++i) {
                    Crypto crypto = m_cryptoQueue.Dequeue();

                    Socket socket = null;
                    bool success = m_idToSocket.TryGetValue(crypto.Id, out socket);
                    if (success) {
                        Sender sender = m_socketSend[socket];
                        Receiver receiver = m_socketReceive[socket];

                        sender.CryptoProvider = crypto.CryptoProvider;
                        receiver.CryptoProvider = crypto.CryptoProvider;
                    }
                }
            }
        }

        private void HandleDisconnects ()
        {
            lock (m_disconnectLock) {
                for (int i = 0; i < m_disconnectQueue.Count; ++i) {
                    Socket socket = m_disconnectQueue.Dequeue();
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();

                    // NOTE: We need to ensure that sockets are never removed 
                    // more than once.
                    Sender sender = m_socketSend[socket];
                    uint id = sender.Id;
                    m_idToSocket.Remove(id);
                    m_socketSend.Remove(socket);
                    m_socketReceive.Remove(socket);

                    if (null != OnDisconnect) {
                        OnDisconnect(id);
                    }
                }
            }
        }

        private void SendQueuedMessages ()
        {
            lock (m_sendLock) {
                for (int i = 0; i < m_sendQueue.Count; ++i) {
                    Queuer queueMessage = m_sendQueue.Dequeue();
                    Sender sender = ConvertQueueToSend(queueMessage);

                    // If something went wrong, abort.
                    if (null == sender) {
                        HandleDisconnect(queueMessage.Id);
                    }
                    else {
                        Send(sender, 0, sizeof(int));
                    }
                }
            }
        }

        private void CheckForDisconnects ()
        {
            foreach (KeyValuePair<uint, Socket> kvp in m_idToSocket) {
                bool disconnected = IsDisconnected(kvp.Value);
                if (disconnected) {
                    HandleDisconnect(kvp.Key);
                }
            }
        }

        private Sender ConvertQueueToSend (Queuer queuer)
        {
            Sender sender = null;

            uint id = queuer.Id;
            IMessage message = queuer.Message;
            Socket socket = null;

            bool success = m_idToSocket.TryGetValue(id, out socket);
            if (success) {
                sender = m_socketSend[socket];

                MemoryStream stream = sender.Stream;
                stream.Position = 0;
                stream.SetLength(0);
                
                // Write in the message type.
                int type = (int) message.Type;
                stream.Write(BitConverter.GetBytes(type), 0, sizeof(int));

                // Serialize the rest of the data.
                message.SerializeTo(stream);

                // See if we have a crypto override for this message.
                ICryptoProvider cryptoProvider = (null != queuer.CryptoOverride) ? queuer.CryptoOverride : sender.CryptoProvider;

                // Encrypt the buffer before we set its length.
                if (null != cryptoProvider) {
                    byte[] encryptBuffer = cryptoProvider.Encrypt(stream.ToArray());
                    stream.SetLength(encryptBuffer.Length);
                    encryptBuffer.CopyTo(stream.GetBuffer(), 0);
                }

                // TODO: Enforce max message size?

                // Set the outgoing size for the stream.
                sender.Size = BitConverter.GetBytes((int) stream.Length);
            }
            else {
                // TODO: Log error.
            }

            return sender;
        }
        #endregion

        #region Protected types
        protected class Sender
        {
            // Constructors.
            public Sender (uint id, Socket socket)
            {
                this.Id = id;
                this.Socket = socket;
                this.Stream = new MemoryStream(MAX_MESSAGE_SIZE);
                this.Sent = 0;
#if SILVERLIGHT
                this.EventArgs = new SocketAsyncEventArgs();
#endif
            }

            // Properties.
            public uint             Id { get; private set; }
            public Socket           Socket { get; private set; }
            public MemoryStream     Stream { get; private set; }
            public int              Sent { get; set; }
            public byte[]           Size { get; set; }
            public ICryptoProvider  CryptoProvider { get; set; }
#if SILVERLIGHT
            public SocketAsyncEventArgs EventArgs { get; private set; }
#endif
        }

        protected class Receiver
        {
            // Constructors.
            public Receiver (uint id, Socket socket)
            {
                this.Id = id;
                this.Socket = socket;
                this.Stream = new MemoryStream(MAX_MESSAGE_SIZE);
                this.Received = 0;
                this.Required = 0;
#if SILVERLIGHT
                this.EventArgs = new SocketAsyncEventArgs();
#endif
            }

            // Properties.
            public uint             Id { get; private set; }
            public Socket           Socket { get; private set; }
            public MemoryStream     Stream { get; private set; }
            public int              Received { get; set; }
            public int              Required { get; set; }
            public ICryptoProvider  CryptoProvider { get; set; }
#if SILVERLIGHT
            public SocketAsyncEventArgs EventArgs { get; private set; }
#endif
        }
        #endregion

        #region Private types
        private class Crypto
        {
            // Constructors.
            public Crypto (uint id, ICryptoProvider cryptoProvider)
            {
                this.Id = id;
                this.CryptoProvider = cryptoProvider;
            }

            // Properties.
            public uint Id { get; private set; }
            public ICryptoProvider CryptoProvider { get; private set; }
        }

        private class Queuer
        {
            // Constructors.
            public Queuer (uint id, IMessage message, ICryptoProvider cryptoOverride)
            {
                this.Id = id;
                this.Message = message;
                this.CryptoOverride = cryptoOverride;
            }

            // Properties.
            public uint Id { get; private set; }
            public IMessage Message { get; private set; }
            public ICryptoProvider CryptoOverride { get; private set; }
        }
        #endregion
    }
}
