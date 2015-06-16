using System;

namespace ConsoleChat
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Invalid input - must be one argument (Server or Client)");
                Console.ReadKey(true);
            }

            switch (args[0])
            {
                case "Server":

                    break;
                case "Client":
                    break;
                default:
                    Console.Error.WriteLine("Invalid input - must be either Server or Client");
                    break;
            }
        }
    }
}
