using System;
using System.Security.Cryptography;

namespace Network
{
    public class RSACryptoProvider : ICryptoProvider
    {
        // Constructors.
        public RSACryptoProvider (RSACryptoServiceProvider clientProvider)
        {
            this.ClientProvider = clientProvider;
        }

        // Properties.
        private RSACryptoServiceProvider ClientProvider { get; set; }

        // Public methods.
        public byte[] Encrypt (byte[] bytes)
        {
            // When we encrypt an outgoing message, we use the client's public
            // key, allowing them to decrypt with their private key.
            return ClientProvider.Encrypt(bytes, true);
        }

        public byte[] Decrypt (byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}
