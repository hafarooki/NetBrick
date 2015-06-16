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

        public ChatServer()
        {
            AddHandler(new ChatRequestHandler());

            ConnectToServer(MasterAddress, MasterPort);
        }
    }
}
