using NetBrick.Core.Server;

namespace NetBrick.Physics.Server
{
    public abstract class PhysicsServer : BrickServer
    {
        public abstract string MasterAddress { get; }
        public abstract int MasterPort { get; }

        public PhysicsManager Manager { get; }

        protected PhysicsServer(string appIdentifier, int port, int maxConnections = 10, string address = "127.0.0.1")
            : base(appIdentifier, port, maxConnections, address)
        {
            ConnectToServer(MasterAddress, MasterPort);
            Manager = new PhysicsManager();
        }
    }
}
