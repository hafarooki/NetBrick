using System;
using DevOne.Security.Cryptography.BCrypt;

namespace NetBrick.Login.Server
{
    public abstract class BaseAccount
    {
        public string Username { get; set; }
        public string Salt { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public DateTime Registered { get; set; }
        public DateTime Updated { get; set; }

        /// <summary>
        /// For (de)serialization only.
        /// </summary>
        public BaseAccount()
        {
        }

        public BaseAccount(string username, string password, string email)
        {
            Username = username;
            Salt = BCryptHelper.GenerateSalt();
            Password = BCryptHelper.HashPassword(password, Salt);
            Email = email;
            Registered = DateTime.Now;
            Updated = DateTime.Now;
        }
    }
}