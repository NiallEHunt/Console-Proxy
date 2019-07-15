using System;

namespace ConsoleProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            // Comment out this line if you do not want to use a firewall in the proxy
            Firewall.Initialise();

            // Starts the Proxy Server listening on port 7777
            ProxyServer proxy = new ProxyServer();
            proxy.Start();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Server started on localhost:7777...");
            Console.WriteLine("Type 'exit' to shut down the server.");
            Console.WriteLine("Type 'block' followed by the url of the website you wish to block to add the url to the firewall.");
            Console.ForegroundColor = ConsoleColor.Green;

            bool isRunning = true;
            while (isRunning)
            {
                string line = Console.ReadLine();

                string[] arguments = line.Trim().Split(null);

                string command = arguments[0];

                if (command.ToLower() == "exit")
                {
                    isRunning = false;
                }
                else if (command.ToLower() == "block")
                {
                    Firewall.BlockURL(".*" + arguments[1]);
                }
                else if (command.ToLower() == "unblock")
                {
                    Firewall.UnblockURL(".*" + arguments[1]);
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("Shutting down");

            proxy.Stop();

            Console.WriteLine("Server stopped");
            Console.WriteLine("Press enter to exit");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.ReadLine();
        }
    }
}