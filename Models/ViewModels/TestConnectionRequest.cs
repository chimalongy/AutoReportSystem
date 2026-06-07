namespace ARS.Models.ViewModels
{
    /// <summary>
    /// Request to test a database connection before saving.
    /// Used for both new configurations and testing existing ones.
    /// </summary>
    public class TestConnectionRequest
    {
        public string DatabaseType { get; set; } = "PostgreSQL";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
