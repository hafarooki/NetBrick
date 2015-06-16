using NetBrick.Core.Server;

namespace NetBrick.Login.Server
{
    public abstract class LoginServer : BrickServer
    {
        public abstract string MasterAddress { get; }
        public abstract int MasterPort { get; }

        protected LoginServer()
        {
            ConnectToServer(MasterAddress, MasterPort);
        }
    }
}
