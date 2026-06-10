using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;

namespace ARS.Classess.Utils
{
    public static class AuditLogger
    {
        public static async Task LogAsync(
            AppDbContext db,
            string eventName,
            int? userId = null,
            string? ipAddress = null,
            string? pageUrl = null)
        {
            try
            {
                // Get schema from EF Core model metadata (reads from AppDbContext's OnModelCreating)
                var schema = db.Model
                    .FindEntityType(typeof(AuditLog))!
                    .GetSchema() ?? "public";

                await db.Database.ExecuteSqlRawAsync(
                    $@"
                    INSERT INTO {schema}.audit_logs (event, eventdate, ipaddress, pageurl, userid)
                    VALUES ({{0}}, {{1}}, {{2}}, {{3}}, {{4}})",
                    eventName,
                    DateTime.UtcNow,
                    ipAddress ?? (object)DBNull.Value,
                    pageUrl ?? (object)DBNull.Value,
                    userId.HasValue ? userId.Value.ToString() : (object)DBNull.Value
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuditLogger] Failed: {ex.Message}");
            }
        }
    }
}