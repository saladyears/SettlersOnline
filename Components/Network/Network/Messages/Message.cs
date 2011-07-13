using System;
using System.IO;
using System.Text;

namespace Network
{
    public abstract class Message : IMessage
    {
        // Properties.
        abstract public MessageType Type { get; }

        // Abstract methods.
        abstract public void SerializeTo (MemoryStream stream);
        abstract public void SerializeFrom (MemoryStream stream);

        // Protected methods.
        protected void WriteString (string str, MemoryStream stream)
        {
            int length = 0;
            byte[] bytes = null;

            if (str != String.Empty) {
                bytes = Encoding.UTF8.GetBytes(str);
                length = bytes.Length;
            }

            WriteInt(length, stream);

            if (0 < length) {
                stream.Write(bytes, 0, length);
            }
        }

        protected void WriteBytes (byte[] bytes, MemoryStream stream)
        {
            int length = (null != bytes) ? (int) bytes.Length : 0;

            WriteInt(length, stream);

            if (0 < length) {
                stream.Write(bytes, 0, bytes.Length);
            }
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
            string str = string.Empty;

            if (0 < length) {
                str = Encoding.UTF8.GetString(buffer, (int) stream.Position, length);
                stream.Position += length;
            }

            return str;
        }

        protected byte[] ReadBytes (MemoryStream stream)
        {
            int length = ReadInt(stream);
            byte[] bytes = null;

            if (0 < length) {
                bytes = new byte[length];
                Array.Copy(stream.GetBuffer(), (int) stream.Position, bytes, 0, length);
                stream.Position += length;
            }
            
            return bytes;
        }

        protected int ReadInt (MemoryStream stream)
        {
            byte[] buffer = stream.GetBuffer();

            int val = BitConverter.ToInt32(buffer, (int) stream.Position);
            stream.Position += sizeof(int);

            return val;
        }
    }
}
