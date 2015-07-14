using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DevOne.Security.Cryptography.BCrypt;
using NetBrick.Core;
using NetBrick.Core.Server;
using StackExchange.Redis;

namespace NetBrick.Login.Server
{
    public abstract class LoginServer<TAccountType> : BrickServer where TAccountType : BaseAccount
    {
        protected abstract string Prefix { get; }

        protected LoginServer(string appIdentifier, int port, string address,
            int maxConnections, bool runOnNewThread = true)
            : base(appIdentifier, port, address, maxConnections, runOnNewThread)
        {
        }

        public IDatabase Database { get; set; }

        public void SetStringData(string key, string value)
        {
            Database.StringSet(key, value);
        }

        public string GetStringData(string key)
        {
            return Database.StringGet(key);
        }

        public void SetBinaryData(string key, byte[] bytes)
        {
            Database.StringSet(key, bytes);
        }

        public byte[] GetBinaryData(string key)
        {
            return Database.StringGet(key);
        }

        public bool DataExists(string key)
        {
            return Database.KeyExists(key);
        }

        public void DeleteData(string key)
        {
            Database.KeyDelete(key);
        }

        public void RegisterUser(TAccountType account)
        {
            SetBinaryData($"{Prefix}:Accounts:{account.Username}", account.ToBytes());
        }

        public TAccountType RetrieveUser(string username)
        {
            return GetBinaryData($"{Prefix}:Accounts:{username}").FromBytes<TAccountType>();
        }

        public void DeleteUser(string username)
        {
            DeleteData($"{Prefix}:Accounts:{username}");
        }

        public bool CheckCredentials(string username, string password)
        {
            var user = RetrieveUser(username);
            return BCryptHelper.CheckPassword(password, user.Password);
        }

        public bool UserExists(string username)
        {
            return DataExists($"{Prefix}:Accounts:{username}");
        }

        public void UpdateUser(TAccountType account)
        {
            account.Updated = DateTime.Now;
            SetBinaryData($"{Prefix}:Accounts:{account.Username}", account.ToBytes());
        }
    }
}