namespace Login
{
    public class User
    {
        public virtual int UserId { get; set; }
        public virtual string Name { get; set; }
        public virtual byte[] Password { get; set; }
        public virtual string HashMethod { get; set; }
    }
}
