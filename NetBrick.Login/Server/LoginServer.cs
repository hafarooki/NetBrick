using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DevOne.Security.Cryptography.BCrypt;
using NetBrick.Core;
using NetBrick.Core.Server;
using System.Data.SQLite;

namespace NetBrick.Login.Server
{
    public abstract class LoginServer<TAccountType> : BrickServer where TAccountType : BaseAccount
    {
        protected abstract string Prefix { get; }

        protected LoginServer(string connectionString, string appIdentifier, int port, string address,
            int maxConnections, bool runOnNewThread = true)
            : base(appIdentifier, port, address, maxConnections, runOnNewThread)
        {
            Connection = new SQLiteConnection(connectionString);
        }

        public SQLiteConnection Connection { get; }

        public bool UserExists()
    }
}