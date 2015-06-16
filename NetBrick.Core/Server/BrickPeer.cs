using Lidgren.Network;

namespace NetBrick.Core.Server
{
    public class BrickPeer
    {
        public NetConnection Connection { get; set; }
        public bool IsServer { get; internal set; }
        internal BasePeerHandler PeerHandler { get; set; }
    }
}