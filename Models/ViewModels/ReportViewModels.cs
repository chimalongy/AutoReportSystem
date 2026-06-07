namespace ARS.Models.ViewModels
{
    public class CreateReportRequest
    {
        public string Name { get; set; } = string.Empty;
        public int DbConnectionConfigId { get; set; }
        public string Query { get; set; } = string.Empty;
        public string OutputFileName { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = "csv";
        public string ExecutionType { get; set; } = "single";
        public string? SingleRunTiming { get; set; }
        public DateTime? SingleRunDateTime { get; set; }
        public string? ScheduleFrequency { get; set; }
        public List<string>? ScheduleDaysOfWeek { get; set; }
        public int? ScheduleDayOfMonth { get; set; }
        public List<string>? ScheduleCustomDates { get; set; }        // <-- CHANGED: DateTime → string
        public List<CustomRecurringSchedule>? ScheduleCustomRecurring { get; set; }
        public string? ScheduleTime { get; set; }                      // <-- CHANGED: TimeSpan → string

        // ── Distribution ──
        public bool EnableEmailDistribution { get; set; } = false;
        public string? EmailToRecipients { get; set; }
        public string? EmailCcRecipients { get; set; }
        public string? EmailBccRecipients { get; set; }
        public string? EmailSubject { get; set; }
        public string? EmailBodyTemplate { get; set; }
        public bool EnableFileSave { get; set; } = false;
        public string? FileSavePath { get; set; }
        public int? MaxRowsPerSheet { get; set; }
        public List<DistributionDestinationRequest>? DistributionDestinations { get; set; }
    }

    public class UpdateReportRequest
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? DbConnectionConfigId { get; set; }
        public string? Query { get; set; }
        public string? OutputFileName { get; set; }
        public string? OutputFormat { get; set; }
        public string? ExecutionType { get; set; }
        public string? SingleRunTiming { get; set; }
        public DateTime? SingleRunDateTime { get; set; }
        public string? ScheduleFrequency { get; set; }
        public List<string>? ScheduleDaysOfWeek { get; set; }
        public int? ScheduleDayOfMonth { get; set; }
        public List<string>? ScheduleCustomDates { get; set; }        // <-- CHANGED: DateTime → string
        public List<CustomRecurringSchedule>? ScheduleCustomRecurring { get; set; }
        public string? ScheduleTime { get; set; }                      // <-- CHANGED: TimeSpan → string

        // ── Distribution ──
        public bool? EnableEmailDistribution { get; set; }
        public string? EmailToRecipients { get; set; }
        public string? EmailCcRecipients { get; set; }
        public string? EmailBccRecipients { get; set; }
        public string? EmailSubject { get; set; }
        public string? EmailBodyTemplate { get; set; }
        public bool? EnableFileSave { get; set; }
        public string? FileSavePath { get; set; }
        public int? MaxRowsPerSheet { get; set; }
        public List<DistributionDestinationRequest>? DistributionDestinations { get; set; }
    }

    public class UpdateReportStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class CustomRecurringSchedule
    {
        public string Date { get; set; } = string.Empty;              // <-- CHANGED: DateTime → string
        public string Time { get; set; } = string.Empty;              // <-- CHANGED: TimeSpan → string
    }


    public class DistributionDestinationRequest
    {
        public int? Id { get; set; }
        public string DestinationType { get; set; } = "email";
        public string? EmailTo { get; set; }
        public string? EmailCc { get; set; }
        public string? EmailBcc { get; set; }
        public string? EmailSubject { get; set; }
        public string? EmailBody { get; set; }
        public string? FilePath { get; set; }

        

        public bool IsActive { get; set; } = true;
    }



}