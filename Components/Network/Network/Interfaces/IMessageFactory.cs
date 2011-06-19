using System.IO;

namespace Network
{
    public interface IMessageFactory
    {
        IMessage SerializeFrom (MemoryStream stream);
    }
}
