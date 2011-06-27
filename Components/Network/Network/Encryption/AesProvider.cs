using System.Security.Cryptography;

namespace Network
{
    public class AesProvider : ICryptoProvider
    {
        // Fields.
        private ICryptoTransform m_encryptor;
        private ICryptoTransform m_decryptor;

        // Constructors.
        public AesProvider (AesManaged aes)
        {
            m_encryptor = aes.CreateEncryptor();
            m_decryptor = aes.CreateDecryptor();
        }

        // Public methods.
        public byte[] Encrypt (byte[] bytes)
        {
            return m_encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        }

        public byte[] Decrypt (byte[] bytes)
        {
            return m_decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        }
    }
}
