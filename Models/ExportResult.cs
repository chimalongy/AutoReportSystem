namespace ARS.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Result type
    // ─────────────────────────────────────────────────────────────────────────
    public class ExportResult
    {
        public bool Success { get; init; }
        public string? FilePath { get; init; }
        public long TotalRows { get; init; }
        public int TotalSheets { get; init; }  // Excel only, 0 for CSV
        public string? ErrorMessage { get; init; }

        public static ExportResult Ok(string filePath, long totalRows, int totalSheets = 0) => new()
        {
            Success = true,
            FilePath = filePath,
            TotalRows = totalRows,
            TotalSheets = totalSheets
        };

        public static ExportResult Fail(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
