using NetBrick.Core.Server;

namespace NetBrick.Physics.Server
{
    public abstract class PhysicsServer : BrickServer
    {
        protected PhysicsServer(string appIdentifier, int port, string address, int maxConnections, bool runOnNewThread = true) : base(appIdentifier, port, address, maxConnections, runOnNewThread)
        {
        }
    }
}