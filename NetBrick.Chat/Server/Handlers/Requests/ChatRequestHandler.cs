﻿using NetBrick.Core;
using NetBrick.Core.Codes;
using NetBrick.Core.Server;
using NetBrick.Core.Server.Handlers;

namespace NetBrick.Chat.Server.Handlers.Requests
{
    public class ChatRequestHandler : PacketHandler
    {
        public override short Code
        {
            get { return (short) FrameworkOperationCode.Chat; }
        }

        public override PacketType Type
        {
            get { return PacketType.Request; }
        }

        public override void Handle(Packet packet, BrickPeer sender)
        {
            if ((string) packet.Parameters[(byte) FrameworkParameterCode.To] == "Everyone")
            {
            }
        }
    }
}