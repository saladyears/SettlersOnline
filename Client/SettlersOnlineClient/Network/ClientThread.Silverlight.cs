using System.Net.Sockets;

namespace Network
{
    public class ClientThread : MessageThread
    {
        protected override bool IsDisconnected (Socket socket)
        {
            return false;
        }

        protected override void Send (Sender sender, int offset, int size)
        {
        }

        protected override void Receive (Receiver receiver, int offset, int size)
        {
        }
    }
}
