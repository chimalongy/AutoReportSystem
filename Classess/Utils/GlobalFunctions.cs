using ARS.Data;
using ARS.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Diagnostics;

namespace ARS.Classess.Utils
{
    public static class GlobalFunctions
    {
        // ── GET all users ─────────────────────────────────────────────────────
        public static async Task<IEnumerable<object>> GetAllUsersAsync(AppDbContext db)
        {
            return await db.AppUsers
                .OrderByDescending(u => u.CreatedAt ?? DateTime.MinValue)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Department,
                    u.Role,
                    u.ProfileStatus,
                    u.LastLoginDate,
                    u.CreatedAt
                })
                .ToListAsync();
        }

        // ── CREATE user ───────────────────────────────────────────────────────
        public static async Task<(object? Result, string? Error)> CreateUserAsync(
            AppDbContext db,
            IConfiguration config,
            string firstName,
            string lastName,
            string email,
            string? department,
            string? role)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email))
            {
                return (null, "First name, last name, and email are required.");
            }

            bool emailExists = await db.AppUsers
                .AnyAsync(u => u.Email.ToLower() == email.ToLower());

            if (emailExists)
                return (null, "CONFLICT");

            var defaultPassword = config["DefaultPassword"];
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

            var user = new AppUser
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim().ToLower(),
                Department = department?.Trim(),
                Role = role ?? "Support",
                ProfileStatus = "enabled",
                Password = hashedPassword,
                CreatedAt = DateTime.UtcNow
            };

            db.AppUsers.Add(user);
            await db.SaveChangesAsync();

            return (new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Department,
                user.Role,
                user.ProfileStatus,
                user.LastLoginDate,
                user.CreatedAt
            }, null);
        }

        // ── UPDATE user ───────────────────────────────────────────────────────
        public static async Task<(object? Result, string? Error)> UpdateUserAsync(
            AppDbContext db,
            int id,
            string? firstName,
            string? lastName,
            string? email,
            string? department,
            string? role)
        {
            var user = await db.AppUsers.FindAsync(id);
            if (user is null)
                return (null, "NOT_FOUND");

            // Check email uniqueness if changing
            if (!string.IsNullOrWhiteSpace(email) &&
                !user.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                bool emailExists = await db.AppUsers
                    .AnyAsync(u => u.Email.ToLower() == email.Trim().ToLower() && u.Id != id);
                if (emailExists)
                    return (null, "CONFLICT");
                user.Email = email.Trim().ToLower();
            }

            if (!string.IsNullOrWhiteSpace(firstName))
                user.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName))
                user.LastName = lastName.Trim();
            if (department is not null)
                user.Department = department.Trim();
            if (!string.IsNullOrWhiteSpace(role))
                user.Role = role.Trim();

            await db.SaveChangesAsync();

            return (new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Department,
                user.Role,
                user.ProfileStatus,
                user.LastLoginDate,
                user.CreatedAt
            }, null);
        }

        // ── DELETE user ───────────────────────────────────────────────────────
        public static async Task<string?> DeleteUserAsync(AppDbContext db, int id)
        {
            var user = await db.AppUsers.FindAsync(id);
            if (user is null)
                return "NOT_FOUND";

            db.AppUsers.Remove(user);
            await db.SaveChangesAsync();
            return null;
        }

        // ── UPDATE user status ────────────────────────────────────────────────
        public static async Task<(object? Result, string? Error)> UpdateUserStatusAsync(
            AppDbContext db,
            int id,
            string? newStatus)
        {
            var allowed = new[] { "enabled", "disabled" };
            if (!allowed.Contains(newStatus?.ToLower()))
                return (null, "Status must be 'enabled' or 'disabled'.");

            var user = await db.AppUsers.FindAsync(id);
            if (user is null)
                return (null, "NOT_FOUND");

            user.ProfileStatus = newStatus!.ToLower();
            await db.SaveChangesAsync();

            return (new { user.Id, user.ProfileStatus }, null);
        }

        public static async Task<IEnumerable<object>> GetAllAuditLogsAsyncOLD(AppDbContext db)
        {
            return await db.AuditLogs
                .OrderByDescending(l => l.EventDate)
                .Select(l => new
                {
                    l.Id,
                    l.UserId,
                    l.IpAddress,
                    l.Event,
                    l.EventDate,
                    l.PageUrl,
                    l.UserEmail,
                    l.Action,
                    l.ResourceName
                })
                .ToListAsync();
        }

        // ── SEED default super admin ──────────────────────────────────────────
        public static async Task SeedSuperAdminAsync(AppDbContext db, IConfiguration config)
        {
            var superAdminEmail = config["SuperAdmin:Email"] ?? "superadmin@ars.com";
            var superAdminPassword = config["SuperAdmin:Password"] ?? "SuperAdmin@123";

            var exists = await db.AppUsers
                .AnyAsync(u => u.Email.ToLower() == superAdminEmail.ToLower());

            if (!exists)
            {
                var hashedPassword = superAdminPassword;

                var superAdmin = new AppUser
                {
                    FirstName = "System",
                    LastName = "Super Admin",
                    Email = superAdminEmail.ToLower(),
                    Department = "IT",
                    Role = "Super Admin",
                    ProfileStatus = "enabled",
                    Password = hashedPassword,
                    CreatedAt = DateTime.UtcNow
                };

                db.AppUsers.Add(superAdmin);
                await db.SaveChangesAsync();

                Console.WriteLine($"[Seed] Super Admin created: {superAdminEmail}");
            }
        }



        public static NpgsqlDataReader GetDataReader(string qry, NpgsqlParameter[] parameters, string oradb)
        {
            NpgsqlDataReader dr = null;

            Stopwatch se = new Stopwatch();
            se.Start();

            ErrorLogger.LogError(DateTime.Now.ToString() + "INFO INITIALIZING PULLING FROM DB");

            NpgsqlConnection Standby_connection = new NpgsqlConnection(oradb);
            NpgsqlCommand cmd = new NpgsqlCommand();
            int retrycount = 0;
            const int maxRetries = 3;

        retry:
            try
            {
                cmd.Connection = Standby_connection;
                cmd.CommandText = qry;
                cmd.CommandType = CommandType.Text;

                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddRange(parameters);
                }

                if (Standby_connection.State != System.Data.ConnectionState.Open)
                    Standby_connection.Open();

                dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(DateTime.Now.ToString() + " ERROR OCCURRED PULLING FROM DB: " + ex.Message);

                try { Standby_connection.Close(); } catch { }

                retrycount++;
                if (retrycount < maxRetries)
                {
                    goto retry;
                }

                dr = null;
            }
            finally
            {
                se.Stop();
                TimeSpan ts = se.Elapsed;
                string eT = string.Format("{0:00}:{1:00}:{2:00}:{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

                ErrorLogger.LogError(DateTime.Now.ToString() + "  INFO COMPLETE PULLING FROM DB");

                cmd.Dispose();
            }

            return dr;
        }













    }



}
