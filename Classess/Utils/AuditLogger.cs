using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;

namespace ARS.Classess.Utils
{
    public static class AuditLogger
    {
        public static async Task LogAsync(
      AppDbContext db,
      string eventName,       // keep for backward compat
      int userId,
      string? ipAddress,
      string? pageUrl,
      string? userEmail = null,
      string? action = null,
      string? resourceName = null)
        {
            var log = new AuditLog
            {
                Event = eventName,
                EventDate = DateTime.UtcNow,
                IpAddress = ipAddress,
                PageUrl = pageUrl,
                UserId = userId.ToString(),
                UserEmail = userEmail,
                Action = action,
                ResourceName = resourceName
            };
            db.AuditLogs.Add(log);
            await db.SaveChangesAsync();
        }
    }
}