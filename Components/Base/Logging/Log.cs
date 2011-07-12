namespace Base
{
    public class Log<T>
    {
        // Constructors.
        protected Log (ILogger logger)
        {
            Logger = logger;
        }

        // Properties.
        protected ILogger Logger { get; private set; }

        // Protected methods.
        protected void TRACE (string message, params object[] args)
        {
            Logger.Trace<T>(message, args);
        }

        protected void DEBUG (string message, params object[] args)
        {
            Logger.Debug<T>(message, args);
        }

        protected void INFO (string message, params object[] args)
        {
            Logger.Info<T>(message, args);
        }

        protected void WARN (string message, params object[] args)
        {
            Logger.Warn<T>(message, args);
        }

        protected void ERROR (string message, params object[] args)
        {
            Logger.Error<T>(message, args);
        }

        protected void FATAL (string message, params object[] args)
        {
            Logger.Fatal<T>(message, args);
        }
    }
}
