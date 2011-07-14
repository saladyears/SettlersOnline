using Base;
using NHibernate;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Database
{
    public class DatabaseThread : Thread<DatabaseThread>, IDatabase
    {
        // Fields.
        private ISessionFactory     m_sessionFactory;
        private ISession            m_session;
        private Queue<GetRequest>   m_getRequests = new Queue<GetRequest>();
        private Queue<SetRequest>   m_setRequests = new Queue<SetRequest>();
        private Object              m_getLock = new Object();
        private Object              m_setLock = new Object();

        // Constructors.
        public DatabaseThread (ILogger logger)
            : base(logger)
        {
            // FIXME: Remove this hack when we're running on server.

            // Initialize NHibernate.
            while (true) {
                try {
                    m_sessionFactory = new NHibernate.Cfg.Configuration().Configure().BuildSessionFactory();
                    m_session = m_sessionFactory.OpenSession();
                }
                catch (Exception ex) {
                    WARN("NHibernate failure: {0}", ex.Message);
                }

                if (null != m_session) {
                    break;
                }

                Thread.Sleep(10);
            }
        }

        // Public methods.
        public override void Execute ()
        {
            GetRequests();
            SetRequests();

            Thread.Sleep(10);
        }

        public void Get<T> (object param, AsyncGetCallback callback, object callbackArg) where T : class
        {
            lock (m_getLock) {
                m_getRequests.Enqueue(new GetRequest(typeof(T), param, callback, callbackArg));
            }
        }

        public void Set<T> (T obj, AsyncSetCallback callback, object callbackArg) where T : class
        {
            lock (m_setLock) {
                m_setRequests.Enqueue(new SetRequest(typeof(T), obj, callback, callbackArg));
            }
        }

        // Private methods.
        private void GetRequests ()
        {
            GetRequest getRequest = null;

            lock (m_getLock) {
                if (0 < m_getRequests.Count) {
                    getRequest = m_getRequests.Dequeue();
                }
            }

            if (null != getRequest) {
                object obj = null;

                Type type = getRequest.Type;
                object param = getRequest.Param;
                bool success = true;

                try {
                    obj = m_session.Get(type, param);
                }
                catch (Exception ex) {
                    success = false;
                    ERROR("Failed to get {0} with {1}: {2}", type.FullName, param.ToString(), ex.Message);
                }
                finally {
                    getRequest.Callback(obj, getRequest.Arg, success);
                }
            }
        }

        private void SetRequests ()
        {
            SetRequest setRequest = null;

            lock (m_setLock) {
                if (0 < m_setRequests.Count) {
                    setRequest = m_setRequests.Dequeue();
                }
            }

            if (null != setRequest) {
                Type type = setRequest.Type;
                object obj = setRequest.Obj;
                bool success = true;

                using (ITransaction transaction = m_session.BeginTransaction()) {
                    try {
                        m_session.Save(obj);
                        transaction.Commit();
                    }
                    catch (HibernateException ex) {
                        success = false;
                        ERROR("Failed to set {0}: {1}", type.FullName, ex.Message);

                        if (transaction.IsActive) {
                            transaction.Rollback();
                        }
                    }
                    finally {
                        setRequest.Callback(setRequest.Arg, success);
                    }
                }
            }
        }

        // Private types.
        private class GetRequest
        {
            // Constructors.
            public GetRequest (Type type, object param, AsyncGetCallback callback, object arg)
            {
                this.Type = type;
                this.Param = param;
                this.Callback = callback;
                this.Arg = arg;
            }

            // Properties.
            public Type Type { get; private set; }
            public object Param { get; private set; }
            public AsyncGetCallback Callback { get; private set; }
            public object Arg { get; private set; }
        }

        private class SetRequest
        {
            // Constructors.
            public SetRequest (Type type, object obj, AsyncSetCallback callback, object arg)
            {
                this.Type = type;
                this.Obj = obj;
                this.Callback = callback;
                this.Arg = arg;
            }

            // Properties.
            public Type Type { get; private set; }
            public object Obj { get; private set; }
            public AsyncSetCallback Callback { get; private set; }
            public object Arg { get; private set; }
        }
    }
}
