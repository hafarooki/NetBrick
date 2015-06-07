using System.Net;

namespace NetBrick.Core.Server
{
    public abstract class BasePeerHandler
    {
        public BrickPeer Peer { get; internal set; }

        public abstract void OnConnect(IPEndPoint endPoint);
        public abstract void OnDisconnect(string reason);

        public abstract bool IsServer();
    }
}