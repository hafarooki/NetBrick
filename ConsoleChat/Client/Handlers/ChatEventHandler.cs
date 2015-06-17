using System;
using ConsoleChat.Codes;
using NetBrick.Core;
using NetBrick.Core.Client.Handlers;

namespace ConsoleChat.Client.Handlers
{
    public class ChatEventHandler : PacketHandler
    {
        public override short Code => (short)ChatEventCode.Chat;
        public override PacketType Type => PacketType.Event;

        public override void Handle(Packet packet)
        {
            Console.WriteLine((string)packet.Parameters[(byte)ChatParameterCode.Message]);
        }
    }
}