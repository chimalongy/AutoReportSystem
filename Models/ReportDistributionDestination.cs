using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    [Table("report_distribution_destinations")]
    public class ReportDistributionDestination
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("report_id")]
        public int ReportId { get; set; }

        [ForeignKey("ReportId")]
        public Report? Report { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("destination_type")]
        public string DestinationType { get; set; } = "email"; // "email" or "file"

        // For Email destinations
        [MaxLength(500)]
        [Column("email_to")]
        public string? EmailTo { get; set; }

        [MaxLength(500)]
        [Column("email_cc")]
        public string? EmailCc { get; set; }

        [MaxLength(500)]
        [Column("email_bcc")]
        public string? EmailBcc { get; set; }

        [MaxLength(300)]
        [Column("email_subject")]
        public string? EmailSubject { get; set; }

        [Column("email_body")]
        public string? EmailBody { get; set; }

        // For File destinations
        [MaxLength(500)]
        [Column("file_path")]
        public string? FilePath { get; set; }

        // ── Max Rows Per Sheet ──
        [Column("max_rows_per_sheet")]
        public int? MaxRowsPerSheet { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}