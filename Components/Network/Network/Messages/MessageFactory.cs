using System.Collections.Generic;
using System.IO;

namespace Network
{
    class MessageFactory
    {
        // Public methods.
        public static IMessage CreateMessage (MessageType type, MemoryStream stream)
        {
            IMessage message = GenerateMessage(type);

            if (null != message) {
                message.SerializeFrom(stream);
            }
            else {
                // TODO: Log error.
            }

            return message;
        }

        // Private methods.
        private static IMessage GenerateMessage (MessageType type)
        {
            IMessage message = null;

            // TODO: Turn this into something fancy and automatic.
            switch (type) {
                case MessageType.Login:
                    message = new LoginMessage();
                    break;
                default:
                    break;
            }

            return message;
        }
    }
}
