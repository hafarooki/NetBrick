using Lidgren.Network;
using NetBrick.Core.Client.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetBrick.Core.Client
{
    public abstract class BrickClient
    {
        private NetClient _client;

        private List<PacketHandler> _handlers;

        public BrickClient(string appIdentifier)
        {
            var config = new NetPeerConfiguration(appIdentifier);

            config.EnableMessageType(NetIncomingMessageType.Data);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);

            _client = new NetClient(config);

            _handlers = new List<PacketHandler>();
        }

        public void Listen()
        {
            var message = _client.ReadMessage();
            if (message == null) return;

            switch (message.MessageType)
            {
                case NetIncomingMessageType.Data:
                    {
                        var packet = new Packet(message);
                        var handlers = from h in _handlers where h.Code == packet.PacketCode && h.Type == packet.PacketType select h;

                        foreach (var handler in handlers)
                        {
                            handler.Handle(packet);
                        }
                    }
                    break;
                case NetIncomingMessageType.StatusChanged:
                    {
                        var status = (NetConnectionStatus)message.ReadByte();
                        Log(LogLevel.Info, "Status Changed: {0}", status);

                        switch (status)
                        {
                            case NetConnectionStatus.Connected:
                                OnConnect();
                                break;
                            case NetConnectionStatus.Disconnected:
                                OnDisconnect(message.ReadString());
                                break;
                        }
                    }
                    break;
            }
        }

        public abstract void Log(LogLevel info, string v, params object[] args);
        protected abstract void OnDisconnect(string v);
        protected abstract void OnConnect();

        public void Connect(string address, int port)
        {
            _client.Connect(address, port);
        }

        public void Disconnect(string reason = "Client disconnected.")
        {
            _client.Disconnect(reason);
        }
    }
}
