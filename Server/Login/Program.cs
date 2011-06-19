using System;
using System.Threading;

namespace Login
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create the threads.
            Network.ServerThread server = new Network.ServerThread();
            Network.ListenThread listen = new Network.ListenThread(4530, server);

            Thread messageThread = new Thread(new ThreadStart(server.Start));
            Thread loginThread = new Thread(new ThreadStart(listen.Start));

            // Create the login handler.
            Login login = new Login(server);
            
            // Start things up.
            messageThread.Start();
            loginThread.Start();

            // We could potentially make this a service so there's no infinite 
            // looping.
            while (true) ;
        }
    }
}
