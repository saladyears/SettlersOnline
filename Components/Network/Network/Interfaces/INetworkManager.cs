using System.Net.Sockets;

namespace Network
{
    public delegate void Connect (uint id);
    public delegate void Disconnect (uint id);
        
    public interface INetworkManager
    {
        event Connect OnConnect;
        event Disconnect OnDisconnect;

        void HandleConnect (Socket socket);
        void HandleDisconnect (uint id);
        
        void AddReceiver (MessageType type, IMessageReceiver receiver);
        void RemoveReceiver (MessageType type, IMessageReceiver receiver);

        void SendMessage (uint id, IMessage message, ICryptoProvider cryptoOverride = null);

        void SetCryptoProvider (uint id, ICryptoProvider cryptoProvider);
    }
}
