﻿using Network;
using System;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace SettlersOnlineClient
{
    public class Login : IMessageReceiver
    {
        // Fields.
        private INetworkManager     m_networkManager;
        private RSA.RSACrypto       m_rsa = new RSA.RSACrypto(2048);
        private bool                m_keysGenerated;
        private uint                m_id;
        private string              m_name;
        private string              m_password;
        
        // Properties.
        private Stage State { get; set; }

        // Constructors.
        public Login (INetworkManager networkManager, string name, string password)
        {
            this.State = Stage.Disconnected;

            m_networkManager = networkManager;
            m_name = name;
            m_password = password;
            
            m_networkManager.OnConnect += new Connect(OnConnect);
            m_networkManager.OnDisconnect += new Disconnect(OnDisconnect);
            m_networkManager.AddReceiver(MessageType.Login, this);

            m_rsa.OnKeysGenerated += new RSA.RSACrypto.KeysGenerated(OnKeysGenerated);
        }

        // Public methods.
        public void ReceiveMessage (uint id, IMessage message)
        {
            LoginMessage loginMessage = message as LoginMessage;

            switch (this.State) {
                case Stage.ReceiveAesData:
                    ReceiveAesData(loginMessage);
                    break;
                default:
                    break;
            }
        }

        // Private methods.
        private void OnConnect (uint id)
        {
            m_id = id;

            // Start generating our keys (if needed).
            if (!m_keysGenerated) {
                m_rsa.GenerateKeys();
            }
            else {
                OnKeysGenerated(m_rsa);
            }
        }

        private void OnDisconnect (uint id)
        {
            this.State = Stage.Disconnected;
        }

        private void OnKeysGenerated (object sender)
        {
            m_keysGenerated = true;

            this.State = Stage.ReceiveAesData;

            // Set up a decryptor for our next message before we switch to AES.
            m_networkManager.SetCryptoProvider(m_id, new RSACryptoProvider(sender as RSA.RSACrypto));

            // Send our public key to the server.
            LoginMessage loginMessage = new LoginMessage(m_rsa.ToXmlString(false), null);
            m_networkManager.SendMessage(m_id, loginMessage);
        }

        private void ReceiveAesData (LoginMessage loginMessage)
        {
            // Create a new Aes provider from our details.
            AesManaged aes = new AesManaged();
            aes.KeySize = 256;

            byte[] data = loginMessage.Data;
            byte[] key = new byte[aes.KeySize / 8];
            byte[] iv = new byte[data.Length - key.Length];

            Array.Copy(data, key, key.Length);
            Array.Copy(data, key.Length, iv, 0, iv.Length);

            aes.Key = key;
            aes.IV = iv;

            m_networkManager.SetCryptoProvider(m_id, new AesProvider(aes));
            
            // Send our login information.
            loginMessage.Name = m_name;
            loginMessage.Data = Encoding.UTF8.GetBytes(m_password);
            m_networkManager.SendMessage(m_id, loginMessage);
        }

        // Private types.
        private enum Stage
        {
            Disconnected,
            ReceiveAesData,
        }        
    }
}