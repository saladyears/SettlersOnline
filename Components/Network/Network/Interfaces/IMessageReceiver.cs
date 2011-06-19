namespace Network
{
    public interface IMessageReceiver
    {
        void ReceiveMessage (uint id, IMessage message);
    }
}
