namespace Database
{
    public delegate void AsyncGetCallback (object obj, object callbackArg);

    public interface IDatabase
    {
        void Get<T> (object param, AsyncGetCallback callback, object callbackArg) where T : class;
    }
}
