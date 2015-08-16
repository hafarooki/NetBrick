namespace NetBrick.Core.Client.Handlers
{
    public abstract class PacketHandler
    {
        public BrickClient Client { get; }

        public PacketHandler(BrickClient client)
        {
            Client = client;
        }

        public abstract short Code { get; }
        public abstract PacketType Type { get; }
        public abstract void Handle(Packet packet);
    }
}