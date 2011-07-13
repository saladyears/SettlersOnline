using Base;
using System;
using System.Collections.Generic;
using System.IO;

namespace Network
{
    class MessageFactory
    {
        // Public methods.
        public static IMessage CreateMessage (MessageType type, MemoryStream stream, ILogger logger)
        {
            IMessage message = GenerateMessage(type);

            if (null != message) {
                message.SerializeFrom(stream);
            }
            else {
                logger.Error<MessageFactory>("Unable to serialize message of type {0} from {1}", type.ToString(), BitConverter.ToString(stream.GetBuffer()));
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
