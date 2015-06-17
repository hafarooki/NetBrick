using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
#if !__NOIPENDPOINT__
using NetEndPoint = System.Net.IPEndPoint;

#endif

namespace Lidgren.Network
{
    public partial class NetPeer
    {
        internal readonly NetPeerConfiguration m_configuration;
        private readonly object m_initializeLock = new object();
        private readonly NetQueue<NetIncomingMessage> m_releasedIncomingMessages;
        internal readonly NetPeerStatistics m_statistics;
        internal readonly NetQueue<NetTuple<NetEndPoint, NetOutgoingMessage>> m_unsentUnconnectedMessages;
        internal bool m_executeFlushSendQueue;
        private uint m_frameCounter;
        internal Dictionary<NetEndPoint, NetConnection> m_handshakes;
        private double m_lastHeartbeat;
        private double m_lastSocketBind = float.MinValue;
        private AutoResetEvent m_messageReceivedEvent;
        internal bool m_needFlushSendQueue;
        private Thread m_networkThread;
        internal NetIncomingMessage m_readHelperMessage;
        internal byte[] m_receiveBuffer;
        private List<NetTuple<SynchronizationContext, SendOrPostCallback>> m_receiveCallbacks;
        internal byte[] m_sendBuffer;
        private EndPoint m_senderRemote;
        internal long m_uniqueIdentifier;

        /// <summary>
        ///     Gets the socket, if Start() has been called
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        ///     Call this to register a callback for when a new message arrives
        /// </summary>
        public void RegisterReceivedCallback(SendOrPostCallback callback, SynchronizationContext syncContext = null)
        {
            if (syncContext == null)
                syncContext = SynchronizationContext.Current;
            if (syncContext == null)
                throw new NetException("Need a SynchronizationContext to register callback on correct thread!");
            if (m_receiveCallbacks == null)
                m_receiveCallbacks = new List<NetTuple<SynchronizationContext, SendOrPostCallback>>();
            m_receiveCallbacks.Add(new NetTuple<SynchronizationContext, SendOrPostCallback>(syncContext, callback));
        }

        /// <summary>
        ///     Call this to unregister a callback, but remember to do it in the same synchronization context!
        /// </summary>
        public void UnregisterReceivedCallback(SendOrPostCallback callback)
        {
            if (m_receiveCallbacks == null)
                return;

            // remove all callbacks regardless of sync context
            RestartRemoveCallbacks:
            for (var i = 0; i < m_receiveCallbacks.Count; i++)
            {
                if (m_receiveCallbacks[i].Item2.Equals(callback))
                {
                    m_receiveCallbacks.RemoveAt(i);
                    goto RestartRemoveCallbacks;
                }
            }
            if (m_receiveCallbacks.Count < 1)
                m_receiveCallbacks = null;
        }

        internal void ReleaseMessage(NetIncomingMessage msg)
        {
            NetException.Assert(msg.m_incomingMessageType != NetIncomingMessageType.Error);

            if (msg.m_isFragment)
            {
                HandleReleasedFragment(msg);
                return;
            }

            m_releasedIncomingMessages.Enqueue(msg);

            if (m_messageReceivedEvent != null)
                m_messageReceivedEvent.Set();

            if (m_receiveCallbacks != null)
            {
                foreach (var tuple in m_receiveCallbacks)
                {
                    try
                    {
                        tuple.Item1.Post(tuple.Item2, this);
                    }
                    catch (Exception ex)
                    {
                        LogWarning("Receive callback exception:" + ex);
                    }
                }
            }
        }

