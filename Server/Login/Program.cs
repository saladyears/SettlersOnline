using Base;
using Network;
using System.Threading;

namespace Login
{
    class Program
    {
        static void Main(string[] args)
        {
            ILogger logger = new NLogger();

            // Create the threads.
            ServerThread server = new ServerThread(logger);
            ListenThread listen = new ListenThread(logger, server, 4530);

            // Create the login handler.
            Login login = new Login(logger, server);
            
            // Start things up.
            server.Run();
            listen.Run();

            // We could potentially make this a service so there's no infinite 
            // looping.
            while (true) {
                if (server.Fatal || listen.Fatal) {
                    // TODO: Potentially attempt to restart?
                    break;
                }

                Thread.Sleep(100);
            }
        }
    }
}
