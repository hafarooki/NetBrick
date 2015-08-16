using NetBrick.Core.Codes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NetBrick.Core.Server.Handlers
{
    public class EstablishEncryptionRequestHandler : PacketHandler
    {
        public EstablishEncryptionRequestHandler(BrickServer server) : base(server)
        {

        }

        public override short Code
        {
            get
            {
                return (short)FrameworkOperationCode.EstablishEncryption;
            }
        }

        public override PacketType Type
        {
            get
            {
                return PacketType.Request;
            }
        }

        public override void Handle(Packet packet, BrickPeer sender)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString((string)packet.Parameters[(byte)FrameworkParameterCode.PublicKey]);

                using (var aes = Aes.Create())
                {
                    aes.GenerateKey();
                    aes.GenerateIV();
                    sender.AesKey = aes.Key;
                    sender.AesIV = aes.IV;

                    var key = rsa.Encrypt(sender.AesKey, true);
                    var iv = rsa.Encrypt(sender.AesIV, true);

                    var response = new Packet(PacketType.Response, (short)FrameworkOperationCode.EstablishEncryption);
                    response.Parameters[(byte)FrameworkParameterCode.AesKey] = key;
                    response.Parameters[(byte)FrameworkParameterCode.AesIV] = iv;
                    Server.Send(response, sender);
                }
            }
        }
    }
}
