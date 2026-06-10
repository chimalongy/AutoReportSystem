using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ARS.Models;

namespace ARS.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IConfiguration _config;

        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration config)
            : base(options)
        {
            _config = config;
        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<DbConnectionConfig> DbConnectionConfigs { get; set; }
        public DbSet<Report> Reports { get; set; } = null!;
        public DbSet<ReportDistributionDestination> ReportDistributionDestinations { get; set; } = null!;
        public DbSet<Execution> Executions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var schema = _config["DatabaseSettings:Schema"] ?? "public";

            // Apply schema to all entities
            modelBuilder.Entity<AuditLog>().ToTable("audit_logs", schema);
            modelBuilder.Entity<AppUser>().ToTable("app_users", schema);
            modelBuilder.Entity<DbConnectionConfig>().ToTable("db_connection_configs", schema);
            modelBuilder.Entity<Report>().ToTable("reports", schema);
            modelBuilder.Entity<ReportDistributionDestination>().ToTable("report_distribution_destinations", schema);
            modelBuilder.Entity<Execution>().ToTable("executions", schema);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

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