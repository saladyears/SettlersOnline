namespace Login
{
    public delegate void AsyncGetCallback<T> (T obj);

    public interface IDatabase
    {
        void Get<T> (object param, AsyncGetCallback<T> callback);
    }
}
