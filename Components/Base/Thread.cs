using System;
using System.Threading;

namespace Base
{
    public abstract class Thread<T> : Log<T>
    {
        // Fields.
        private volatile bool m_done;
        private volatile bool m_fatal;

        // Constructors.
        protected Thread (ILogger logger) 
            : base(logger) 
        { 
        }

        // Properties.
        public bool Done { 
            protected get 
            {
                return m_done;
            }
            set 
            {
                m_done = value;
            }
        }

        public bool Fatal {
            get 
            {
                return m_fatal;
            }
            protected set
            {
                m_fatal = value;
            }
        }

        // Abstract methods.
        public abstract void Execute ();

        // Public methods.
        public void Run ()
        {
            Thread thread = new Thread(new ThreadStart(this.Start));
            thread.Start();

            TRACE("Started thread");
        }

        // Private methods.
        private void Start ()
        {
            while (!m_fatal && !m_done) {
                Execute();
            }
        }
    }
}
