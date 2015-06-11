using System;
using NetBrick.Core;
using NetBrick.Core.Server;
using System.Collections.Generic;
using System.Net;
using NetBrick.Chat.Server.Handlers.Requests;

namespace NetBrick.Chat.Server
{
    public abstract class ChatServer : BrickServer
    {
        public abstract string MasterAddress { get; }
        public abstract int MasterPort { get; }

        public ChatServer(string appIdentifier, int port, List<IPEndPoint> servers, int maxConnections = 10, string address = "127.0.0.1")
            : base(appIdentifier, port, maxConnections, address)
        {
            AddHandler(new ChatRequestHandler());

            ConnectToServer(MasterAddress, MasterPort);
        }
    }
}
