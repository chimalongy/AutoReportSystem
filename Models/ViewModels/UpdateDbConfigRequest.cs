namespace ARS.Models.ViewModels
{
    /// <summary>
    /// Request to update an existing database configuration.
    /// All fields are optional. If Password is provided, a new
    /// encrypted connection string will be generated.
    /// The password itself is never stored.
    /// </summary>
    public class UpdateDbConfigRequest
    {
        public int Id { get; set; }
        public string? DatabaseType { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
