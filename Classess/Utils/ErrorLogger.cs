using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARS.Classess.Utils
{
    /// <summary>
    /// Writes application errors to a per-day JSON file, e.g. ErrorLogs_2026-07-27.json.
    /// Fire-and-forget: this class swallows its own failures so a broken logger
    /// never becomes the cause of a new outage.
    /// </summary>
    public static class ErrorLogger
    {
        // ── Configuration ────────────────────────────────────────────────────
        // Change this to point wherever you want logs stored (e.g. a shared
        // path alongside GlobalVariables.reportsDirectory).
        private static readonly string _logsDirectory =
            Path.Combine(GlobalVariables.rootDirectory, "ErrorLogs");

      

        private static readonly object _fileLock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Log an exception. Pass along whatever request context you have
        /// available (all optional) so entries are easy to trace back.
        /// </summary>
        public static void LogError(
            Exception ex,
            string? source = null,
            string? userEmail = null,
            int? userId = null,
            string? ipAddress = null,
            string? pageUrl = null,
            string? additionalInfo = null)
        {
            
            if (ex is null) return;

            var entry = new ErrorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                InnerExceptionMessage = ex.InnerException?.Message,
                Source = source,
                UserEmail = userEmail,
                UserId = userId,
                IpAddress = ipAddress,
                PageUrl = pageUrl,
                AdditionalInfo = additionalInfo
            };

            WriteEntry(entry);
        }

        /// <summary>
        /// Log a plain message (no exception object available).
        /// </summary>
        public static void LogError(
            string message,
            string? source = null,
            string? userEmail = null,
            int? userId = null,
            string? ipAddress = null,
            string? pageUrl = null,
            string? additionalInfo = null)
        {
            var entry = new ErrorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Source = source,
                UserEmail = userEmail,
                UserId = userId,
                IpAddress = ipAddress,
                PageUrl = pageUrl,
                AdditionalInfo = additionalInfo
            };

            WriteEntry(entry);
        }

        // ── Internals ────────────────────────────────────────────────────────

        private static void WriteEntry(ErrorLogEntry entry)
        {
            try
            {
                lock (_fileLock)
                {
                    Directory.CreateDirectory(_logsDirectory);

                    var fileName = $"ErrorLogs_{DateTime.UtcNow:yyyy-MM-dd}.json";
                    var filePath = Path.Combine(_logsDirectory, fileName);

                    List<ErrorLogEntry> entries;

                    if (File.Exists(filePath))
                    {
                        var existingJson = File.ReadAllText(filePath);
                        entries = string.IsNullOrWhiteSpace(existingJson)
                            ? new List<ErrorLogEntry>()
                            : JsonSerializer.Deserialize<List<ErrorLogEntry>>(existingJson) ?? new List<ErrorLogEntry>();
                    }
                    else
                    {
                        entries = new List<ErrorLogEntry>();
                    }

                    entries.Add(entry);

                    var json = JsonSerializer.Serialize(entries, _jsonOptions);
                    File.WriteAllText(filePath, json);
                }
            }
            catch
            {
                // Intentionally swallowed: logging must never throw and take
                // down the request that triggered it.
            }
        }
    }

    public class ErrorLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
        public string? InnerExceptionMessage { get; set; }
        public string? Source { get; set; }
        public string? UserEmail { get; set; }
        public int? UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? PageUrl { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}