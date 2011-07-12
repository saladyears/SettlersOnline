using NLog;

namespace Base
{
    public class NLogger : ILogger
    {
        // Public methods.
        public void Trace<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Trace(message, args);
        }

        public void Debug<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Debug(message, args);
        }

        public void Info<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Info(message, args);
        }

        public void Warn<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Warn(message, args);
        }

        public void Error<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Error(message, args);
        }

        public void Fatal<T> (string message, params object[] args)
        {
            Logger logger = LogManager.GetLogger(typeof(T).FullName);
            logger.Fatal(message, args);
        }
    }
}
