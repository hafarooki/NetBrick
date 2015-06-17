using System;
using System.Threading;
using ConsoleChat.Client;
using ConsoleChat.Server;

namespace ConsoleChat
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Server, or Client?");

            switch (Console.ReadLine())
            {
                case "Server":
                    ChatServer.Instance.Start();
                    break;
                case "Client":
                    new Thread(ChatClient.Instance.ListenLoop).Start();
                    ChatClient.Instance.Connect("localhost", 25000);
                    break;
                default:
                    Console.Error.WriteLine("Invalid input - must be either Server or Client.\nPress any key to exit...");
                    Console.ReadKey(true);
                    break;
            }
        }
    }
}