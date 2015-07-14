using Lidgren.Network;
using System.Security.Cryptography;

namespace NetBrick.Core.Server
{
    public class BrickPeer
    {
        public NetConnection Connection { get; set; }
        public bool IsServer { get; internal set; }
        public BasePeerHandler PeerHandler { get; set; }
        public byte[] AesKey { get; set; }
        public byte[] AesIV { get; set; }

        public void Kick(string reason)
        {
            Connection.Disconnect(reason);
        }

        public byte[] Encrypt(byte[] plain)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIV;
                return aes.CreateEncryptor().TransformFinalBlock(plain, 0, plain.Length);
            }
        }

        public byte[] Decrypt(byte[] encrypted)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.IV = AesIV;
                return aes.CreateDecryptor().TransformFinalBlock(encrypted, 0, encrypted.Length);
            }
        }
    }
}