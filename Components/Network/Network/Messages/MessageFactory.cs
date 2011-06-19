using System.Collections.Generic;
using System.IO;

namespace Network
{
    class MessageFactory
    {
        // Fields.
        private static Dictionary<MessageType, IMessageFactory> s_factories = new Dictionary<MessageType, IMessageFactory>();

        // Public methods.
        public static void RegisterFactory (MessageType type, IMessageFactory factory)
        {
            s_factories.Add(type, factory);
        }

        public static IMessage CreateMessage (MessageType type, MemoryStream stream)
        {
            IMessage ret = null;
            IMessageFactory factory = null;

            bool success = s_factories.TryGetValue(type, out factory);
            if (success) {
                ret = factory.SerializeFrom(stream);
            }

            if (null == ret) {
                // TODO: Log error.
            }

            return ret;
        }
    }
}
