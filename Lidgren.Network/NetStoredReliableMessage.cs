namespace Lidgren.Network
{
    internal struct NetStoredReliableMessage
    {
        public double LastSent;
        public NetOutgoingMessage Message;
        public int NumSent;
        public int SequenceNumber;

        public void Reset()
        {
            NumSent = 0;
            LastSent = 0.0;
            Message = null;
        }
    }
}