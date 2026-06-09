using ARS.Data;
using ARS.Models;
using SpreadCheetah;
using SpreadCheetah.Worksheets;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace ARS.Classess.Utils
{
    public class ReportFetcher
    {
        private readonly AppDbContext _dbContext;

        public ReportFetcher(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public static string BuildExecutionFileName(Report report)
        {
            string safeName = string.Concat(report.Name.Split(System.IO.Path.GetInvalidFileNameChars()))
                                    .Trim()
                                    .ToLowerInvariant()
                                    .Replace(' ', '_');

            DateTime now = DateTime.Now;

            if (report.ExecutionType == "single")
                return safeName;

            if (report.ExecutionType == "scheduled")
            {
                return report.ScheduleFrequency?.ToLowerInvariant() switch
                {
                    "daily" => $"{safeName}_{now:yyyy-MM-dd}",
                    "weekly" => $"{safeName}_{now:yyyy}_W{System.Globalization.ISOWeek.GetWeekOfYear(now):D2}",
                    "monthly" => $"{safeName}_{now:yyyy-MM}",
                    "custom_dates" => $"{safeName}_{now:yyyy-MM-dd}",
                    "custom_recurring" => $"{safeName}_{now:yyyy-MM-dd_HH-mm}",
                    _ => $"{safeName}_{now:yyyy-MM-dd_HH-mm-ss}"
                };
            }

            return $"{safeName}_{now:yyyy-MM-dd_HH-mm-ss}";
        }

        /// <summary>
        /// Main entry point: creates execution record, runs SQL, distributes results, updates status.
        /// </summary>
        public async Task ExecuteReportAsync(Report report)
        {
            string safeName = string.Concat(report.Name.Split(System.IO.Path.GetInvalidFileNameChars()))
                                     .Trim()
                                     .ToLowerInvariant()
                                     .Replace(' ', '_')
                                     .ToUpperInvariant();



            string executionName = BuildExecutionFileName(report);
            string reportFolder = Path.Combine(GlobalVariables.reportsDirectory, safeName);
            string executionPath = Path.Combine(reportFolder, "executions");
            string executionResultPath = Path.Combine(executionPath, "results");
            string logPath = Path.Combine(executionPath, "Logs");
            string logFileName = executionName;

            Directory.CreateDirectory(reportFolder);
            Directory.CreateDirectory(executionPath);
            Directory.CreateDirectory(executionResultPath);
            Directory.CreateDirectory(logPath);
            Logger.LogToFile(logPath, logFileName, $"STEP 0: CREATING EXCEUTION ENVIRONMENT");
            // ══════════════════════════════════════════════════════════════════════
            // STEP 0: CREATE EXECUTION RECORD (status = running)
            // ══════════════════════════════════════════════════════════════════════
            var execution = new Execution
            {
                ReportId = report.Id,
                ExecutionStatus = "running",
                StartTime = DateTime.UtcNow,
                ExecutionLogsPath = Path.Combine(logPath, logFileName)
            };

                 _dbContext.Executions.Add(execution);
                await _dbContext.SaveChangesAsync();
           

            Logger.LogToFile(logPath, logFileName, $"STARTING EXECUTION ID: {execution.Id} FOR {executionName}");
            Logger.LogToFile(logPath, logFileName, $"STEP 1: RUN SQL");

            string resultFilePath = Path.Combine(executionResultPath, executionName +
                (report.OutputFormat == "csv" ? ".csv" : ".xlsx"));

            ExportResult result;
            List<EmailSentRecord> emailsSent = new();
            List<FileSentRecord> filesSent = new();

            try
            {
                DbConnectionConfig dbConfig = await _dbContext.DbConnectionConfigs.FindAsync(report.DbConnectionConfigId);
                string query = report.Query;

                // ═══════════════════════════════════════════════════════════════
                // STEP 1: EXECUTE SQL & EXPORT
                // ═══════════════════════════════════════════════════════════════
                result = report.OutputFormat switch
                {
                    "csv" => await RunSqlCommandToCsvAsync(
                                 dbConfig, query, resultFilePath,
                                 logPath, logFileName, report.MaxRowsPerSheet),

                    "excel" => await RunSqlCommandToExcelAsync(
                                   dbConfig, query, resultFilePath,
                                   logPath, logFileName, report.MaxRowsPerSheet),

                    _ => ExportResult.Fail($"Unsupported output format: {report.OutputFormat}")
                };

                if (!result.Success)
                {
                    throw new Exception(result.ErrorMessage ?? "Export failed");
                }

                Logger.LogToFile(logPath, logFileName,
                    $"STEP 1 [COMPLETED]: {result.TotalRows:N0} rows → {result.FilePath}");

                // ═══════════════════════════════════════════════════════════════
                // STEP 2: SAVE TO FILE (if enabled on report)
                // ═══════════════════════════════════════════════════════════════
                if (report.EnableFileSave && !string.IsNullOrWhiteSpace(report.FileSavePath))
                {
                    Logger.LogToFile(logPath, logFileName, $"STEP 2: SAVING TO FILES");
                    var fileRecord = await SaveToFileAsync(report, result.FilePath, executionResultPath, executionName, logPath, logFileName);
                    filesSent.Add(fileRecord);
                    Logger.LogToFile(logPath, logFileName,
                        $"STEP 2 [COMPLETED]: Saved to {fileRecord.Path} — {fileRecord.Status}");
                }

                // ═══════════════════════════════════════════════════════════════
                // STEP 3: EMAIL DISTRIBUTION (if enabled on report)
                // ═══════════════════════════════════════════════════════════════
                if (report.EnableEmailDistribution)
                {
                    Logger.LogToFile(logPath, logFileName, $"STEP 3: EMAIL DISTRIBUTION");
                    var emailRecords = await DistributeByEmailAsync(report, result.FilePath, executionName, logPath, logFileName);
                    emailsSent.AddRange(emailRecords);
                    Logger.LogToFile(logPath, logFileName,
                        $"STEP 3 [COMPLETED]: {emailRecords.Count(r => r.Status == "sent")}/{emailRecords.Count} emails sent");
                }

                // ═══════════════════════════════════════════════════════════════
                // STEP 4: ADDITIONAL DESTINATIONS
                // ═══════════════════════════════════════════════════════════════
                if (report.DistributionDestinations?.Any() == true)
                {
                    Logger.LogToFile(logPath, logFileName, $"STEP 4: ADDITIONAL DESTINATIONS");
                    foreach (var dest in report.DistributionDestinations.Where(d => d.IsActive))
                    {
                        if (dest.DestinationType == "email")
                        {
                            var emailRecs = await SendToDestinationEmailAsync(dest, result.FilePath, report.OutputFileName, logPath, logFileName);
                            emailsSent.AddRange(emailRecs);
                        }
                        else if (dest.DestinationType == "file")
                        {
                            var fileRec = await SaveToDestinationFileAsync(report, dest, result.FilePath, logPath, logFileName);
                            filesSent.Add(fileRec);
                        }
                    }
                    Logger.LogToFile(logPath, logFileName,
                        $"STEP 4 [COMPLETED]: {emailsSent.Count(r => r.Status == "sent")} emails, {filesSent.Count(r => r.Status == "saved")} files");
                }

                // ═══════════════════════════════════════════════════════════════
                // STEP 5: MARK EXECUTION COMPLETED
                // ═══════════════════════════════════════════════════════════════
                execution.ExecutionStatus = "completed";
                execution.EndTime = DateTime.UtcNow;
                execution.RowCount = (int)result.TotalRows;
                execution.ExecutionResultPath = result.FilePath;
                execution.ExecutionLogsPath = Path.Combine(logPath, logFileName);
                execution.EmailsSentJson = JsonSerializer.Serialize(emailsSent);
                execution.FilesSentJson = JsonSerializer.Serialize(filesSent);

                // Update report metadata
                report.LastRunDate = DateTime.UtcNow;
                report.LastErrorMessage = null;
                if (report.ExecutionType == "single")
                {
                    report.Status = "stopped";
                }

                Logger.LogToFile(logPath, logFileName,
                    $"EXECUTION {execution.Id} COMPLETED — {result.TotalRows:N0} rows");
            }
            catch (Exception ex)
            {
                // ═══════════════════════════════════════════════════════════════
                // FAILURE HANDLER
                // ═══════════════════════════════════════════════════════════════
                execution.ExecutionStatus = "failed";
                execution.EndTime = DateTime.UtcNow;
                execution.ErrorMessage = ex.Message;
                execution.ExecutionLogsPath = Path.Combine(logPath, logFileName);
                execution.EmailsSentJson = JsonSerializer.Serialize(emailsSent);
                execution.FilesSentJson = JsonSerializer.Serialize(filesSent);

                report.LastErrorMessage = ex.Message;

                Logger.LogToFile(logPath, logFileName,
                    $"EXECUTION {execution.Id} FAILED — {ex.Message}");
            }
            finally
            {
                await _dbContext.SaveChangesAsync();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DISTRIBUTION HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private async Task<FileSentRecord> SaveToFileAsync(
      Report report, string sourceFilePath,
      string executionResultPath, string executionName, string logPath, string logFileName)
        {
            var record = new FileSentRecord
            {
                Id = 1,
                Path = report.FileSavePath ?? "",
                Status = "pending"
            };

            string sourceExt = Path.GetExtension(sourceFilePath); // e.g. ".csv" or ".xlsx"
            string dest_filename = Path.HasExtension(report.OutputFileName)
                ? report.OutputFileName
                : report.OutputFileName + sourceExt;

            try
            {
               
                string destPath = Path.Combine(
                    report.FileSavePath!,
                    dest_filename);
                Logger.LogToFile(logPath, logFileName, $"SAVING FILE AT {destPath}");
                string? destDir = Path.GetDirectoryName(destPath);
                if (string.IsNullOrEmpty(destDir))
                    throw new InvalidOperationException(
                        $"Cannot resolve destination directory from path: {destPath}");

                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        Directory.CreateDirectory(destDir);

                        await using var src = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                        await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                        await src.CopyToAsync(dst);

                        record.Path = destPath;
                        record.Status = "saved";
                        break;
                    }
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"Access denied to path '{report.FileSavePath}': {ex.Message}";
                Logger.LogToFile(logPath, logFileName, $"Access denied to path '{report.FileSavePath}': {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"Path not found '{report.FileSavePath}': {ex.Message}";
                Logger.LogToFile(logPath, logFileName, $"Path not found '{report.FileSavePath}': {ex.Message}");
            }
            catch (IOException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"I/O error after retries: {ex.Message}";
                Logger.LogToFile(logPath, logFileName, $"I/O error after retries: {ex.Message}");
            }
            catch (Exception ex)
            {
                record.Status = "failed";
                record.ErrorMessage = ex.Message;
                Logger.LogToFile(logPath, logFileName, ex.Message);
            }

            return record;
        }

        private async Task<List<EmailSentRecord>> DistributeByEmailAsync(Report report,
            string attachmentPath, string executionName, string logPath, string logFileName)
        {
            var records = new List<EmailSentRecord>();
            var recipients = new List<string>();

            if (!string.IsNullOrWhiteSpace(report.EmailToRecipients))
                recipients.AddRange(report.EmailToRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries));

           

            int id = 1;
            foreach (var email in recipients.Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)))
            {
                Logger.LogToFile(logPath, logFileName, $"Distributing to email {email}");
                var record = new EmailSentRecord
                {
                    Id = id++,
                    Email = email,
                    Status = "pending"
                };

                try
                {
                    string subject = (report.EmailSubject ?? "Report: {{reportName}}")
                        .Replace("{{reportName}}", report.Name)
                        .Replace("{{date}}", DateTime.Now.ToString("yyyy-MM-dd"));

                    string body = report.EmailBodyTemplate ?? "Please find the attached report.";

                    body = body + "\\n" + attachmentPath;

                    bool result = await EmailSender.SendEmail(email, subject, body);
                    if (result == false)
                    {
                        record.Status = "failed";
                    }
                    else
                    {
                        record.Status = "sent";
                    }
                }
                catch (Exception ex)
                {
                    record.Status = "failed";
                    record.ErrorMessage = ex.Message;
                }

                records.Add(record);
            }

            return records;
        }

        private async Task<List<EmailSentRecord>> SendToDestinationEmailAsync(
            ReportDistributionDestination dest, string attachmentPath, string executionName, string logPath, string logFileName)
        {
            var records = new List<EmailSentRecord>();
           

            var recipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(dest.EmailTo))
                recipients.AddRange(dest.EmailTo.Split(',', StringSplitOptions.RemoveEmptyEntries));

            int id = 1;
            foreach (var email in recipients.Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)))
            {
                Logger.LogToFile(logPath, logFileName, $"Sending Mail to {email}");
                var record = new EmailSentRecord
                {
                    Id = id++,
                    Email = email,
                    Status = "pending"
                };

                try
                {
                    string body = dest.EmailBody ?? "Please find the attached report.";
                    body = body + "\\n" + attachmentPath;
                    await EmailSender.SendEmail(email,
                        dest.EmailSubject ?? $"Report: {executionName}",
                        body);
                    record.Status = "sent";
                }
                catch (Exception ex)
                {
                    record.Status = "failed";
                    record.ErrorMessage = ex.Message;
                }

                records.Add(record);
            }

            return records;
        }

        private async Task<FileSentRecord> SaveToDestinationFileAsync( Report report,
      ReportDistributionDestination dest, string sourceFilePath, string logpath, string logFileName)
        {
            var record = new FileSentRecord
            {
                Id = 1,
                Path = dest.FilePath ?? "",
                Status = "pending"
            };

            string sourceExt = Path.GetExtension(sourceFilePath); // e.g. ".csv" or ".xlsx"
            string dest_filename = Path.HasExtension(report.OutputFileName)
                ? report.OutputFileName
                : report.OutputFileName + sourceExt;

            try
            {
                string destPath = Path.Combine(dest.FilePath!,
                    Path.GetFileName(dest_filename));

                string? destDir = Path.GetDirectoryName(destPath);
                if (string.IsNullOrEmpty(destDir))
                    Logger.LogToFile(logpath, logFileName, $"Cannot resolve destination directory from path: {destPath}");

                // Retry loop handles transient network blips
                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    Logger.LogToFile(logpath, logFileName, $"Attempt {attempt} to save file to destination: {destPath}");
                    try
                    {
                        Directory.CreateDirectory(destDir);

                        // Stream copy avoids the 2 GB limit on File.Copy over some
                        // SMB shares and gives you a progress hook if needed later.
                        await using var src = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                        await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                        await src.CopyToAsync(dst);

                        record.Path = destPath;
                        record.Status = "saved";
                        break; // success — exit retry loop
                    }
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        // Brief back-off before retrying (handles momentary share
                        // unavailability, DFS referral delays, etc.)
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"Access denied to network path '{dest.FilePath}': {ex.Message}";
            }
            catch (DirectoryNotFoundException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"Network path not found '{dest.FilePath}': {ex.Message}";
            }
            catch (IOException ex)
            {
                record.Status = "failed";
                record.ErrorMessage = $"Network I/O error after retries: {ex.Message}";
            }
            catch (Exception ex)
            {
                record.Status = "failed";
                record.ErrorMessage = ex.Message;
            }

            return record;
        }
        // ══════════════════════════════════════════════════════════════════════
        // EXCEL EXPORT
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ExportResult> RunSqlCommandToExcelAsync(
            DbConnectionConfig dbConfig,
            string query,
            string outputFilePath,
            string logFolder,
            string logFileName,
            int? maxRowsPerSheet,
            int maxRetries = 6)
        {
            int? MAX_ROWS_PER_SHEET = maxRowsPerSheet ?? 1_048_576;
            const int FETCH_PAGE_SIZE = 100_000;
            const int baseDelayMs = 2000;
            const int maxDelayMs = 30_000;

            string connectionString = Cryptor.Decrypt(dbConfig.EncryptedConnectionString, true);
            bool isOracle = dbConfig.DatabaseType.Equals("oracle", StringComparison.OrdinalIgnoreCase);
            bool isPostgres = dbConfig.DatabaseType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);

            string pagedQuery = isOracle
                ? $"SELECT * FROM ({query}) t ORDER BY 1 OFFSET :offset ROWS FETCH NEXT :fetchSize ROWS ONLY"
                : $"SELECT * FROM ({query}) t ORDER BY 1 OFFSET @offset ROWS FETCH NEXT @fetchSize ROWS ONLY";

            string tempFilePath = outputFilePath + ".tmp";

            long totalRowsWritten = 0;
            int sheetNumber = 1;
            int rowsInCurrentSheet = 0;
            bool headersWritten = false;
            int columnCount = 0;
            DataCell[]? rowBuffer = null;

            FileStream? fileStream = null;
            Spreadsheet? spreadsheet = null;

            try
            {
                fileStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1 << 16,
                    useAsync: true);

                spreadsheet = await Spreadsheet.CreateNewAsync(fileStream, new SpreadCheetahOptions
                {
                    CompressionLevel = SpreadCheetahCompressionLevel.Optimal
                });

                while (true)
                {
                    int attempt = 0;
                    bool pageCompleted = false;
                    long rowsThisPage = 0;

                    while (!pageCompleted)
                    {
                        attempt++;

                        try
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Fetching page at offset {totalRowsWritten:N0} " +
                                $"(attempt {attempt}, page size {FETCH_PAGE_SIZE:N0})...");

                            await using DbConnection connection = isPostgres
                                ? new NpgsqlConnection(connectionString)
                                : new OracleConnection(connectionString);

                            await connection.OpenAsync();

                            await using DbCommand command = connection.CreateCommand();
                            command.CommandText = pagedQuery;
                            command.CommandTimeout = 600;

                            DbParameter offsetParam = command.CreateParameter();
                            offsetParam.ParameterName = "offset";
                            offsetParam.Value = totalRowsWritten;
                            command.Parameters.Add(offsetParam);

                            DbParameter fetchParam = command.CreateParameter();
                            fetchParam.ParameterName = "fetchSize";
                            fetchParam.Value = FETCH_PAGE_SIZE;
                            command.Parameters.Add(fetchParam);

                            await using DbDataReader reader = await command.ExecuteReaderAsync(
                                System.Data.CommandBehavior.SequentialAccess);

                            if (!headersWritten)
                            {
                                columnCount = reader.FieldCount;
                                rowBuffer = new DataCell[columnCount];
                                await StartNewSheetAsync(spreadsheet, reader, sheetNumber, columnCount);
                                headersWritten = true;
                            }

                            rowsThisPage = 0;

                            while (await reader.ReadAsync())
                            {
                                if (rowsInCurrentSheet >= MAX_ROWS_PER_SHEET)
                                {
                                    sheetNumber++;
                                    rowsInCurrentSheet = 0;
                                    await StartNewSheetAsync(spreadsheet, reader, sheetNumber, columnCount);
                                }

                                FillRowBuffer(reader, rowBuffer!, columnCount);
                                await spreadsheet.AddRowAsync(rowBuffer!);

                                rowsInCurrentSheet++;
                                totalRowsWritten++;
                                rowsThisPage++;

                                if (totalRowsWritten % 100_000 == 0)
                                {
                                    Logger.LogToFile(logFolder, logFileName,
                                        $"      Written {totalRowsWritten:N0} rows total...");
                                }
                            }

                            pageCompleted = true;

                            Logger.LogToFile(logFolder, logFileName,
                                $"      Page done. {rowsThisPage:N0} rows fetched. " +
                                $"Total written: {totalRowsWritten:N0}");
                        }
                        catch (NotSupportedException ex)
                        {
                            return ExportResult.Fail($"Unsupported operation: {ex.Message}");
                        }
                        catch (PostgresException ex) when (ex.SqlState == "28P01")
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      PostgreSQL authentication failed: {ex.Message}");
                            return ExportResult.Fail($"PostgreSQL authentication failed: {ex.Message}");
                        }
                        catch (OracleException ex) when (ex.Number == 1017)
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Oracle authentication failed: {ex.Message}");
                            return ExportResult.Fail($"Oracle authentication failed: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            if (attempt >= maxRetries)
                            {
                                Logger.LogToFile(logFolder, logFileName,
                                    $"      Giving up after {attempt} attempts at offset " +
                                    $"{totalRowsWritten:N0}. Error: {ex.Message}");
                                return ExportResult.Fail($"Failed after {attempt} attempts at offset {totalRowsWritten:N0}: {ex.Message}");
                            }

                            int delay = Math.Min(baseDelayMs * (int)Math.Pow(2, attempt - 1), maxDelayMs);
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Network error at offset {totalRowsWritten:N0}: {ex.Message}. " +
                                $"Resuming from that offset in {delay / 1000}s...");

                            await Task.Delay(delay);
                        }
                    }

                    if (rowsThisPage == 0)
                    {
                        Logger.LogToFile(logFolder, logFileName,
                            $"      All rows fetched. Total: {totalRowsWritten:N0} rows " +
                            $"across {sheetNumber} sheet(s).");
                        break;
                    }
                }

                await spreadsheet.FinishAsync();
                await fileStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Logger.LogToFile(logFolder, logFileName,
                    $"      Export failed. Partial temp file at: {tempFilePath}");
                return ExportResult.Fail(ex.Message);
            }
            finally
            {
                if (spreadsheet != null) await spreadsheet.DisposeAsync();
                if (fileStream != null) await fileStream.DisposeAsync();
            }

            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);

            File.Move(tempFilePath, outputFilePath);

            Logger.LogToFile(logFolder, logFileName,
                $"      STEP 1 [COMPLETED]: Exported {totalRowsWritten:N0} rows " +
                $"across {sheetNumber} sheet(s) → {outputFilePath}");

            return ExportResult.Ok(outputFilePath, totalRowsWritten, sheetNumber);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CSV EXPORT
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ExportResult> RunSqlCommandToCsvAsync(
            DbConnectionConfig dbConfig,
            string query,
            string outputFilePath,
            string logFolder,
            string logFileName,
            int? maxRowsPerSheet,
            int maxRetries = 6)
        {
            const int FETCH_PAGE_SIZE = 10_000;
            const int baseDelayMs = 2000;
            const int maxDelayMs = 30_000;

            string connectionString = Cryptor.Decrypt(dbConfig.EncryptedConnectionString, true);
            bool isOracle = dbConfig.DatabaseType.Equals("oracle", StringComparison.OrdinalIgnoreCase);
            bool isPostgres = dbConfig.DatabaseType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);

            string pagedQuery = isOracle
                ? $"SELECT * FROM ({query}) t ORDER BY 1 OFFSET :offset ROWS FETCH NEXT :fetchSize ROWS ONLY"
                : $"SELECT * FROM ({query}) t ORDER BY 1 OFFSET @offset ROWS FETCH NEXT @fetchSize ROWS ONLY";

            string tempFilePath = outputFilePath + ".tmp";

            long totalRowsWritten = 0;
            bool headersWritten = false;
            int columnCount = 0;

            StreamWriter? writer = null;

            try
            {
                writer = new StreamWriter(tempFilePath, false, Encoding.UTF8, bufferSize: 1 << 16);

                while (true)
                {
                    int attempt = 0;
                    bool pageCompleted = false;
                    long rowsThisPage = 0;

                    while (!pageCompleted)
                    {
                        attempt++;

                        try
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Fetching page at offset {totalRowsWritten:N0} " +
                                $"(attempt {attempt}, page size {FETCH_PAGE_SIZE:N0})...");

                            await using DbConnection connection = isPostgres
                                ? new NpgsqlConnection(connectionString)
                                : new OracleConnection(connectionString);

                            await connection.OpenAsync();

                            await using DbCommand command = connection.CreateCommand();
                            command.CommandText = pagedQuery;
                            command.CommandTimeout = 600;

                            DbParameter offsetParam = command.CreateParameter();
                            offsetParam.ParameterName = "offset";
                            offsetParam.Value = totalRowsWritten;
                            command.Parameters.Add(offsetParam);

                            DbParameter fetchParam = command.CreateParameter();
                            fetchParam.ParameterName = "fetchSize";
                            fetchParam.Value = FETCH_PAGE_SIZE;
                            command.Parameters.Add(fetchParam);

                            await using DbDataReader reader = await command.ExecuteReaderAsync(
                                System.Data.CommandBehavior.SequentialAccess);

                            if (!headersWritten)
                            {
                                columnCount = reader.FieldCount;
                                var headers = Enumerable.Range(0, columnCount)
                                    .Select(i => EscapeCsv(reader.GetName(i)));
                                await writer.WriteLineAsync(string.Join(",", headers));
                                headersWritten = true;
                            }

                            rowsThisPage = 0;

                            while (await reader.ReadAsync())
                            {
                                var fields = Enumerable.Range(0, columnCount)
                                    .Select(i => reader.IsDBNull(i)
                                        ? ""
                                        : EscapeCsv(reader.GetValue(i)?.ToString() ?? ""));

                                await writer.WriteLineAsync(string.Join(",", fields));

                                totalRowsWritten++;
                                rowsThisPage++;

                                if (totalRowsWritten % 100_000 == 0)
                                {
                                    Logger.LogToFile(logFolder, logFileName,
                                        $"      Written {totalRowsWritten:N0} rows total...");
                                }
                            }

                            pageCompleted = true;

                            Logger.LogToFile(logFolder, logFileName,
                                $"      Page done. {rowsThisPage:N0} rows fetched. " +
                                $"Total written: {totalRowsWritten:N0}");
                        }
                        catch (NotSupportedException ex)
                        {
                            return ExportResult.Fail($"Unsupported operation: {ex.Message}");
                        }
                        catch (PostgresException ex) when (ex.SqlState == "28P01")
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      PostgreSQL authentication failed: {ex.Message}");
                            return ExportResult.Fail($"PostgreSQL authentication failed: {ex.Message}");
                        }
                        catch (OracleException ex) when (ex.Number == 1017)
                        {
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Oracle authentication failed: {ex.Message}");
                            return ExportResult.Fail($"Oracle authentication failed: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            if (attempt >= maxRetries)
                            {
                                Logger.LogToFile(logFolder, logFileName,
                                    $"      Giving up after {attempt} attempts at offset " +
                                    $"{totalRowsWritten:N0}. Error: {ex.Message}");
                                return ExportResult.Fail($"Failed after {attempt} attempts at offset {totalRowsWritten:N0}: {ex.Message}");
                            }

                            int delay = Math.Min(baseDelayMs * (int)Math.Pow(2, attempt - 1), maxDelayMs);
                            Logger.LogToFile(logFolder, logFileName,
                                $"      Network error at offset {totalRowsWritten:N0}: {ex.Message}. " +
                                $"Resuming from that offset in {delay / 1000}s...");

                            await Task.Delay(delay);
                        }
                    }

                    if (rowsThisPage == 0)
                    {
                        Logger.LogToFile(logFolder, logFileName,
                            $"      All rows fetched. Total: {totalRowsWritten:N0} rows.");
                        break;
                    }
                }

                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                Logger.LogToFile(logFolder, logFileName,
                    $"      CSV export failed. Partial temp file at: {tempFilePath}");
                return ExportResult.Fail(ex.Message);
            }
            finally
            {
                if (writer != null) await writer.DisposeAsync();
            }

            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);

            File.Move(tempFilePath, outputFilePath);

            Logger.LogToFile(logFolder, logFileName,
                $"      STEP 1 [COMPLETED]: Exported {totalRowsWritten:N0} rows → {outputFilePath}");

            return ExportResult.Ok(outputFilePath, totalRowsWritten);
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static async Task StartNewSheetAsync(
            Spreadsheet spreadsheet,
            DbDataReader reader,
            int sheetNumber,
            int columnCount)
        {
            await spreadsheet.StartWorksheetAsync($"Sheet{sheetNumber}", new WorksheetOptions());

            var headerRow = new DataCell[columnCount];
            for (int i = 0; i < columnCount; i++)
                headerRow[i] = new DataCell(reader.GetName(i));

            await spreadsheet.AddRowAsync(headerRow);
        }

        private static void FillRowBuffer(DbDataReader reader, DataCell[] buffer, int columnCount)
        {
            for (int i = 0; i < columnCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    buffer[i] = new DataCell();
                    continue;
                }

                buffer[i] = reader.GetFieldType(i) switch
                {
                    Type t when t == typeof(int) => new DataCell(reader.GetInt32(i)),
                    Type t when t == typeof(long) => new DataCell(reader.GetInt64(i)),
                    Type t when t == typeof(double) => new DataCell(reader.GetDouble(i)),
                    Type t when t == typeof(float) => new DataCell(reader.GetFloat(i)),
                    Type t when t == typeof(decimal) => new DataCell((double)reader.GetDecimal(i)),
                    Type t when t == typeof(bool) => new DataCell(reader.GetBoolean(i)),
                    Type t when t == typeof(DateTime) => new DataCell(reader.GetDateTime(i).ToString("yyyy-MM-dd HH:mm:ss")),
                    _ => new DataCell(reader.GetValue(i)?.ToString() ?? "")
                };
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // EXPORT RESULT
    // ══════════════════════════════════════════════════════════════════════
    public class ExportResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FilePath { get; set; }
        public long TotalRows { get; set; }
        public int SheetCount { get; set; }

        public static ExportResult Ok(string filePath, long totalRows, int sheetCount = 1)
            => new() { Success = true, FilePath = filePath, TotalRows = totalRows, SheetCount = sheetCount };

        public static ExportResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }

    // ══════════════════════════════════════════════════════════════════════
    // EMAIL SERVICE (stub — replace with your implementation)
    // ══════════════════════════════════════════════════════════════════════
    public class EmailService
    {
        public async Task SendAsync(string to, string subject, string body, string? attachmentPath = null)
        {
            // TODO: Implement with your SMTP/sendgrid/etc configuration
            // Example:
            // var message = new MimeMessage();
            // message.To.Add(MailboxAddress.Parse(to));
            // message.Subject = subject;
            // ...
            await Task.CompletedTask;
        }
    }
}