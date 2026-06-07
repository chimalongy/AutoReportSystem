using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    [Table("executions", Schema = "public")]
    public class Execution
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
        [Column("execution_status")]
        public string ExecutionStatus { get; set; } = "running"; // "running", "completed", "failed"

        [Column("execution_logs_path")]
        public string? ExecutionLogsPath { get; set; }

        [Column("execution_result_path")]
        public string? ExecutionResultPath { get; set; }

        // ── Emails Sent ──
        [Column("emails_sent")]
        public string? EmailsSentJson { get; set; } // Stored as JSON array

        // ── Files Sent ──
        [Column("files_sent")]
        public string? FilesSentJson { get; set; } // Stored as JSON array

        [Column("start_time")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [Column("row_count")]
        public int? RowCount { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Helper classes for JSON serialization ──
    public class EmailSentRecord
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "sent", "failed", "pending"
        public string? ErrorMessage { get; set; }
    }

    public class FileSentRecord
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "saved", "failed", "pending"
        public string? ErrorMessage { get; set; }
    }
}