using Microsoft.EntityFrameworkCore;
using ARS.Models;

namespace ARS.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }

        public DbSet<DbConnectionConfig> DbConnectionConfigs { get; set; }
        public DbSet<Report> Reports { get; set; } = null!;

        // ── Distribution ──
        public DbSet<ReportDistributionDestination> ReportDistributionDestinations { get; set; } = null!;
        public DbSet<Execution> Executions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // 👇 Tell EF Core these are jsonb columns, not text
            modelBuilder.Entity<Execution>()
                .Property(e => e.EmailsSentJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Execution>()
                .Property(e => e.FilesSentJson)
                .HasColumnType("jsonb");

            base.OnModelCreating(modelBuilder);
        }
    }
}
