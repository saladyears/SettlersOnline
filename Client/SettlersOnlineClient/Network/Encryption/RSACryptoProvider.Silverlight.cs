using System;

namespace Network
{
    public class RSACryptoProvider : ICryptoProvider
    {
        // Constructors.
        public RSACryptoProvider (RSA.RSACrypto clientProvider)
        {
            this.ClientProvider = clientProvider;
        }

        // Properties.
        private RSA.RSACrypto ClientProvider { get; set; }

        // Public methods.
        public byte[] Encrypt (byte[] bytes)
        {
            // We do not encrypt on the outgoing method.
            return bytes;
        }

        public byte[] Decrypt (byte[] bytes)
        {
            return ClientProvider.Decrypt(bytes);
        }
    }
}
