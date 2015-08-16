using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// Constructor for BrickServer
        /// </summary>
        /// <param name="appIdentifier">The application identifier</param>
        /// <param name="port">The port to listen on</param>
        /// <param name="address">The address to listen on</param>
        /// <param name="maxConnections">The maximum amount of connections</param>
        /// <param name="runOnNewThread">Should the server run on a separate thread?</param>
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

            AddHandler(new EstablishEncryptionRequestHandler(this));
        }

        /// <summary>
        /// A list of address that are known servers
        /// </summary>
        protected abstract List<IPEndPoint> ServerIpList { get; }

        protected virtual int PacketsPerSecond { get { return 100; } }

        /// <summary>
        /// Connected peers
        /// </summary>
        public Dictionary<IPEndPoint, BrickPeer> Peers { get; set; }

        /// <summary>
        /// Start the server
        /// </summary>
        public void Start()
        {
            _server.Start();
        }

        /// <summary>
        /// Method for listening for messages
        /// </summary>
        public void ListenLoop()
        {
            while (!Environment.HasShutdownStarted)
            {
                Listen();
                Thread.Sleep(1000 / PacketsPerSecond);
            }
        }

        /// <summary>
        /// Listens for messages
        /// </summary>
        public void Listen()
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
                            $"A nonexistent peer of the endpoint {message.SenderEndPoint} has tried sending a packet. Disconnected the connection.");
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
                    Log(LogLevel.Info, $"Status Changed for {message.SenderEndPoint}. New Status: {status}");
                    switch (status)
                    {
                        case NetConnectionStatus.Connected:
                        {
                            var peer = new BrickPeer
                            {
                                Connection = message.SenderConnection,
                                IsServer = ServerIpList.Contains(message.SenderEndPoint)
                            };


                            var handler = CreateHandler(peer);

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
                                    $"A nonexistent peer of the endpoint {message.SenderEndPoint} has tried sending disconnecting.");
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
                    Log(LogLevel.Warn, $"Unhandled lidgren message \"{message.MessageType}\" received.");
                    break;
            }
        }

        /// <summary>
        /// Send a message to one peer
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="recipient">The peer to send to</param>
        /// <param name="method">The method of delivery</param>
        /// <param name="sequenceChannel">The sequence channel to send it on</param>
        public void Send(Packet packet, BrickPeer recipient,
            NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 0)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());
            _server.SendMessage(message, recipient.Connection, method, sequenceChannel);
        }

        /// <summary>
        /// Send a message to multiple peers
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="recipients">The peers to send to</param>
        /// <param name="method">The method of delivery</param>
        /// <param name="sequenceChannel">The sequence channel to send it on</param>
        public void Send(Packet packet, IEnumerable<BrickPeer> recipients,
            NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 0)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());

            var connections = recipients.Select(peer => peer.Connection).ToList();

            _server.SendMessage(message, connections, method, sequenceChannel);
        }

        /// <summary>
        /// Senda  message to all peers
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="method">The method of delivery</param>
        public void SendToAll(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
        {
            var message = _server.CreateMessage();
            message.Write(packet.ToMessage());
            _server.SendToAll(message, method);
        }

        /// <summary>
        /// Add a handler that handles packets
        /// </summary>
        /// <param name="handler">The handler to add</param>
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

        /// <summary>
        /// Remove a handler
        /// </summary>
        /// <param name="handler">The handler to remove</param>
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

        /// <summary>
        /// Connect to a server
        /// </summary>
        /// <param name="address">The address of the server</param>
        /// <param name="port">The port of the server</param>
        public void ConnectToServer(string address, int port)
        {
            _server.Connect(address, port);
        }

        public abstract BasePeerHandler CreateHandler(BrickPeer peer);
        public abstract void Log(LogLevel level, string message);
    }
}