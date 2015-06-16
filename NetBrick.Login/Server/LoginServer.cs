using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NetBrick.Core;
using NetBrick.Core.Server;
using StackExchange.Redis;

namespace NetBrick.Login.Server
{
    public abstract class LoginServer : BrickServer
    {
        protected LoginServer(string host, int port) : base(TODO, TODO, TODO, TODO)
        {
            ConnectionMultiplexer = ConnectionMultiplexer.Connect(host + ":" + port);
            Database = ConnectionMultiplexer.GetDatabase();
        }

        internal ConnectionMultiplexer ConnectionMultiplexer { get; }
        internal IDatabase Database { get; }

        public void SetData(string key, object value)
        {
            Database.StringSet(key, value.ToBytes());
        }

        public T GetData<T>(string key)
        {
            return ((byte[]) Database.StringGet(key)).FromBytes<T>();
        }

        public bool DataExists(string key)
        {
            return Database.KeyExists(key);
        }

        public void DeleteData(string key)
        {
            Database.KeyDelete(key);
        }

        public void RegisterUser(string username, string password, string email)
        {
            SetData($"Users:{username}:Email", email);
            var salt = GenerateSaltValue();
            var hash = HashPassword(username, salt, new SHA256Managed());
            SetData($"Users:{username}:Salt", salt);
            SetData($"Users:{username}:Password", hash);
        }

        public void DeleteUser(string username)
        {
            DeleteData($"Users:{username}");
        }

        public void SetUserData(string username, string key, object value)
        {
            SetData($"Users:{username}:{key}", value);
        }

        public T GetUserData<T>(string username, string key)
        {
            return GetData<T>($"Users:{username}:{key}");
        }

        public bool UserDataExists(string username, string key)
        {
            return Database.KeyExists($"Users:{username}:{key}");
        }

        public void DeleteUserData(string username, string key)
        {
            DeleteData($"Users:{username}:{key}");
        }

        // https://msdn.microsoft.com/en-us/library/aa545602(v=cs.70).aspx

        private static string GenerateSaltValue()
        {
            var utf16 = new UnicodeEncoding();

            var random = new Random(unchecked((int) DateTime.Now.Ticks));

            var saltValue = new byte[12];

            random.NextBytes(saltValue);

            var saltValueString = utf16.GetString(saltValue);

            return saltValueString;
        }

        private static string HashPassword(string clearData, string saltValue, HashAlgorithm hash)
        {
            var encoding = new UnicodeEncoding();

            if (clearData == null || hash == null) return null;
            if (saltValue == null)
            {
                saltValue = GenerateSaltValue();
            }

            var binarySaltValue = new byte[12];

            binarySaltValue[0] = byte.Parse(saltValue.Substring(0, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture.NumberFormat);
            binarySaltValue[1] = byte.Parse(saltValue.Substring(2, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture.NumberFormat);
            binarySaltValue[2] = byte.Parse(saltValue.Substring(4, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture.NumberFormat);
            binarySaltValue[3] = byte.Parse(saltValue.Substring(6, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture.NumberFormat);

            var valueToHash = new byte[12 + encoding.GetByteCount(clearData)];
            var binaryPassword = encoding.GetBytes(clearData);

            binarySaltValue.CopyTo(valueToHash, 0);
            binaryPassword.CopyTo(valueToHash, 12);

            var hashValue = hash.ComputeHash(valueToHash);

            return hashValue.Aggregate(saltValue,
                (current, hexdigit) => current + hexdigit.ToString("X2", CultureInfo.InvariantCulture.NumberFormat));
        }
    }
}