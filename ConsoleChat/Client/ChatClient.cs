using System;
using System.Threading;
using ConsoleChat.Client.Handlers;
using ConsoleChat.Codes;
using NetBrick.Core;
using NetBrick.Core.Client;

namespace ConsoleChat.Client
{
    public class ChatClient : BrickClient
    {
        public string Name { get; private set; }

        private static ChatClient _instance;

        public ChatClient() : base("ConsoleChat")
        {
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            AddHandler(new ChatEventHandler());
        }

        public static ChatClient Instance => _instance ?? (_instance = new ChatClient());

        public void ListenLoop()
        {
            while(!Environment.HasShutdownStarted)
                Listen();
        }

        public override void Log(LogLevel level, string message, params object[] args)
        {
            Console.WriteLine($"{DateTime.Now} [{level}] {message}", args);
        }

        protected override void OnDisconnect(string reason)
        {
            Log(LogLevel.Info, "Disconnected. Reason: {0}", reason);
        }

        protected override void OnConnect()
        {
            Log(LogLevel.Info, "Connected.");

            var start = false;

            while (!start)
            {
                Console.Write("Name: ");
                var name = Console.ReadLine().Trim();

                if (string.IsNullOrEmpty(name))
                {
                    Console.WriteLine();
                    Console.WriteLine("Invalid name! Try again.");
                    continue;
                }

                Name = name;
                start = true;
            }

            new Thread(Chat).Start();
        }

        private void Chat()
        {
            while (!Environment.HasShutdownStarted)
            {
                var message = Console.ReadLine();
                if (string.IsNullOrEmpty(message)) continue;
                message = message.Trim();

                var packet = new Packet(PacketType.Request, (short) ChatOperationCode.Chat);
                packet.Parameters[(byte) ChatParameterCode.Name] = Name;
                packet.Parameters[(byte) ChatParameterCode.Message] = message;

                Send(packet);
            }
        }
    }
}