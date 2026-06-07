using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    /// <summary>
    /// Stores database connection configurations.
    /// NOTE: Password is NEVER stored here. It is only used to build
    /// the connection string, which is then encrypted and stored.
    /// </summary>
    [Table("db_connection_configs", Schema = "public")]
    public class DbConnectionConfig
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("database_type")]
        public string DatabaseType { get; set; } = "PostgreSQL";

        [Required]
        [Column("host")]
        public string Host { get; set; } = string.Empty;

        [Column("port")]
        public int Port { get; set; }

        [Required]
        [Column("database_name")]
        public string DatabaseName { get; set; } = string.Empty;

        [Required]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted connection string. The password is embedded within
        /// this encrypted string and never stored in plain text.
        /// </summary>
        [Required]
        [Column("encrypted_connection_string")]
        public string EncryptedConnectionString { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
