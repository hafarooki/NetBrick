using System.Collections.Generic;
using Lidgren.Network;
using NetBrick.Core.Client.Handlers;
using System;
using System.Security.Cryptography;
using NetBrick.Core.Codes;

namespace NetBrick.Core.Client
{
    public abstract class BrickClient
    {
        private readonly NetClient _client;
        private readonly Dictionary<short, PacketHandler> _eventHandlers;
        private readonly Dictionary<short, PacketHandler> _requestHandlers;
        private readonly Dictionary<short, PacketHandler> _responseHandlers;
        public byte[] AesKey { get; set; }
        public byte[] AesIV { get; set; }
        internal RSAParameters PrivateKey { get; private set; }

        protected BrickClient(string appIdentifier)
        {
            var config = new NetPeerConfiguration(appIdentifier);

            config.EnableMessageType(NetIncomingMessageType.Data);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);

            _client = new NetClient(config);

            _requestHandlers = new Dictionary<short, PacketHandler>();
            _responseHandlers = new Dictionary<short, PacketHandler>();
            _eventHandlers = new Dictionary<short, PacketHandler>();

            AddHandler(new EstablishEncryptionResponseHandler(this));

            _client.Start();
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

                        handler?.Handle(packet);
                    }
                    break;
                case NetIncomingMessageType.StatusChanged:
                    {
                        var status = (NetConnectionStatus)message.ReadByte();
                        Log(LogLevel.Info, $"Status Changed: {status}");

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

        public abstract void Log(LogLevel level, string message);
        protected abstract void OnDisconnect(string reason);
        protected abstract void OnConnect();

        public void Connect(string address, int port)
        {
            _client.Connect(address, port);
        }

        public void Disconnect(string reason = "Client disconnected.")
        {
            _client.Disconnect(reason);
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

        public void Send(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 0)
        {
            var message = _client.CreateMessage();
            message.Write(packet.ToMessage());
            _client.SendMessage(message, method, sequenceChannel);
        }

        public void EstablishEncryption(int keySize = 2048)
        {
            using (var rsa = new RSACryptoServiceProvider(keySize))
            {
                PrivateKey = rsa.ExportParameters(true);
                var packet = new Packet(PacketType.Request, (short)FrameworkOperationCode.EstablishEncryption);
                packet.Parameters[(byte)FrameworkParameterCode.PublicKey] = rsa.ToXmlString(false);
                Send(packet);
            }
        }

        public byte[] Encrypt(byte[] plain)
        {
            using(var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIV;
                return aes.CreateEncryptor().TransformFinalBlock(plain, 0, plain.Length);
            }
        }

        public byte[] Decrypt(byte[] encrypted)
        {
            using(var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIV;
                return aes.CreateDecryptor().TransformFinalBlock(encrypted, 0, encrypted.Length);
            }
        }
    }
}