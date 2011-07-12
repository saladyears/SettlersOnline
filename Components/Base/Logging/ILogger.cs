namespace Base
{
    public interface ILogger
    {
        void Trace<T> (string message, params object[] args);
        void Debug<T> (string message, params object[] args);
        void Info<T> (string message, params object[] args);
        void Warn<T> (string message, params object[] args);
        void Error<T> (string message, params object[] args);
        void Fatal<T> (string message, params object[] args);
    }
}
