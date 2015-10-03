using System;
using System.Collections.Generic;
using System.Net;
using ConsoleChat.Server.Handlers;
using NetBrick.Core;
using NetBrick.Core.Server;

namespace ConsoleChat.Server
{
    public class ChatServer : BrickServer
    {
        private static ChatServer _instance;

        public ChatServer() : base("ConsoleChat", 25000, "127.0.0.1", 10)
        {
            RegisterHandlers();
        }

        public static ChatServer Instance => _instance ?? (_instance = new ChatServer());
        protected override List<IPEndPoint> ServerIpList => new List<IPEndPoint>();

        public override BasePeerHandler CreateHandler(BrickPeer peer)
        {
            return new ChatPeerHandler();
        }

        public override void Log(LogLevel level, string message)
        {
            Console.WriteLine($"{DateTime.Now} [{level}] {message}"); 
        }

        public void RegisterHandlers()
        {
            AddHandler(new ChatRequestHandler(this));
        }
    }
}