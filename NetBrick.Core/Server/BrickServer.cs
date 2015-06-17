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
        private readonly Dictionary<short, PacketHandler> _eventHandlers;
        private readonly Dictionary<short, PacketHandler> _requestHandlers;
        private readonly Dictionary<short, PacketHandler> _responseHandlers;
        private readonly NetServer _server;

        protected BrickServer(string appIdentifier, int port, string address, int maxConnections,
            bool runOnNewThread = true)
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
                new Thread(ListenLoop).Start();

            _requestHandlers = new Dictionary<short, PacketHandler>();
            _responseHandlers = new Dictionary<short, PacketHandler>();
            _eventHandlers = new Dictionary<short, PacketHandler>();
            Peers = new Dictionary<IPEndPoint, BrickPeer>();
        }

        protected abstract List<IPEndPoint> ServerIpList { get; }
        public Dictionary<IPEndPoint, BrickPeer> Peers { get; set; }

        public Dictionary<IPEndPoint, BrickPeer> Clients
        {
            get { return (Dictionary<IPEndPoint, BrickPeer>) Peers.Where(p => !p.Value.IsServer); }
        }

        public Dictionary<IPEndPoint, BrickPeer> Servers
        {
            get { return (Dictionary<IPEndPoint, BrickPeer>) Peers.Where(p => p.Value.IsServer); }
        }

        public void Start()
        {
            _server.Start();
        }

        public void ListenLoop()
        {
            while (!Environment.HasShutdownStarted)
            {
                Listen();
            }
        }

        private void Listen()
        {
            var message = _server.ReadMessage();

            if (message == null) return;

            switch (message.MessageType)
            {
                case NetIncomingMessageType.Data:
                {
                    BrickPeer peer;
                    Peers.TryGetValue(message.SenderEndPoint, out peer);

                    if (peer == null)
                    {
                        Log(LogLevel.Warn,
                            "A nonexistent peer of the endpoint {0} has tried sending a packet. Disconnected the connection.",
                            message.SenderEndPoint);
                        message.SenderConnection.Disconnect("Nonexistent peer detected.");
                        return;
                    }

                    var packet = new Packet(message);
                    PacketHandler handler = null;

                    switch (packet.PacketType)
                    {
                        case PacketType.Request:
                            _requestHandlers.TryGetValue(packet.PacketCode, out handler);
                            break;
                        case PacketType.Response:
                            _responseHandlers.TryGetValue(packet.PacketCode, out handler);
                            break;
                        case PacketType.Event:
                            _eventHandlers.TryGetValue(packet.PacketCode, out handler);
                            break;
                    }

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

                            if (peer == null)
                            {
                                Log(LogLevel.Warn,
                                    "A nonexistent peer of the endpoint {0} has tried sending disconnecting.",
                                    message.SenderEndPoint);
                                message.SenderConnection.Disconnect("Nonexistent peer detected.");
                                return;
                            }

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
            switch (handler.Type)
            {
                case PacketType.Event:
                    _eventHandlers.Add(handler.Code, handler);
                    break;
                case PacketType.Request:
                    _requestHandlers.Add(handler.Code, handler);
                    break;
                case PacketType.Response:
                    _responseHandlers.Add(handler.Code, handler);
                    break;
            }
        }

        public void RemoveHandler(PacketHandler handler)
        {
            switch (handler.Type)
            {
                case PacketType.Event:
                    _eventHandlers.Remove(handler.Code);
                    break;
                case PacketType.Request:
                    _requestHandlers.Remove(handler.Code);
                    break;
                case PacketType.Response:
                    _responseHandlers.Remove(handler.Code);
                    break;
            }
        }

        public void ConnectToServer(string address, int port)
        {
            _server.Connect(address, port);
        }

        public abstract BasePeerHandler CreateHandler();
        public abstract void Log(LogLevel level, string message, params object[] args);
    }
}