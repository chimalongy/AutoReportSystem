// Jobs/ReportExecutionJob.cs
using Quartz;
using Microsoft.EntityFrameworkCore;
using ARS.Data;
using ARS.Models;
using ARS.Classess.Utils;

namespace ARS.Jobs
{
    [DisallowConcurrentExecution]
    public class ReportExecutionJob : IJob
    {
        private readonly IServiceScopeFactory _scopeFactory;  // 👈 swap this
        private readonly ILogger<ReportExecutionJob> _logger;

        public ReportExecutionJob(IServiceScopeFactory scopeFactory, ILogger<ReportExecutionJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public static readonly JobKey Key = new("ReportExecutionJob");

        public async Task Execute(IJobExecutionContext context)
        {
            using var scope = _scopeFactory.CreateScope();  // 👈 fresh scope per execution
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oneDrive = scope.ServiceProvider.GetRequiredService<OneDriveUploader>();
            var reportId = context.MergedJobDataMap.GetInt("reportId");

            var report = await db.Reports
                .Include(r => r.DbConnectionConfig)
                .Include(r => r.DistributionDestinations)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
            {
                _logger.LogWarning("ReportExecutionJob: Report {ReportId} not found.", reportId);
                return;
            }

            if (report.Status != "active")
            {
                _logger.LogInformation("ReportExecutionJob: Report {ReportId} is not active, skipping.", reportId);
                return;
            }

            _logger.LogInformation("ReportExecutionJob: Executing report {ReportId} - {ReportName}", reportId, report.Name);

            var fetcher = new ReportFetcher(db, oneDrive);  // 👈 uses the fresh scoped db
            await fetcher.ExecuteReportAsync(report);
        }
    }
}