        private void BindSocket(bool reBind)
        {
            var now = NetTime.Now;
            if (now - m_lastSocketBind < 1.0)
            {
                LogDebug("Suppressed socket rebind; last bound " + (now - m_lastSocketBind) + " seconds ago");
                return; // only allow rebind once every second
            }
            m_lastSocketBind = now;

            if (Socket == null)
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            if (reBind)
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

            Socket.ReceiveBufferSize = m_configuration.ReceiveBufferSize;
            Socket.SendBufferSize = m_configuration.SendBufferSize;
            Socket.Blocking = false;

            var ep = (EndPoint) new NetEndPoint(m_configuration.LocalAddress, reBind ? Port : m_configuration.Port);
            Socket.Bind(ep);

            try
            {
                const uint IOC_IN = 0x80000000;
                const uint IOC_VENDOR = 0x18000000;
                var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                Socket.IOControl((int) SIO_UDP_CONNRESET, new[] {Convert.ToByte(false)}, null);
            }
            catch
            {
                // ignore; SIO_UDP_CONNRESET not supported on this platform
            }

            var boundEp = Socket.LocalEndPoint as NetEndPoint;
            LogDebug("Socket bound to " + boundEp + ": " + Socket.IsBound);
            Port = boundEp.Port;
        }

        private void InitializeNetwork()
        {
            lock (m_initializeLock)
            {
                m_configuration.Lock();

                if (Status == NetPeerStatus.Running)
                    return;

                if (m_configuration.m_enableUPnP)
                    UPnP = new NetUPnP(this);

                InitializePools();

                m_releasedIncomingMessages.Clear();
                m_unsentUnconnectedMessages.Clear();
                m_handshakes.Clear();

                // bind to socket
                BindSocket(false);

                m_receiveBuffer = new byte[m_configuration.ReceiveBufferSize];
                m_sendBuffer = new byte[m_configuration.SendBufferSize];
                m_readHelperMessage = new NetIncomingMessage(NetIncomingMessageType.Error);
                m_readHelperMessage.m_data = m_receiveBuffer;

                var macBytes = NetUtility.GetMacAddressBytes();

                var boundEp = Socket.LocalEndPoint as NetEndPoint;
                var epBytes = BitConverter.GetBytes(boundEp.GetHashCode());
                var combined = new byte[epBytes.Length + macBytes.Length];
                Array.Copy(epBytes, 0, combined, 0, epBytes.Length);
                Array.Copy(macBytes, 0, combined, epBytes.Length, macBytes.Length);
                m_uniqueIdentifier = BitConverter.ToInt64(NetUtility.ComputeShaHash(combined), 0);

                Status = NetPeerStatus.Running;
            }
        }

        private void NetworkLoop()
        {
            VerifyNetworkThread();

            LogDebug("Network thread started");

            //
            // Network loop
            //
            do
            {
                try
                {
                    Heartbeat();
                }
                catch (Exception ex)
                {
                    LogWarning(ex.ToString());
                }
            } while (Status == NetPeerStatus.Running);

            //
            // perform shutdown
            //
            ExecutePeerShutdown();
        }

        private void ExecutePeerShutdown()
        {
            VerifyNetworkThread();

            LogDebug("Shutting down...");

            // disconnect and make one final heartbeat
            var list = new List<NetConnection>(m_handshakes.Count + m_connections.Count);
            lock (m_connections)
            {
                foreach (var conn in m_connections)
                    if (conn != null)
                        list.Add(conn);

                lock (m_handshakes)
                {
                    foreach (var hs in m_handshakes.Values)
                        if (hs != null)
                            list.Add(hs);

                    // shut down connections
                    foreach (var conn in list)
                        conn.Shutdown(m_shutdownReason);
                }
            }

            FlushDelayedPackets();

            // one final heartbeat, will send stuff and do disconnect
            Heartbeat();

            NetUtility.Sleep(10);

            lock (m_initializeLock)
            {
                try
                {
                    if (Socket != null)
                    {
                        try
                        {
                            Socket.Shutdown(SocketShutdown.Receive);
                        }
                        catch (Exception ex)
                        {
                            LogDebug("Socket.Shutdown exception: " + ex);
                        }

                        try
                        {
                            Socket.Close(2); // 2 seconds timeout
                        }
                        catch (Exception ex)
                        {
                            LogDebug("Socket.Close exception: " + ex);
                        }
                    }
                }
                finally
                {
                    Socket = null;
                    Status = NetPeerStatus.NotRunning;
                    LogDebug("Shutdown complete");

                    // wake up any threads waiting for server shutdown
                    if (m_messageReceivedEvent != null)
                        m_messageReceivedEvent.Set();
                }

                m_lastSocketBind = float.MinValue;
                m_receiveBuffer = null;
                m_sendBuffer = null;
                m_unsentUnconnectedMessages.Clear();
                m_connections.Clear();
                m_connectionLookup.Clear();
                m_handshakes.Clear();
            }
        }

