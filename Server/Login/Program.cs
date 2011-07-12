using Base;
using Database;
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
            DatabaseThread database = new DatabaseThread(logger);

            // Create the login handler.
            Login login = new Login(logger, server, database);
            
            // Start things up.
            server.Run();
            listen.Run();
            database.Run();

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
