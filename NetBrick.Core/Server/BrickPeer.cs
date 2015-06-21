using Lidgren.Network;

namespace NetBrick.Core.Server
{
    public class BrickPeer
    {
        public NetConnection Connection { get; set; }
        public bool IsServer { get; internal set; }
        public BasePeerHandler PeerHandler { get; set; }

        public void Kick(string reason)
        {
            Connection.Disconnect(reason);
        }
    }
}