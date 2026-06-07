using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    [Table("reports", Schema = "public")]
    public class Report
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("db_connection_config_id")]
        public int DbConnectionConfigId { get; set; }

        [ForeignKey("DbConnectionConfigId")]
        public DbConnectionConfig? DbConnectionConfig { get; set; }

        [Required]
        [Column("query")]
        public string Query { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("output_file_name")]
        public string OutputFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        [Column("output_format")]
        public string OutputFormat { get; set; } = "csv";

        // ── Automation Configuration ──
        [Required]
        [MaxLength(20)]
        [Column("execution_type")]
        public string ExecutionType { get; set; } = "single";

        // Single Run
        [MaxLength(20)]
        [Column("single_run_timing")]
        public string? SingleRunTiming { get; set; }

        [Column("single_run_date_time")]
        public DateTime? SingleRunDateTime { get; set; }

        // Scheduled Run
        [MaxLength(20)]
        [Column("schedule_frequency")]
        public string? ScheduleFrequency { get; set; }

        [Column("schedule_days_of_week")]
        public string? ScheduleDaysOfWeek { get; set; }

        [Column("schedule_day_of_month")]
        public int? ScheduleDayOfMonth { get; set; }

        [Column("schedule_custom_dates")]
        public string? ScheduleCustomDates { get; set; }

        [Column("schedule_custom_recurring")]
        public string? ScheduleCustomRecurring { get; set; }

        [MaxLength(10)]
        [Column("schedule_time")]
        public string? ScheduleTime { get; set; }        // <-- CHANGED: TimeSpan? → string?

        // ── Status & Tracking ──
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("last_run_date")]
        public DateTime? LastRunDate { get; set; }

        [Column("next_run_date")]
        public DateTime? NextRunDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by_user_id")]
        public int CreatedByUserId { get; set; }

        [MaxLength(500)]
        [Column("last_error_message")]
        public string? LastErrorMessage { get; set; }




        // ── Distribution Configuration ──
        [Column("enable_email_distribution")]
        public bool EnableEmailDistribution { get; set; } = false;

        [MaxLength(500)]
        [Column("email_to_recipients")]
        public string? EmailToRecipients { get; set; }

        [MaxLength(500)]
        [Column("email_cc_recipients")]
        public string? EmailCcRecipients { get; set; }

        [MaxLength(500)]
        [Column("email_bcc_recipients")]
        public string? EmailBccRecipients { get; set; }

        [MaxLength(300)]
        [Column("email_subject")]
        public string? EmailSubject { get; set; }

        [Column("email_body_template")]
        public string? EmailBodyTemplate { get; set; }

        [Column("enable_file_save")]
        public bool EnableFileSave { get; set; } = false;

        [MaxLength(500)]
        [Column("file_save_path")]
        public string? FileSavePath { get; set; }

        // ── Global Excel Configuration ──
        [Column("max_rows_per_sheet")]
        public int? MaxRowsPerSheet { get; set; }

        // Navigation: multiple delivery destinations
        public ICollection<ReportDistributionDestination> DistributionDestinations { get; set; } = new List<ReportDistributionDestination>();








    }
}