namespace NetBrick.Core.Server.Handlers
{
    public abstract class PacketHandler
    {
        public BrickServer Server { get; }

        protected PacketHandler(BrickServer server)
        {
            Server = server;
        }

        public abstract short Code { get; }
        public abstract PacketType Type { get; }
        public abstract void Handle(Packet packet, BrickPeer sender);
    }
}