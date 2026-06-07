using ARS.Classess.Utils;
using ARS.Data;
using ARS.Models;
using ARS.Models.ViewModels;
using ARS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ARS.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ReportSchedulerService _reportScheduler;

        public DashboardController(AppDbContext db, IConfiguration config, ReportSchedulerService reportScheduler)
        {
            _db = db;
            _config = config;
            _reportScheduler = reportScheduler;
        }

        // ══════════════════════════════════════════════════════════════════════
        // VIEWS
        // ══════════════════════════════════════════════════════════════════════

        public IActionResult Index()
        {
            return View("~/Views/Dashboard/Index.cshtml");
        }

        [Route("Dashboard/Settings/Users")]
        [Authorize(Roles = "Super Admin,Admin")]
        public IActionResult Users()
        {
            return View("~/Views/Dashboard/Settings/Users.cshtml");
        }

        [Route("Dashboard/Settings/AuditLogs")]
        [Authorize(Roles = "Super Admin,Admin")]
        public IActionResult AuditLogs()
        {
            return View("~/Views/Dashboard/Settings/AuditLogs.cshtml");
        }

        [Route("Dashboard/Databases")]
        [Authorize(Roles = "Super Admin,Admin")]
        public IActionResult Databases()
        {
            return View("~/Views/Dashboard/Databases.cshtml");
        }

        // ══════════════════════════════════════════════════════════════════════
        // USERS API
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("Dashboard/Settings/Users/GetAll")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UsersGetAll()
        {
            var users = await GlobalFunctions.GetAllUsersAsync(_db);
            return Json(users);
        }

        [HttpPost]
        [Route("Dashboard/Settings/Users/Create")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UsersCreate([FromBody] CreateUserRequest req)
        {
            // ── Authorization check: Support users cannot create users ──────────
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // ── Only Super Admin can create Admin users ─────────────────────────
            if (string.Equals(req.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !currentRole.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only Super Admin can create Admin users." });
            }

            // ── Validate role ───────────────────────────────────────────────────
            var validRoles = new[] { "Admin", "Support" };
            if (!validRoles.Contains(req.Role ?? "Support"))
                return BadRequest(new { message = "Role must be 'Admin' or 'Support'." });

            var (result, error) = await GlobalFunctions.CreateUserAsync(
                _db, _config,
                req.FirstName, req.LastName, req.Email,
                req.Department, req.Role);

            if (error == "CONFLICT")
                return Conflict(new { message = "A user with this email already exists." });

            if (error is not null)
                return BadRequest(new { message = error });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);

            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - CREATED USER WITH EMAIL - {req.Email}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(result);
        }

        [HttpPost]
        [Route("Dashboard/Settings/Users/Update/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UsersUpdate(int id, [FromBody] UpdateUserRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // Only Super Admin can change role to Admin
            if (!string.IsNullOrWhiteSpace(req.Role) &&
                req.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                !currentRole.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only Super Admin can assign Admin role." });
            }

            var (result, error) = await GlobalFunctions.UpdateUserAsync(
                _db, id,
                req.FirstName, req.LastName, req.Email,
                req.Department, req.Role);

            if (error == "NOT_FOUND")
                return NotFound(new { message = "User not found." });

            if (error == "CONFLICT")
                return Conflict(new { message = "A user with this email already exists." });

            if (error is not null)
                return BadRequest(new { message = error });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);

            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED USER WITH ID: {id}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(result);
        }

        [HttpPost]
        [Route("Dashboard/Settings/Users/Delete/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UsersDelete(int id)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // Prevent users from deleting themselves
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (id == currentUserId)
                return BadRequest(new { message = "You cannot delete your own account." });

            // Only Super Admin can delete Admin users
            var targetUser = await _db.AppUsers.FindAsync(id);
            if (targetUser?.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true &&
                !currentRole.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only Super Admin can delete Admin users." });
            }

            var error = await GlobalFunctions.DeleteUserAsync(_db, id);

            if (error == "NOT_FOUND")
                return NotFound(new { message = "User not found." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);

            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - DELETED USER WITH USER ID: {id}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(new { message = "User deleted." });
        }

        [HttpPost]
        [Route("Dashboard/Settings/Users/UpdateStatus/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UsersUpdateStatus(int id, [FromBody] UpdateStatusRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // Prevent users from disabling themselves
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (id == currentUserId)
                return BadRequest(new { message = "You cannot change your own status." });

            var (result, error) = await GlobalFunctions.UpdateUserStatusAsync(_db, id, req.ProfileStatus);

            if (error == "NOT_FOUND")
                return NotFound(new { message = "User not found." });

            if (error is not null)
                return BadRequest(new { message = error });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);

            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED USER WITH USER ID: {id} - STATUS TO {req.ProfileStatus}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(result);
        }

        // ══════════════════════════════════════════════════════════════════════
        // AUDIT LOGS API
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("Dashboard/Settings/AuditLogs/GetAll")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> AuditLogsGetAll()
        {
            var logs = await GlobalFunctions.GetAllAuditLogsAsync(_db);
            return Json(logs);
        }

        // ══════════════════════════════════════════════════════════════════════
        // DATABASE CONFIGURATIONS API
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("Dashboard/Databases/GetAll")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigsGetAll()
        {
            var configs = await _db.DbConnectionConfigs
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.DatabaseType,
                    c.Host,
                    c.Port,
                    c.DatabaseName,
                    c.Username,
                    c.Status,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync();

            return Json(configs);
        }

        [HttpPost]
        [Route("Dashboard/Databases/Create")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigCreate([FromBody] CreateDbConfigRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // ── Validate ──────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(req.Host) ||
                string.IsNullOrWhiteSpace(req.DatabaseName) ||
                string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Host, database name, username, and password are all required." });
            }

            var validTypes = new[] { "postgresql", "postgres", "oracle" };
            if (!validTypes.Contains(req.DatabaseType?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Database type must be PostgreSQL or Oracle." });

            if (req.Port <= 0 || req.Port > 65535)
                return BadRequest(new { message = "Port must be between 1 and 65535." });

            // ── Build encrypted connection string ─────────────────────────────
            string encryptedConnectionString;
            try
            {
                encryptedConnectionString = ConnectionStringBuilder.BuildEncryptedConnectionString(
                    req.DatabaseType!, req.Host, req.Port, req.DatabaseName, req.Username, req.Password);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Failed to build connection string: {ex.Message}" });
            }

            // ── Normalize database type to consistent values ──────────────────
            var normalizedType = req.DatabaseType!.ToLowerInvariant() switch
            {
                "postgres" => "PostgreSQL",
                "postgresql" => "PostgreSQL",
                "oracle" => "Oracle",
                _ => req.DatabaseType!
            };

            // ── Save ──────────────────────────────────────────────────────────
            var config = new DbConnectionConfig
            {
                DatabaseType = normalizedType,
                Host = req.Host.Trim(),
                Port = req.Port,
                DatabaseName = req.DatabaseName.Trim(),
                Username = req.Username.Trim(),
                EncryptedConnectionString = encryptedConnectionString,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };

            _db.DbConnectionConfigs.Add(config);
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - CREATED DATABASE CONFIG '{config.DatabaseName}' ON {config.Host}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(new
            {
                config.Id,
                config.DatabaseType,
                config.Host,
                config.Port,
                config.DatabaseName,
                config.Username,
                config.Status,
                config.CreatedAt,
                config.UpdatedAt
            });
        }

        [HttpPost]
        [Route("Dashboard/Databases/Update/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigUpdate(int id, [FromBody] UpdateDbConfigRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var config = await _db.DbConnectionConfigs.FindAsync(id);
            if (config is null)
                return NotFound(new { message = "Database configuration not found." });

            // ── Update fields ─────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(req.DatabaseType))
            {
                var validTypes = new[] { "postgresql", "postgres", "oracle" };
                if (!validTypes.Contains(req.DatabaseType.ToLowerInvariant()))
                    return BadRequest(new { message = "Database type must be PostgreSQL or Oracle." });

                config.DatabaseType = req.DatabaseType.ToLowerInvariant() switch
                {
                    "postgres" => "PostgreSQL",
                    "postgresql" => "PostgreSQL",
                    "oracle" => "Oracle",
                    _ => req.DatabaseType
                };
            }

            if (!string.IsNullOrWhiteSpace(req.Host))
                config.Host = req.Host.Trim();

            if (req.Port.HasValue)
            {
                if (req.Port.Value <= 0 || req.Port.Value > 65535)
                    return BadRequest(new { message = "Port must be between 1 and 65535." });
                config.Port = req.Port.Value;
            }

            if (!string.IsNullOrWhiteSpace(req.DatabaseName))
                config.DatabaseName = req.DatabaseName.Trim();

            if (!string.IsNullOrWhiteSpace(req.Username))
                config.Username = req.Username.Trim();

            // ── Rebuild connection string if password provided ────────────────
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                try
                {
                    config.EncryptedConnectionString = ConnectionStringBuilder.BuildEncryptedConnectionString(
                        config.DatabaseType, config.Host, config.Port, config.DatabaseName, config.Username, req.Password);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = $"Failed to rebuild connection string: {ex.Message}" });
                }
            }

            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED DATABASE CONFIG ID: {id} - '{config.DatabaseName}'",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(new
            {
                config.Id,
                config.DatabaseType,
                config.Host,
                config.Port,
                config.DatabaseName,
                config.Username,
                config.Status,
                config.CreatedAt,
                config.UpdatedAt
            });
        }

        [HttpPost]
        [Route("Dashboard/Databases/Delete/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigDelete(int id)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var config = await _db.DbConnectionConfigs.FindAsync(id);
            if (config is null)
                return NotFound(new { message = "Database configuration not found." });

            _db.DbConnectionConfigs.Remove(config);
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - DELETED DATABASE CONFIG ID: {id} - '{config.DatabaseName}'",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(new { message = "Database configuration deleted." });
        }

        [HttpPost]
        [Route("Dashboard/Databases/UpdateStatus/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigUpdateStatus(int id, [FromBody] UpdateDbStatusRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var allowed = new[] { "active", "inactive" };
            if (!allowed.Contains(req.Status?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Status must be 'active' or 'inactive'." });

            var config = await _db.DbConnectionConfigs.FindAsync(id);
            if (config is null)
                return NotFound(new { message = "Database configuration not found." });

            config.Status = req.Status!.ToLowerInvariant();
            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED DATABASE CONFIG ID: {id} STATUS TO {config.Status}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            return Json(new { config.Id, config.Status });
        }

        [HttpPost]
        [Route("Dashboard/Databases/TestConnection")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigTestConnection([FromBody] TestConnectionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Host) ||
                string.IsNullOrWhiteSpace(req.DatabaseName) ||
                string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Host, database name, username, and password are all required to test a connection." });
            }

            var validTypes = new[] { "postgresql", "postgres", "oracle" };
            if (!validTypes.Contains(req.DatabaseType?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Database type must be PostgreSQL or Oracle." });

            if (req.Port <= 0 || req.Port > 65535)
                return BadRequest(new { message = "Port must be between 1 and 65535." });

            var (success, error) = await ConnectionStringBuilder.TestConnectionAsync(
                req.DatabaseType!, req.Host, req.Port, req.DatabaseName, req.Username, req.Password);

            if (success)
                return Json(new { success = true, message = "Connection successful! Database is reachable." });

            return BadRequest(new { success = false, message = error ?? "Connection failed." });
        }

        [HttpPost]
        [Route("Dashboard/Databases/TestExisting/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> DbConfigTestExisting(int id)
        {
            var config = await _db.DbConnectionConfigs.FindAsync(id);
            if (config is null)
                return NotFound(new { message = "Database configuration not found." });

            var (success, error) = await ConnectionStringBuilder.TestEncryptedConnectionAsync(config.EncryptedConnectionString);

            if (success)
                return Json(new { success = true, message = "Connection successful! Database is reachable." });

            return BadRequest(new { success = false, message = error ?? "Connection failed." });
        }





        // ══════════════════════════════════════════════════════════════════════
        // REPORTS VIEWS
        // ══════════════════════════════════════════════════════════════════════

        [Route("Dashboard/Reports")]
        [Authorize(Roles = "Super Admin,Admin")]
        public IActionResult Reports()
        {
            return View("~/Views/Dashboard/Reports.cshtml");
        }

        // ══════════════════════════════════════════════════════════════════════
        // REPORTS API
        // ══════════════════════════════════════════════════════════════════════



        [HttpGet]
        [Route("Dashboard/Reports/GetAll")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportsGetAll()
        {
            var reports = await _db.Reports
                .Include(r => r.DistributionDestinations)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.DbConnectionConfigId,
                    r.Query,
                    r.OutputFileName,
                    r.OutputFormat,
                    r.ExecutionType,
                    r.SingleRunTiming,
                    r.SingleRunDateTime,
                    r.ScheduleFrequency,
                    r.ScheduleDaysOfWeek,
                    r.ScheduleDayOfMonth,
                    r.ScheduleCustomDates,
                    r.ScheduleCustomRecurring,
                    r.ScheduleTime,
                    r.Status,
                    r.LastRunDate,
                    r.NextRunDate,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.LastErrorMessage,
                    // Distribution
                    r.EnableEmailDistribution,
                    r.EmailToRecipients,
                    r.EmailCcRecipients,
                    r.EmailBccRecipients,
                    r.EmailSubject,
                    r.EmailBodyTemplate,
                    r.EnableFileSave,
                    r.FileSavePath,
                    r.MaxRowsPerSheet,
                    DistributionDestinations = r.DistributionDestinations.Select(d => new
                    {
                        d.Id,
                        d.DestinationType,
                        d.EmailTo,
                        d.EmailCc,
                        d.EmailBcc,
                        d.EmailSubject,
                        d.EmailBody,
                        d.FilePath,
                        //d.MaxRowsPerSheet,  // ← ADD THIS
                        d.IsActive
                    }).ToList()
                })
                .ToListAsync();

            return Json(reports);
        }







        [HttpPost]
        [Route("Dashboard/Reports/Create")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportsCreate([FromBody] CreateReportRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // ── Validation ──────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Report name is required." });
            if (req.DbConnectionConfigId <= 0)
                return BadRequest(new { message = "Please select a valid database." });
            if (string.IsNullOrWhiteSpace(req.Query))
                return BadRequest(new { message = "Report query is required." });
            if (string.IsNullOrWhiteSpace(req.OutputFileName))
                return BadRequest(new { message = "Output file name is required." });

            var validFormats = new[] { "csv", "excel" };
            if (!validFormats.Contains(req.OutputFormat?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Output format must be CSV or Excel." });

            var validExecTypes = new[] { "single", "scheduled" };
            if (!validExecTypes.Contains(req.ExecutionType?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Execution type must be Single or Scheduled." });

            // Verify database exists
            var dbConfig = await _db.DbConnectionConfigs.FindAsync(req.DbConnectionConfigId);
            if (dbConfig is null)
                return BadRequest(new { message = "Selected database configuration not found." });

            // ── Build report entity ─────────────────────────────────────────
            var report = new Report
            {
                Name = req.Name.Trim(),
                DbConnectionConfigId = req.DbConnectionConfigId,
                Query = req.Query.Trim(),
                OutputFileName = req.OutputFileName.Trim(),
                OutputFormat = req.OutputFormat!.ToLowerInvariant(),
                ExecutionType = req.ExecutionType!.ToLowerInvariant(),
                CreatedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                Status = "active",
                MaxRowsPerSheet = req.MaxRowsPerSheet
            };

            // Single run configuration
            if (report.ExecutionType == "single")
            {
                report.SingleRunTiming = req.SingleRunTiming?.ToLowerInvariant() ?? "immediately";
                if (report.SingleRunTiming == "scheduled")
                {
                    if (!req.SingleRunDateTime.HasValue)
                        return BadRequest(new { message = "Scheduled date and time is required for single run." });
                    report.SingleRunDateTime = req.SingleRunDateTime.Value.ToUniversalTime();
                    report.NextRunDate = report.SingleRunDateTime;
                }
            }
            // Scheduled run configuration
            else
            {
                var validFreqs = new[] { "daily", "weekly", "monthly", "custom_dates", "custom_recurring" };
                if (!validFreqs.Contains(req.ScheduleFrequency?.ToLowerInvariant() ?? ""))
                    return BadRequest(new { message = "Invalid schedule frequency." });

                report.ScheduleFrequency = req.ScheduleFrequency!.ToLowerInvariant();

                switch (report.ScheduleFrequency)
                {
                    case "daily":
                        report.ScheduleTime = req.ScheduleTime;
                        break;
                    case "weekly":
                        if (req.ScheduleDaysOfWeek is null || req.ScheduleDaysOfWeek.Count == 0)
                            return BadRequest(new { message = "At least one day of week is required." });
                        report.ScheduleDaysOfWeek = System.Text.Json.JsonSerializer.Serialize(req.ScheduleDaysOfWeek);
                        report.ScheduleTime = req.ScheduleTime;
                        break;
                    case "monthly":
                        if (!req.ScheduleDayOfMonth.HasValue || req.ScheduleDayOfMonth.Value < 1 || req.ScheduleDayOfMonth.Value > 31)
                            return BadRequest(new { message = "Valid day of month (1-31) is required." });
                        report.ScheduleDayOfMonth = req.ScheduleDayOfMonth;
                        report.ScheduleTime = req.ScheduleTime;
                        break;
                    case "custom_dates":
                        if (req.ScheduleCustomDates is null || req.ScheduleCustomDates.Count == 0)
                            return BadRequest(new { message = "At least one custom date is required." });
                        report.ScheduleCustomDates = System.Text.Json.JsonSerializer.Serialize(req.ScheduleCustomDates);
                        break;
                    case "custom_recurring":
                        if (req.ScheduleCustomRecurring is null || req.ScheduleCustomRecurring.Count == 0)
                            return BadRequest(new { message = "At least one custom recurring schedule is required." });
                        report.ScheduleCustomRecurring = System.Text.Json.JsonSerializer.Serialize(req.ScheduleCustomRecurring);
                        break;
                }
            }

            // ── Distribution Configuration ───────────────────────────────────
            report.EnableEmailDistribution = req.EnableEmailDistribution;
            if (report.EnableEmailDistribution)
            {
                report.EmailToRecipients = req.EmailToRecipients;
                report.EmailCcRecipients = req.EmailCcRecipients;
                report.EmailBccRecipients = req.EmailBccRecipients;
                report.EmailSubject = req.EmailSubject;
                report.EmailBodyTemplate = req.EmailBodyTemplate;
            }

            report.EnableFileSave = req.EnableFileSave;
            if (report.EnableFileSave)
            {
                report.FileSavePath = req.FileSavePath;
            }

            // Distribution destinations
            if (req.DistributionDestinations?.Any() == true)
            {
                foreach (var dest in req.DistributionDestinations)
                {
                    report.DistributionDestinations.Add(new ReportDistributionDestination
                    {
                        DestinationType = dest.DestinationType.ToLowerInvariant(),
                        EmailTo = dest.EmailTo,
                        EmailCc = dest.EmailCc,
                        EmailBcc = dest.EmailBcc,
                        EmailSubject = dest.EmailSubject,
                        EmailBody = dest.EmailBody,
                        FilePath = dest.FilePath,
                      
                        IsActive = dest.IsActive
                    });
                }
            }

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - CREATED REPORT '{report.Name}'",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            await _reportScheduler.ScheduleReportAsync(report);

            // ── Return response with distribution data ─────────────────────────
            return Json(new
            {
                report.Id,
                report.Name,
                report.DbConnectionConfigId,
                report.Query,
                report.OutputFileName,
                report.OutputFormat,
                report.ExecutionType,
                report.SingleRunTiming,
                report.SingleRunDateTime,
                report.ScheduleFrequency,
                report.ScheduleDaysOfWeek,
                report.ScheduleDayOfMonth,
                report.ScheduleCustomDates,
                report.ScheduleCustomRecurring,
                report.ScheduleTime,
                report.Status,
                report.LastRunDate,
                report.NextRunDate,
                report.CreatedAt,
                report.UpdatedAt,
                report.LastErrorMessage,
                // Distribution
                report.EnableEmailDistribution,
                report.EmailToRecipients,
                report.EmailCcRecipients,
                report.EmailBccRecipients,
                report.EmailSubject,
                report.EmailBodyTemplate,
                report.EnableFileSave,
                report.FileSavePath,
                report.MaxRowsPerSheet,
                DistributionDestinations = report.DistributionDestinations.Select(d => new
                {
                    d.Id,
                    d.DestinationType,
                    d.EmailTo,
                    d.EmailCc,
                    d.EmailBcc,
                    d.EmailSubject,
                    d.EmailBody,
                    d.FilePath,
                    //d.MaxRowsPerSheet,
                    d.IsActive
                }).ToList()
            });
        }




        [HttpPost]
        [Route("Dashboard/Reports/Update/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportsUpdate(int id, [FromBody] UpdateReportRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var report = await _db.Reports
                .Include(r => r.DistributionDestinations)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report is null)
                return NotFound(new { message = "Report not found." });

            // ── Update basic fields ─────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(req.Name))
                report.Name = req.Name.Trim();
            if (req.DbConnectionConfigId.HasValue && req.DbConnectionConfigId.Value > 0)
            {
                var dbConfig = await _db.DbConnectionConfigs.FindAsync(req.DbConnectionConfigId.Value);
                if (dbConfig is null)
                    return BadRequest(new { message = "Selected database configuration not found." });
                report.DbConnectionConfigId = req.DbConnectionConfigId.Value;
            }

            if (!string.IsNullOrWhiteSpace(req.Query))
                report.Query = req.Query.Trim();
            if (!string.IsNullOrWhiteSpace(req.OutputFileName))
                report.OutputFileName = req.OutputFileName.Trim();
            if (!string.IsNullOrWhiteSpace(req.OutputFormat))
            {
                var validFormats = new[] { "csv", "excel" };
                if (!validFormats.Contains(req.OutputFormat.ToLowerInvariant()))
                    return BadRequest(new { message = "Output format must be CSV or Excel." });
                report.OutputFormat = req.OutputFormat.ToLowerInvariant();
            }

            // ── Update execution type and schedule ──────────────────────────
            if (!string.IsNullOrWhiteSpace(req.ExecutionType))
            {
                var validExecTypes = new[] { "single", "scheduled" };
                if (!validExecTypes.Contains(req.ExecutionType.ToLowerInvariant()))
                    return BadRequest(new { message = "Execution type must be Single or Scheduled." });

                report.ExecutionType = req.ExecutionType.ToLowerInvariant();

                // Reset schedule fields
                report.SingleRunTiming = null;
                report.SingleRunDateTime = null;
                report.ScheduleFrequency = null;
                report.ScheduleDaysOfWeek = null;
                report.ScheduleDayOfMonth = null;
                report.ScheduleCustomDates = null;
                report.ScheduleCustomRecurring = null;
                report.ScheduleTime = null;
                report.NextRunDate = null;

                if (report.ExecutionType == "single")
                {
                    report.SingleRunTiming = req.SingleRunTiming?.ToLowerInvariant() ?? "immediately";
                    if (report.SingleRunTiming == "scheduled" && req.SingleRunDateTime.HasValue)
                    {
                        report.SingleRunDateTime = req.SingleRunDateTime.Value.ToUniversalTime();
                        report.NextRunDate = report.SingleRunDateTime;
                    }
                }
                else
                {
                    var validFreqs = new[] { "daily", "weekly", "monthly", "custom_dates", "custom_recurring" };
                    if (!validFreqs.Contains(req.ScheduleFrequency?.ToLowerInvariant() ?? ""))
                        return BadRequest(new { message = "Invalid schedule frequency." });

                    report.ScheduleFrequency = req.ScheduleFrequency!.ToLowerInvariant();

                    switch (report.ScheduleFrequency)
                    {
                        case "daily":
                            report.ScheduleTime = req.ScheduleTime;
                            break;
                        case "weekly":
                            if (req.ScheduleDaysOfWeek is null || req.ScheduleDaysOfWeek.Count == 0)
                                return BadRequest(new { message = "At least one day of week is required." });
                            report.ScheduleDaysOfWeek = System.Text.Json.JsonSerializer.Serialize(req.ScheduleDaysOfWeek);
                            report.ScheduleTime = req.ScheduleTime;
                            break;
                        case "monthly":
                            if (!req.ScheduleDayOfMonth.HasValue || req.ScheduleDayOfMonth.Value < 1 || req.ScheduleDayOfMonth.Value > 31)
                                return BadRequest(new { message = "Valid day of month (1-31) is required." });
                            report.ScheduleDayOfMonth = req.ScheduleDayOfMonth;
                            report.ScheduleTime = req.ScheduleTime;
                            break;
                        case "custom_dates":
                            if (req.ScheduleCustomDates is null || req.ScheduleCustomDates.Count == 0)
                                return BadRequest(new { message = "At least one custom date is required." });
                            report.ScheduleCustomDates = System.Text.Json.JsonSerializer.Serialize(req.ScheduleCustomDates);
                            break;
                        case "custom_recurring":
                            if (req.ScheduleCustomRecurring is null || req.ScheduleCustomRecurring.Count == 0)
                                return BadRequest(new { message = "At least one custom recurring schedule is required." });
                            report.ScheduleCustomRecurring = System.Text.Json.JsonSerializer.Serialize(req.ScheduleCustomRecurring);
                            break;
                    }
                }
            }

            // ── Update Distribution ───────────────────────────────────────────
            report.EnableEmailDistribution = req.EnableEmailDistribution ?? false;
            report.EmailToRecipients = report.EnableEmailDistribution ? req.EmailToRecipients : null;
            report.EmailCcRecipients = report.EnableEmailDistribution ? req.EmailCcRecipients : null;
            report.EmailBccRecipients = report.EnableEmailDistribution ? req.EmailBccRecipients : null;
            report.EmailSubject = report.EnableEmailDistribution ? req.EmailSubject : null;
            report.EmailBodyTemplate = report.EnableEmailDistribution ? req.EmailBodyTemplate : null;

            report.EnableFileSave = req.EnableFileSave ?? false;
            report.FileSavePath = report.EnableFileSave ? req.FileSavePath : null;
            // In the update section:
            if (req.MaxRowsPerSheet.HasValue)
                report.MaxRowsPerSheet = req.MaxRowsPerSheet.Value;
            else
                report.MaxRowsPerSheet = null; // Allow clearing

            // Replace destinations
            _db.ReportDistributionDestinations.RemoveRange(report.DistributionDestinations);

            if (req.DistributionDestinations?.Any() == true)
            {
                foreach (var dest in req.DistributionDestinations)
                {
                    report.DistributionDestinations.Add(new ReportDistributionDestination
                    {
                        DestinationType = dest.DestinationType.ToLowerInvariant(),
                        EmailTo = dest.EmailTo,
                        EmailCc = dest.EmailCc,
                        EmailBcc = dest.EmailBcc,
                        EmailSubject = dest.EmailSubject,
                        EmailBody = dest.EmailBody,
                        FilePath = dest.FilePath,
                  
                        IsActive = dest.IsActive
                    });
                }
            }

            report.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED REPORT ID: {id} - '{report.Name}'",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            await _reportScheduler.ScheduleReportAsync(report);

            // ── Return response with distribution data ─────────────────────────
            return Json(new
            {
                report.Id,
                report.Name,
                report.DbConnectionConfigId,
                report.Query,
                report.OutputFileName,
                report.OutputFormat,
                report.ExecutionType,
                report.SingleRunTiming,
                report.SingleRunDateTime,
                report.ScheduleFrequency,
                report.ScheduleDaysOfWeek,
                report.ScheduleDayOfMonth,
                report.ScheduleCustomDates,
                report.ScheduleCustomRecurring,
                report.ScheduleTime,
                report.Status,
                report.LastRunDate,
                report.NextRunDate,
                report.CreatedAt,
                report.UpdatedAt,
                report.LastErrorMessage,
                // Distribution
                report.EnableEmailDistribution,
                report.EmailToRecipients,
                report.EmailCcRecipients,
                report.EmailBccRecipients,
                report.EmailSubject,
                report.EmailBodyTemplate,
                report.EnableFileSave,
                report.FileSavePath,
                report.MaxRowsPerSheet,
                DistributionDestinations = report.DistributionDestinations.Select(d => new
                {
                    d.Id,
                    d.DestinationType,
                    d.EmailTo,
                    d.EmailCc,
                    d.EmailBcc,
                    d.EmailSubject,
                    d.EmailBody,
                    d.FilePath,
                    d.IsActive
                }).ToList()
            });
        }




        [HttpPost]
        [Route("Dashboard/Reports/Delete/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportsDelete(int id)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var report = await _db.Reports.FindAsync(id);
            if (report is null)
                return NotFound(new { message = "Report not found." });

            _db.Reports.Remove(report);
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - DELETED REPORT ID: {id} - '{report.Name}'",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );

            await _reportScheduler.UnscheduleReportAsync(id);
            return Json(new { message = "Report deleted." });
        }

        [HttpPost]
        [Route("Dashboard/Reports/UpdateStatus/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportsUpdateStatus(int id, [FromBody] UpdateReportStatusRequest req)
        {
            var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (currentRole.Equals("Support", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var allowed = new[] { "active", "inactive" };
            if (!allowed.Contains(req.Status?.ToLowerInvariant() ?? ""))
                return BadRequest(new { message = "Status must be 'active' or 'inactive'." });

            var report = await _db.Reports.FindAsync(id);
            if (report is null)
                return NotFound(new { message = "Report not found." });

            report.Status = req.Status!.ToLowerInvariant();
            report.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ── Audit log ─────────────────────────────────────────────────────
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var email = User.FindFirstValue(ClaimTypes.Email);
            await AuditLogger.LogAsync(
                db: _db,
                eventName: $"{email} - UPDATED REPORT ID: {id} STATUS TO {report.Status}",
                userId: userId,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                pageUrl: HttpContext.Request.Path
            );
            if (report.Status == "active")
                await _reportScheduler.ScheduleReportAsync(report);
            else
                await _reportScheduler.UnscheduleReportAsync(report.Id);
            return Json(new { report.Id, report.Status });
        }




        // ══════════════════════════════════════════════════════════════════════
        // REPORTS VIEWS
        // ══════════════════════════════════════════════════════════════════════


        [Route("Dashboard/Reports/{reportName}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> ReportDetail(string reportName)
        {
            var report = await _db.Reports
                .Include(r => r.DbConnectionConfig)
                .FirstOrDefaultAsync(r => r.Name == reportName);

            if (report is null)
                return NotFound();

            return View("~/Views/Dashboard/ReportDetail.cshtml", report);
        }




        // ══════════════════════════════════════════════════════════════════════
        // EXECUTIONS API
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("Dashboard/Reports/Executions/{reportId:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> GetExecutions(int reportId)
        {
            var report = await _db.Reports.FindAsync(reportId);
            if (report is null)
                return NotFound(new { message = "Report not found." });

            var executions = await _db.Executions
                .Where(e => e.ReportId == reportId)
                .OrderByDescending(e => e.StartTime)
                .Select(e => new
                {
                    e.Id,
                    e.ReportId,
                    e.ExecutionStatus,
                    e.ExecutionLogsPath,
                    e.ExecutionResultPath,
                    e.EmailsSentJson,
                    e.FilesSentJson,
                    e.StartTime,
                    e.EndTime,
                    e.RowCount,
                    e.ErrorMessage,
                    e.CreatedAt
                })
                .ToListAsync();

            var result = executions.Select(e => new
            {
                e.Id,
                e.ReportId,
                e.ExecutionStatus,
                e.ExecutionLogsPath,
                e.ExecutionResultPath,
                EmailsSent = !string.IsNullOrEmpty(e.EmailsSentJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<object>>(e.EmailsSentJson)
                    : new List<object>(),
                FilesSent = !string.IsNullOrEmpty(e.FilesSentJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<object>>(e.FilesSentJson)
                    : new List<object>(),
                e.StartTime,
                e.EndTime,
                e.RowCount,
                e.ErrorMessage,
                e.CreatedAt
            });

            return Json(result);
        }

        [HttpPost]
        [Route("Dashboard/Reports/Executions/Create")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> CreateExecution([FromBody] CreateExecutionRequest req)
        {
            var report = await _db.Reports.FindAsync(req.ReportId);
            if (report is null)
                return NotFound(new { message = "Report not found." });

            var execution = new Execution
            {
                ReportId = req.ReportId,
                ExecutionStatus = "running",
                StartTime = DateTime.UtcNow
            };

            _db.Executions.Add(execution);
            await _db.SaveChangesAsync();

            return Json(new
            {
                execution.Id,
                execution.ReportId,
                execution.ExecutionStatus,
                execution.StartTime
            });
        }

        [HttpPost]
        [Route("Dashboard/Reports/Executions/Update/{id:int}")]
        [Authorize(Roles = "Super Admin,Admin")]
        public async Task<IActionResult> UpdateExecution(int id, [FromBody] UpdateExecutionRequest req)
        {
            var execution = await _db.Executions.FindAsync(id);
            if (execution is null)
                return NotFound(new { message = "Execution not found." });

            if (!string.IsNullOrWhiteSpace(req.ExecutionStatus))
            {
                var validStatuses = new[] { "running", "completed", "failed" };
                if (!validStatuses.Contains(req.ExecutionStatus.ToLowerInvariant()))
                    return BadRequest(new { message = "Status must be running, completed, or failed." });
                execution.ExecutionStatus = req.ExecutionStatus.ToLowerInvariant();
            }

            if (req.EndTime.HasValue)
                execution.EndTime = req.EndTime.Value;

            if (!string.IsNullOrWhiteSpace(req.ExecutionLogsPath))
                execution.ExecutionLogsPath = req.ExecutionLogsPath;

            if (!string.IsNullOrWhiteSpace(req.ExecutionResultPath))
                execution.ExecutionResultPath = req.ExecutionResultPath;

            if (req.EmailsSent != null)
                execution.EmailsSentJson = System.Text.Json.JsonSerializer.Serialize(req.EmailsSent);

            if (req.FilesSent != null)
                execution.FilesSentJson = System.Text.Json.JsonSerializer.Serialize(req.FilesSent);

            if (req.RowCount.HasValue)
                execution.RowCount = req.RowCount.Value;

            if (!string.IsNullOrWhiteSpace(req.ErrorMessage))
                execution.ErrorMessage = req.ErrorMessage;

            await _db.SaveChangesAsync();

            return Json(new
            {
                execution.Id,
                execution.ExecutionStatus,
                execution.StartTime,
                execution.EndTime,
                execution.RowCount
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // EXECUTION LOGS API
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Route("Dashboard/Reports/ExecutionLogs")]
        [Authorize(Roles = "Super Admin,Admin")]
        public IActionResult GetExecutionLogs([FromQuery] string path, [FromQuery] bool download = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(new { message = "Log path is required." });

            // Security: prevent directory traversal
            var fullPath = Path.GetFullPath(path);
            var reportsRoot = Path.GetFullPath(GlobalVariables.reportsDirectory);

            if (!fullPath.StartsWith(reportsRoot, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!System.IO.File.Exists(fullPath))
                return NotFound(new { message = "Log file not found." });

            if (download)
            {
                var fileName = Path.GetFileName(fullPath);
                var mimeType = "text/plain";
                return PhysicalFile(fullPath, mimeType, fileName);
            }

            // Return file content as plain text
            var content = System.IO.File.ReadAllText(fullPath);
            return Content(content, "text/plain");
        }





    }
}
