using System;
using ConsoleChat.Codes;
using NetBrick.Core;
using NetBrick.Core.Server;
using NetBrick.Core.Server.Handlers;

namespace ConsoleChat.Server.Handlers
{
    public class ChatRequestHandler : PacketHandler
    {
        public override short Code => (short)ChatOperationCode.Chat;

        public override PacketType Type => PacketType.Request;

        public override void Handle(Packet packet, BrickPeer sender)
        {
            Console.WriteLine("test");

            var name = (string)packet.Parameters[(byte)ChatParameterCode.Name];
            var message = (string)packet.Parameters[(byte)ChatParameterCode.Message];

            var chatPacket = new Packet(PacketType.Event, (short)ChatEventCode.Chat);
            chatPacket.Parameters[(byte)ChatParameterCode.Message] = $"{name}: {message}";

            Console.WriteLine($"{name}: {message}");
            ChatServer.Instance.SendToAll(chatPacket);
        }
    }
}