using NetBrick.Core.Server;

namespace NetBrick.Physics.Server
{
    public abstract class PhysicsServer : BrickServer
    {
        public abstract string MasterAddress { get; }
        public abstract int MasterPort { get; }

        protected PhysicsServer() : base(false)
        {
            ConnectToServer(MasterAddress, MasterPort);
        }
    }
}
