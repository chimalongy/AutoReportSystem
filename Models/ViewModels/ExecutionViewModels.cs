namespace ARS.Models.ViewModels
{
    public class CreateExecutionRequest
    {
        public int ReportId { get; set; }
    }

    public class UpdateExecutionRequest
    {
        public string? ExecutionStatus { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ExecutionLogsPath { get; set; }
        public string? ExecutionResultPath { get; set; }
        public List<EmailSentRecord>? EmailsSent { get; set; }
        public List<FileSentRecord>? FilesSent { get; set; }
        public int? RowCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}