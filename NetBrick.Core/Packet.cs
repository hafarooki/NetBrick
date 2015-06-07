using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetBrick.Core
{
    public class Packet
    {
        public PacketType PacketType { get; set; }
        public short PacketCode { get; set; }
        public Dictionary<byte, object> Parameters { get; }

        public Packet(NetBuffer buffer)
        {
            PacketType = (PacketType)buffer.ReadByte();
            PacketCode = buffer.ReadInt16();
            var amount = buffer.ReadByte();
            buffer.SkipPadBits();
            var parameters = buffer.ReadBytes(amount).FromBytes<List<PacketParameter>>(true);
            Parameters = new Dictionary<byte, object>();

            foreach (var parameter in parameters)
            {
                Parameters.Add(parameter.Code, parameter.Data);
            }
        }

        public Packet(PacketType packetType, short packetCode)
        {
            Parameters = new Dictionary<byte, object>();

            PacketType = packetType;
            PacketCode = packetCode;
        }

        internal NetBuffer ToMessage()
        {
            var buffer = new NetBuffer();

            var parameters = new List<PacketParameter>();

            foreach(var key in Parameters.Keys)
            {
                parameters.Add(new PacketParameter(key, Parameters[key]));
            }

            var bytes = parameters.ToBytes();

            buffer.Write((byte)PacketType);
            buffer.Write(PacketCode);
            buffer.Write(bytes.Length);
            buffer.WritePadBits();
            buffer.Write(bytes);

            return buffer;
        }

        private class PacketParameter
        {
            public byte Code { get; set; }
            public object Data { get; set; }

            public PacketParameter(byte code, object data)
            {
                Code = code;
                Data = data;
            }
        }
    }
}
