using Lidgren.Network;

namespace NetBrick.Core.Server
{
    public class BrickPeer
    {
        public NetConnection Connection { get; set; }
        internal BasePeerHandler PeerHandler { get; set; }
    }
}