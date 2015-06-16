using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Lidgren.Network;
using NetBrick.Core.Server.Handlers;

namespace NetBrick.Core.Server
{
    public abstract class BrickServer
    {
        private readonly Dictionary<PacketIdentifier, PacketHandler> _handlers;
        private readonly NetServer _server;
        private readonly Dictionary<PacketIdentifier, PacketHandler> _serverHandlers;

        protected abstract List<IPEndPoint> ServerIpList { get; }

        protected BrickServer(string appIdentifier, int port, string address, int maxConnections, bool runOnNewThread = true)
        {
            var config = new NetPeerConfiguration(appIdentifier);

            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.Data);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);

            config.Port = port;
            config.LocalAddress = IPAddress.Parse(address);
            config.MaximumConnections = maxConnections;

            _server = new NetServer(config);

            if (runOnNewThread)
                new Thread(Listen).Start();

            _handlers = new Dictionary<PacketIdentifier, PacketHandler>();
            _serverHandlers = new Dictionary<PacketIdentifier, PacketHandler>();
        }

        public void Start()
        {
            _server.Start();
        }

        public Dictionary<IPEndPoint, BrickPeer> Peers { get; set; }

        public Dictionary<IPEndPoint, BrickPeer> Clients
        {
            get { return (Dictionary<IPEndPoint, BrickPeer>) Peers.Where(p => !p.Value.IsServer); }
        }

        public Dictionary<IPEndPoint, BrickPeer> Servers
        {
            get { return (Dictionary<IPEndPoint, BrickPeer>) Peers.Where(p => p.Value.IsServer); }
        }

        public void Listen()
        {
            while (!Environment.HasShutdownStarted)
            {
                var message = _server.ReadMessage();

                if (message == null) continue;

                switch (message.MessageType)
                {
                    case NetIncomingMessageType.Data:
                    {
                        BrickPeer peer;
                        Peers.TryGetValue(message.SenderEndPoint, out peer);

                        if (peer == null) throw new Exception("Nonexistant peer sent message!");

                        var packet = new Packet(message);
                        PacketHandler handler;
                        (peer.IsServer ? _serverHandlers : _handlers).TryGetValue(new PacketIdentifier { PacketCode = packet.PacketCode, PacketType = packet.PacketType }, out handler);
                        handler?.Handle(packet, peer);
                    }
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        message.SenderConnection.Approve();
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var status = (NetConnectionStatus) message.ReadByte();
                        Log(LogLevel.Info, "Status Changed for {0}. New Status: {1}", message.SenderEndPoint, status);
                        switch (status)
                        {
                            case NetConnectionStatus.Connected:
                            {
                                var peer = new BrickPeer
                                {
                                    Connection = message.SenderConnection,
                                    IsServer = ServerIpList.Contains(message.SenderEndPoint)
                                };


                                var handler = CreateHandler();

                                peer.PeerHandler = handler;
                                handler.Peer = peer;

                                Peers.Add(peer.Connection.RemoteEndPoint, peer);
                                handler.OnConnect(message.SenderEndPoint);
                            }
                                break;
                            case NetConnectionStatus.Disconnected:
                            {
                                BrickPeer peer;
                                Peers.TryGetValue(message.SenderEndPoint, out peer);

                                if (peer == null) throw new Exception("Nonexistant peer disconnected!");

                                peer.PeerHandler.OnDisconnect(message.ReadString());
                            }
                                break;
                        }
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                        Log(LogLevel.Info, message.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Log(LogLevel.Error, message.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Log(LogLevel.Warn, message.ReadString());
                        break;
                    default:
                        Log(LogLevel.Warn, "Unhandled lidgren message \"{0}\" received.", message.MessageType);
                        break;
                }
            }
        }

        public void Send(Packet packet, BrickPeer recipient,
            NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 0)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());
            _server.SendMessage(message, recipient.Connection, method, sequenceChannel);
        }

        public void Send(Packet packet, IEnumerable<BrickPeer> recipients,
            NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 0)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());

            var connections = recipients.Select(peer => peer.Connection).ToList();

            _server.SendMessage(message, connections, method, sequenceChannel);
        }

        public void SendToAll(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());
            _server.SendToAll(message, method);
        }

        public void AddHandler(PacketHandler handler)
        {
            _handlers.Add(new PacketIdentifier() { PacketCode = handler.Code, PacketType = handler.Type }, handler);
        }

        public void RemoveHandler(PacketHandler handler)
        {
            _handlers.Remove(new PacketIdentifier() { PacketCode = handler.Code, PacketType = handler.Type });
        }

        public void AddServerHandler(PacketHandler handler)
        {
            _serverHandlers.Add(new PacketIdentifier() { PacketCode = handler.Code, PacketType = handler.Type }, handler);
        }

        public void RemoveServerHandler(PacketHandler handler)
        {
            _serverHandlers.Remove(new PacketIdentifier() { PacketCode = handler.Code, PacketType = handler.Type });
        }

        public void ConnectToServer(string address, int port)
        {
            _server.Connect(address, port);
        }

        public abstract BasePeerHandler CreateHandler();
        public abstract void Log(LogLevel level, string message, params object[] args);
    }
}