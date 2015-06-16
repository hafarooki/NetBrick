using NetBrick.Core.Server;

namespace NetBrick.Physics.Server
{
    public abstract class PhysicsServer : BrickServer
    {
        protected PhysicsServer() : base(false)
        {
        }
    }
}