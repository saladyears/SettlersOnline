using System.IO;
using System.Text;

namespace Network
{
    public class LoginMessage : Message
    {
        // Constructors.
        public LoginMessage ()
        {
        }

        public LoginMessage (string name, byte[] data)
        {
            this.Name = name;
            this.Data = data;
        }

        // Properties.
        public override MessageType Type { get { return MessageType.Login; } }
        public string Name { get; set; }
        public byte[] Data { get; set; }

        // Public methods.
        public override void SerializeTo (MemoryStream stream)
        {
            WriteString(this.Name, stream);
            WriteBytes(this.Data, stream);
        }

        public override void SerializeFrom (MemoryStream stream)
        {
            this.Name = ReadString(stream);
            this.Data = ReadBytes(stream);
        }
    }
}
