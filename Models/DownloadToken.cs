using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    [Table("download_tokens")]
    public class DownloadToken
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column("execution_id")]
        public int ExecutionId { get; set; }

        [ForeignKey("ExecutionId")]
        public Execution? Execution { get; set; }

        [Required]
        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
