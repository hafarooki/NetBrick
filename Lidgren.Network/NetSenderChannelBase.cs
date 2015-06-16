namespace Lidgren.Network
{
    internal abstract class NetSenderChannelBase
    {
        // access this directly to queue things in this channel
        protected NetQueue<NetOutgoingMessage> m_queuedSends;
        internal abstract int WindowSize { get; }

        internal int QueuedSendsCount
        {
            get { return m_queuedSends.Count; }
        }

        internal abstract int GetAllowedSends();

        public int GetFreeWindowSlots()
        {
            return GetAllowedSends() - m_queuedSends.Count;
        }

        internal abstract NetSendResult Enqueue(NetOutgoingMessage message);
        internal abstract void SendQueuedMessages(double now);
        internal abstract void Reset();
        internal abstract void ReceiveAcknowledge(double now, int sequenceNumber);
    }
}