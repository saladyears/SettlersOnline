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
        private const int DEFAULT_MESSAGE_SIZE = 4096;
        #endregion

        #region Fields
        private volatile bool   m_stop;
        
        // These fields require thread-safety.
        protected Dictionary<MessageType, HashSet<IMessageReceiver>> m_receivers = new Dictionary<MessageType, HashSet<IMessageReceiver>>();
        private Queue<Socket> m_connectQueue = new Queue<Socket>();
        private Queue<Socket> m_disconnectQueue = new Queue<Socket>();
        private Queue<Queuer> m_sendQueue = new Queue<Queuer>();
        protected Object m_receiverLock = new Object();
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

        public void SendMessage (uint id, IMessage message)
        {
            lock (m_sendLock) {
                m_sendQueue.Enqueue(new Queuer(id, message));
            }
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

        private void HandleDisconnects ()
        {
            lock (m_disconnectLock) {
                for (int i = 0; i < m_disconnectQueue.Count; ++i) {
                    Socket socket = m_disconnectQueue.Dequeue();
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
                        Send(sender, 0, (int) sender.Stream.Length);
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

                // Reset the stream's size.
                MemoryStream stream = sender.Stream;
                stream.SetLength(sizeof(int));

                // Set up the stream's position so we can write the size
                // in after we serialize the message.
                stream.Position = sizeof(int);

                // Write in the message type.
                int type = (int) message.Type;
                stream.Write(BitConverter.GetBytes(type), 0, sizeof(int));

                // Serialize the rest of the data.
                message.SerializeTo(stream);

                // TODO: Encrypt the buffer before we set its length.

                stream.Position = 0;

                int length = (int) stream.Length - sizeof(int);
                stream.Write(BitConverter.GetBytes(length), 0, sizeof(int));
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
                this.Stream = new MemoryStream(DEFAULT_MESSAGE_SIZE);
                this.Sent = 0;
            }

            // Properties.
            public uint         Id { get; private set; }
            public Socket       Socket { get; private set; }
            public MemoryStream Stream { get; private set; }
            public int          Sent { get; set; }
        }

        protected class Receiver
        {
            // Constructors.
            public Receiver (uint id, Socket socket)
            {
                this.Id = id;
                this.Socket = socket;
                this.Stream = new MemoryStream(DEFAULT_MESSAGE_SIZE);
                this.Received = 0;
                this.Required = 0;
            }

            // Properties.
            public uint         Id { get; private set; }
            public Socket       Socket { get; private set; }
            public MemoryStream Stream{ get; private set; }
            public int          Received { get; set; }
            public int          Required { get; set; }
        }
        #endregion

        #region Private types
        private class Queuer
        {
            // Constructors.
            public Queuer (uint id, IMessage message)
            {
                this.Id = id;
                this.Message = message;
            }

            // Properties.
            public uint Id { get; private set; }
            public IMessage Message { get; private set; }
        }
        #endregion
    }
}
