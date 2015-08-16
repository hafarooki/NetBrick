using NetBrick.Core.Codes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NetBrick.Core.Client.Handlers
{
    public class EstablishEncryptionResponseHandler : PacketHandler
    {
        public EstablishEncryptionResponseHandler(BrickClient client) : base(client)
        {

        }

        public override short Code => (short)FrameworkOperationCode.EstablishEncryption;

        public override PacketType Type => PacketType.Response;

        public override void Handle(Packet packet)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(Client.PrivateKey);
                var key = rsa.Decrypt((byte[])packet.Parameters[(byte)FrameworkParameterCode.AesKey], true);
                var iv = rsa.Decrypt((byte[])packet.Parameters[(byte)FrameworkParameterCode.AesIV], true);

                Client.AesKey = key;
                Client.AesIV = iv;
            }
        }
    }
}
