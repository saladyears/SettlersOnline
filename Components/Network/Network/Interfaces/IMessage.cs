using System.IO;

namespace Network
{
    // Public types.
    public enum MessageType
    {
        Login = 0,
        Lobby = 1,
        Game = 2,
        Chat = 3,
    }

    public interface IMessage
    {
        // Properties.
        MessageType Type { get; }

        // Public methods.
        void SerializeTo (MemoryStream stream);
        void SerializeFrom (MemoryStream stream);
    }
}
