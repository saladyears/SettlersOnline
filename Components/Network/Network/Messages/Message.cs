using System;
using System.IO;
using System.Text;

namespace Network
{
    public abstract class Message<T> : IMessage where T : IMessage, new()
    {
        // Properties.
        abstract public MessageType Type { get; }

        // Public methods.
        abstract public void SerializeTo (MemoryStream stream);
        abstract public void SerializeFrom (MemoryStream stream);

        // Protected methods.
        protected void WriteString (string str, MemoryStream stream)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);

            WriteInt((int) bytes.Length, stream);
            stream.Write(bytes, 0, bytes.Length);
        }

        protected void WriteBytes (byte[] bytes, MemoryStream stream)
        {
            WriteInt((int) bytes.Length, stream);
            stream.Write(bytes, 0, bytes.Length);
        }

        protected void WriteInt (int val, MemoryStream stream)
        {
            byte[] bytes = BitConverter.GetBytes(val);
            stream.Write(bytes, 0, bytes.Length);
        }

        protected string ReadString (MemoryStream stream)
        {
            byte[] buffer = stream.GetBuffer();

            int length = ReadInt(stream);

            string str = Encoding.UTF8.GetString(buffer, (int) stream.Position, length);
            stream.Position += length;

            return str;
        }

        protected byte[] ReadBytes (MemoryStream stream)
        {
            byte[] buffer = stream.GetBuffer();

            int length = ReadInt(stream);
            
            byte[] bytes = new byte[length];
            stream.Read(bytes, 0, length);
            
            return bytes;
        }

        protected int ReadInt (MemoryStream stream)
        {
            byte[] buffer = stream.GetBuffer();

            int val = BitConverter.ToInt32(buffer, (int) stream.Position);
            stream.Position += sizeof(int);

            return val;
        }

        // Private types.
        private class Factory : IMessageFactory
        {
            static Factory ()
            {
                // This just feels so wrong.
                IMessage message = new T();
                MessageFactory.RegisterFactory(message.Type, new Factory());
            }

            public IMessage SerializeFrom (MemoryStream stream)
            {
                IMessage message = new T();
                message.SerializeFrom(stream);
                return message;
            }
        }
    }
}
