using System;
using System.Collections.Generic;
using System.Net;
using NetBrick.Core;
using NetBrick.Core.Server;

namespace ConsoleChat.Server
{
    public class ChatServer : BrickServer
    {
        public ChatServer(string appIdentifier, int port, string address, int maxConnections, bool runOnNewThread = true) : base(appIdentifier, port, address, maxConnections, runOnNewThread)
        {
        }

        protected override List<IPEndPoint> ServerIpList { get { return new List<IPEndPoint>(); } }

        public override BasePeerHandler CreateHandler()
        {
            return new ChatPeerHandler();
        }

        public override void Log(LogLevel level, string message, params object[] args)
        {
            Console.WriteLine($"{DateTime.Now} [{level}] {message}", args);
        }
    }
}