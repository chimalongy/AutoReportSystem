// Services/ReportSchedulerStartup.cs
using Microsoft.EntityFrameworkCore;
using ARS.Data;

namespace ARS.Services
{
    public class ReportSchedulerStartup : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ReportSchedulerStartup> _logger;

        public ReportSchedulerStartup(IServiceProvider services, ILogger<ReportSchedulerStartup> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schedulerService = scope.ServiceProvider.GetRequiredService<ReportSchedulerService>();

            var activeReports = await db.Reports
                .Where(r => r.Status == "active")
                .ToListAsync(cancellationToken);

            _logger.LogInformation("ReportSchedulerStartup: Scheduling {Count} active reports.", activeReports.Count);

            foreach (var report in activeReports)
            {
                try
                {
                    await schedulerService.ScheduleReportAsync(report);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReportSchedulerStartup: Failed to schedule report {ReportId}", report.Id);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}