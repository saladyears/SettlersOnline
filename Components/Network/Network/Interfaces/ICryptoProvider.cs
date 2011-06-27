namespace Network
{
    public interface ICryptoProvider
    {
        byte[] Encrypt (byte[] bytes);
        byte[] Decrypt (byte[] bytes);
    }
}
