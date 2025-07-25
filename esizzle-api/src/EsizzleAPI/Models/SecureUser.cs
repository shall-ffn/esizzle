namespace EsizzleAPI.Models
{
    /// <summary>
    /// Represents a user from the Users table in Loanmaster database
    /// </summary>
    public class SecureUser
    {
        public int UserID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Name => !string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName) 
            ? $"{FirstName} {LastName}".Trim() 
            : UserName ?? "";
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int AccessLevel { get; set; }
        public int AuthLevel { get; set; }
        public bool Active { get; set; }
        public int? ClientID { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsActive => Active;
        public bool IsSuperUser => AccessLevel >= 10; // Adjust this logic as needed
    }
}