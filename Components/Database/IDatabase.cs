namespace Database
{
    public delegate void AsyncGetCallback (object obj, object callbackArg, bool success);
    public delegate void AsyncSetCallback (object callbackArg, bool success);

    public interface IDatabase
    {
        void Get<T> (object param, AsyncGetCallback callback, object callbackArg) where T : class;
        void Set<T> (T obj, AsyncSetCallback callback, object callbackArg) where T : class;
    }
}