        private void Heartbeat()
        {
            VerifyNetworkThread();

            var now = NetTime.Now;
            var delta = now - m_lastHeartbeat;

            var maxCHBpS = 1250 - m_connections.Count;
            if (maxCHBpS < 250)
                maxCHBpS = 250;
            if (delta > (1.0/maxCHBpS) || delta < 0.0) // max connection heartbeats/second max
            {
                m_frameCounter++;
                m_lastHeartbeat = now;

                // do handshake heartbeats
                if ((m_frameCounter%3) == 0)
                {
                    foreach (var kvp in m_handshakes)
                    {
                        var conn = kvp.Value;
#if DEBUG
                        // sanity check
                        if (kvp.Key != kvp.Key)
                            LogWarning("Sanity fail! Connection in handshake list under wrong key!");
#endif
                        conn.UnconnectedHeartbeat(now);
                        if (conn.m_status == NetConnectionStatus.Connected ||
                            conn.m_status == NetConnectionStatus.Disconnected)
                        {
#if DEBUG
                            // sanity check
                            if (conn.m_status == NetConnectionStatus.Disconnected &&
                                m_handshakes.ContainsKey(conn.RemoteEndPoint))
                            {
                                LogWarning("Sanity fail! Handshakes list contained disconnected connection!");
                                m_handshakes.Remove(conn.RemoteEndPoint);
                            }
#endif
                            break; // collection has been modified
                        }
                    }
                }

#if DEBUG
                SendDelayedPackets();
#endif

                // update m_executeFlushSendQueue
                if (m_configuration.m_autoFlushSendQueue && m_needFlushSendQueue)
                {
                    m_executeFlushSendQueue = true;
                    m_needFlushSendQueue = false;
                        // a race condition to this variable will simply result in a single superfluous call to FlushSendQueue()
                }

                // do connection heartbeats
                lock (m_connections)
                {
                    for (var i = m_connections.Count - 1; i >= 0; i--)
                    {
                        var conn = m_connections[i];
                        conn.Heartbeat(now, m_frameCounter);
                        if (conn.m_status == NetConnectionStatus.Disconnected)
                        {
                            //
                            // remove connection
                            //
                            m_connections.RemoveAt(i);
                            m_connectionLookup.Remove(conn.RemoteEndPoint);
                        }
                    }
                }
                m_executeFlushSendQueue = false;

                // send unsent unconnected messages
                NetTuple<NetEndPoint, NetOutgoingMessage> unsent;
                while (m_unsentUnconnectedMessages.TryDequeue(out unsent))
                {
                    var om = unsent.Item2;

                    var len = om.Encode(m_sendBuffer, 0, 0);

                    Interlocked.Decrement(ref om.m_recyclingCount);
                    if (om.m_recyclingCount <= 0)
                        Recycle(om);

                    bool connReset;
                    SendPacket(len, unsent.Item1, 1, out connReset);
                }
            }

            //
            // read from socket
            //
            if (Socket == null)
                return;

            if (!Socket.Poll(1000, SelectMode.SelectRead)) // wait up to 1 ms for data to arrive
                return;

            //if (m_socket == null || m_socket.Available < 1)
            //	return;

            // update now
            now = NetTime.Now;

            do
            {
                var bytesReceived = 0;
                try
                {
                    bytesReceived = Socket.ReceiveFrom(m_receiveBuffer, 0, m_receiveBuffer.Length, SocketFlags.None,
                        ref m_senderRemote);
                }
                catch (SocketException sx)
                {
                    switch (sx.SocketErrorCode)
                    {
                        case SocketError.ConnectionReset:
                            // connection reset by peer, aka connection forcibly closed aka "ICMP port unreachable" 
                            // we should shut down the connection; but m_senderRemote seemingly cannot be trusted, so which connection should we shut down?!
                            // So, what to do?
                            LogWarning("ConnectionReset");
                            return;

                        case SocketError.NotConnected:
                            // socket is unbound; try to rebind it (happens on mobile when process goes to sleep)
                            BindSocket(true);
                            return;

                        default:
                            LogWarning("Socket exception: " + sx);
                            return;
                    }
                }

                if (bytesReceived < NetConstants.HeaderByteSize)
                    return;

                //LogVerbose("Received " + bytesReceived + " bytes");

                var ipsender = (NetEndPoint) m_senderRemote;

                if (UPnP != null && now < UPnP.m_discoveryResponseDeadline && bytesReceived > 32)
                {
                    // is this an UPnP response?
                    var resp = Encoding.ASCII.GetString(m_receiveBuffer, 0, bytesReceived);
                    if (resp.Contains("upnp:rootdevice") || resp.Contains("UPnP/1.0"))
                    {
                        try
                        {
                            resp = resp.Substring(resp.ToLower().IndexOf("location:") + 9);
                            resp = resp.Substring(0, resp.IndexOf("\r")).Trim();
                            UPnP.ExtractServiceUrl(resp);
                            return;
                        }
                        catch (Exception ex)
                        {
                            LogDebug("Failed to parse UPnP response: " + ex);

                            // don't try to parse this packet further
                            return;
                        }
                    }
                }

                NetConnection sender = null;
                m_connectionLookup.TryGetValue(ipsender, out sender);

                //
                // parse packet into messages
                //
                var numMessages = 0;
                var numFragments = 0;
                var ptr = 0;
                while ((bytesReceived - ptr) >= NetConstants.HeaderByteSize)
                {
                    // decode header
                    //  8 bits - NetMessageType
                    //  1 bit  - Fragment?
                    // 15 bits - Sequence number
                    // 16 bits - Payload length in bits

                    numMessages++;

                    var tp = (NetMessageType) m_receiveBuffer[ptr++];

                    var low = m_receiveBuffer[ptr++];
                    var high = m_receiveBuffer[ptr++];

                    var isFragment = ((low & 1) == 1);
                    var sequenceNumber = (ushort) ((low >> 1) | (high << 7));

                    numFragments++;

                    var payloadBitLength = (ushort) (m_receiveBuffer[ptr++] | (m_receiveBuffer[ptr++] << 8));
                    var payloadByteLength = NetUtility.BytesToHoldBits(payloadBitLength);

                    if (bytesReceived - ptr < payloadByteLength)
                    {
                        LogWarning("Malformed packet; stated payload length " + payloadByteLength + ", remaining bytes " +
                                   (bytesReceived - ptr));
                        return;
                    }

                    if (tp >= NetMessageType.Unused1 && tp <= NetMessageType.Unused29)
                    {
                        ThrowOrLog("Unexpected NetMessageType: " + tp);
                        return;
                    }

                    try
                    {
                        if (tp >= NetMessageType.LibraryError)
                        {
                            if (sender != null)
                                sender.ReceivedLibraryMessage(tp, ptr, payloadByteLength);
                            else
                                ReceivedUnconnectedLibraryMessage(now, ipsender, tp, ptr, payloadByteLength);
                        }
                        else
                        {
                            if (sender == null &&
                                !m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.UnconnectedData))
                                return; // dropping unconnected message since it's not enabled

                            var msg = CreateIncomingMessage(NetIncomingMessageType.Data, payloadByteLength);
                            msg.m_isFragment = isFragment;
                            msg.m_receiveTime = now;
                            msg.m_sequenceNumber = sequenceNumber;
                            msg.m_receivedMessageType = tp;
                            msg.m_senderConnection = sender;
                            msg.m_senderEndPoint = ipsender;
                            msg.m_bitLength = payloadBitLength;

                            Buffer.BlockCopy(m_receiveBuffer, ptr, msg.m_data, 0, payloadByteLength);
                            if (sender != null)
                            {
                                if (tp == NetMessageType.Unconnected)
                                {
                                    // We're connected; but we can still send unconnected messages to this peer
                                    msg.m_incomingMessageType = NetIncomingMessageType.UnconnectedData;
                                    ReleaseMessage(msg);
                                }
                                else
                                {
                                    // connected application (non-library) message
                                    sender.ReceivedMessage(msg);
                                }
                            }
                            else
                            {
                                // at this point we know the message type is enabled
                                // unconnected application (non-library) message
                                msg.m_incomingMessageType = NetIncomingMessageType.UnconnectedData;
                                ReleaseMessage(msg);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("Packet parsing error: " + ex.Message + " from " + ipsender);
                    }
                    ptr += payloadByteLength;
                }

                m_statistics.PacketReceived(bytesReceived, numMessages, numFragments);
                if (sender != null)
                    sender.m_statistics.PacketReceived(bytesReceived, numMessages, numFragments);
            } while (Socket.Available > 0);
        }

        /// <summary>
        ///     If NetPeerConfiguration.AutoFlushSendQueue() is false; you need to call this to send all messages queued using
        ///     SendMessage()
        /// </summary>
        public void FlushSendQueue()
        {
            m_executeFlushSendQueue = true;
        }

        internal void HandleIncomingDiscoveryRequest(double now, NetEndPoint senderEndPoint, int ptr,
            int payloadByteLength)
        {
            if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.DiscoveryRequest))
            {
                var dm = CreateIncomingMessage(NetIncomingMessageType.DiscoveryRequest, payloadByteLength);
                if (payloadByteLength > 0)
                    Buffer.BlockCopy(m_receiveBuffer, ptr, dm.m_data, 0, payloadByteLength);
                dm.m_receiveTime = now;
                dm.m_bitLength = payloadByteLength*8;
                dm.m_senderEndPoint = senderEndPoint;
                ReleaseMessage(dm);
            }
        }

