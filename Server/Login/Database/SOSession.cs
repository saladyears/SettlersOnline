using System;

namespace Login
{
    class SOSession
    {
        public virtual int UserId { get; set; }
        public virtual DateTime LastUpdate { get; set; }
        public virtual byte[] IV { get; set; }
        public virtual byte[] EncryptionKey { get; set; }
        public virtual int GameId { get; set; }
    }
}
