// Utils/DownloadTokenStore.cs
using ARS.Data;
using ARS.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ARS.Classess.Utils
{
    public class DownloadTokenStore
    {
        private readonly AppDbContext _db;

        private readonly int _tokenExpiryDays;

        public DownloadTokenStore(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _tokenExpiryDays = configuration.GetValue<int>("DownloadSettings:TokenExpiryDays", 7);
        }

        public async Task<string> GenerateAsync(int executionId, string filePath, TimeSpan? ttl = null)
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                               .Replace("+", "-").Replace("/", "_").Replace("=", "");

            _db.DownloadTokens.Add(new DownloadToken
            {
                Token = token,
                ExecutionId = executionId,
                FilePath = filePath,
                ExpiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromDays(_tokenExpiryDays))
            });

            await _db.SaveChangesAsync();
            return token;
        }

        public async Task<string?> ResolveAsync(string token)
        {
            var entry = await _db.DownloadTokens
                .Where(t => t.Token == token)
                .FirstOrDefaultAsync();

            if (entry == null) return null;

            if (DateTime.UtcNow > entry.ExpiresAt)
            {
                _db.DownloadTokens.Remove(entry);
                await _db.SaveChangesAsync();
                return null;
            }

            return entry.FilePath;
        }

        // Optional: call this periodically to clean up expired tokens
        public async Task PurgeExpiredAsync()
        {
            var expired = _db.DownloadTokens.Where(t => t.ExpiresAt < DateTime.UtcNow);
            _db.DownloadTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }
}