        internal void HandleIncomingDiscoveryResponse(double now, NetEndPoint senderEndPoint, int ptr,
            int payloadByteLength)
        {
            if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.DiscoveryResponse))
            {
                var dr = CreateIncomingMessage(NetIncomingMessageType.DiscoveryResponse, payloadByteLength);
                if (payloadByteLength > 0)
                    Buffer.BlockCopy(m_receiveBuffer, ptr, dr.m_data, 0, payloadByteLength);
                dr.m_receiveTime = now;
                dr.m_bitLength = payloadByteLength*8;
                dr.m_senderEndPoint = senderEndPoint;
                ReleaseMessage(dr);
            }
        }

        private void ReceivedUnconnectedLibraryMessage(double now, NetEndPoint senderEndPoint, NetMessageType tp,
            int ptr, int payloadByteLength)
        {
            NetConnection shake;
            if (m_handshakes.TryGetValue(senderEndPoint, out shake))
            {
                shake.ReceivedHandshake(now, tp, ptr, payloadByteLength);
                return;
            }

            //
            // Library message from a completely unknown sender; lets just accept Connect
            //
            switch (tp)
            {
                case NetMessageType.Discovery:
                    HandleIncomingDiscoveryRequest(now, senderEndPoint, ptr, payloadByteLength);
                    return;
                case NetMessageType.DiscoveryResponse:
                    HandleIncomingDiscoveryResponse(now, senderEndPoint, ptr, payloadByteLength);
                    return;
                case NetMessageType.NatIntroduction:
                    if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.NatIntroductionSuccess))
                        HandleNatIntroduction(ptr);
                    return;
                case NetMessageType.NatPunchMessage:
                    if (m_configuration.IsMessageTypeEnabled(NetIncomingMessageType.NatIntroductionSuccess))
                        HandleNatPunch(ptr, senderEndPoint);
                    return;
                case NetMessageType.ConnectResponse:

                    lock (m_handshakes)
                    {
                        foreach (var hs in m_handshakes)
                        {
                            if (hs.Key.Address.Equals(senderEndPoint.Address))
                            {
                                if (hs.Value.m_connectionInitiator)
                                {
                                    //
                                    // We are currently trying to connection to XX.XX.XX.XX:Y
                                    // ... but we just received a ConnectResponse from XX.XX.XX.XX:Z
                                    // Lets just assume the router decided to use this port instead
                                    //
                                    var hsconn = hs.Value;
                                    m_connectionLookup.Remove(hs.Key);
                                    m_handshakes.Remove(hs.Key);

                                    LogDebug("Detected host port change; rerouting connection to " + senderEndPoint);
                                    hsconn.MutateEndPoint(senderEndPoint);

                                    m_connectionLookup.Add(senderEndPoint, hsconn);
                                    m_handshakes.Add(senderEndPoint, hsconn);

                                    hsconn.ReceivedHandshake(now, tp, ptr, payloadByteLength);
                                    return;
                                }
                            }
                        }
                    }

                    LogWarning("Received unhandled library message " + tp + " from " + senderEndPoint);
                    return;
                case NetMessageType.Connect:
                    if (m_configuration.AcceptIncomingConnections == false)
                    {
                        LogWarning("Received Connect, but we're not accepting incoming connections!");
                        return;
                    }
                    // handle connect
                    // It's someone wanting to shake hands with us!

                    var reservedSlots = m_handshakes.Count + m_connections.Count;
                    if (reservedSlots >= m_configuration.m_maximumConnections)
                    {
                        // server full
                        var full = CreateMessage("Server full");
                        full.m_messageType = NetMessageType.Disconnect;
                        SendLibrary(full, senderEndPoint);
                        return;
                    }

                    // Ok, start handshake!
                    var conn = new NetConnection(this, senderEndPoint);
                    conn.m_status = NetConnectionStatus.ReceivedInitiation;
                    m_handshakes.Add(senderEndPoint, conn);
                    conn.ReceivedHandshake(now, tp, ptr, payloadByteLength);
                    return;

                case NetMessageType.Disconnect:
                    // this is probably ok
                    LogVerbose("Received Disconnect from unconnected source: " + senderEndPoint);
                    return;
                default:
                    LogWarning("Received unhandled library message " + tp + " from " + senderEndPoint);
                    return;
            }
        }

        internal void AcceptConnection(NetConnection conn)
        {
            // LogDebug("Accepted connection " + conn);
            conn.InitExpandMTU(NetTime.Now);

            if (m_handshakes.Remove(conn.m_remoteEndPoint) == false)
                LogWarning("AcceptConnection called but m_handshakes did not contain it!");

            lock (m_connections)
            {
                if (m_connections.Contains(conn))
                {
                    LogWarning("AcceptConnection called but m_connection already contains it!");
                }
                else
                {
                    m_connections.Add(conn);
                    m_connectionLookup.Add(conn.m_remoteEndPoint, conn);
                }
            }
        }

        [Conditional("DEBUG")]
        internal void VerifyNetworkThread()
        {
            var ct = Thread.CurrentThread;
            if (Thread.CurrentThread != m_networkThread)
                throw new NetException("Executing on wrong thread! Should be library system thread (is " + ct.Name +
                                       " mId " + ct.ManagedThreadId + ")");
        }

        internal NetIncomingMessage SetupReadHelperMessage(int ptr, int payloadLength)
        {
            VerifyNetworkThread();

            m_readHelperMessage.m_bitLength = (ptr + payloadLength)*8;
            m_readHelperMessage.m_readPosition = (ptr*8);
            return m_readHelperMessage;
        }
    }
}