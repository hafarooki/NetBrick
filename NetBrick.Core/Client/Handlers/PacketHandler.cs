namespace NetBrick.Core.Client.Handlers
{
    public abstract class PacketHandler
    {
        public abstract short Code { get; }
        public abstract PacketType Type { get; }
        public abstract void Handle(Packet packet);
    }
}