namespace ARS.Models.ViewModels
{
    /// <summary>
    /// Request to create a new database configuration.
    /// The password is only used to build the encrypted connection string
    /// and is never stored in the database.
    /// </summary>
    public class CreateDbConfigRequest
    {
        public string DatabaseType { get; set; } = "PostgreSQL";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
