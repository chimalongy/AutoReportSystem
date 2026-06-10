// Services/ReportSchedulerService.cs
using Quartz;
using ARS.Models;

namespace ARS.Services
{
    public class ReportSchedulerService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<ReportSchedulerService> _logger;
        private static readonly TimeZoneInfo AppTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");

        public ReportSchedulerService(ISchedulerFactory schedulerFactory, ILogger<ReportSchedulerService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        // Call this after Create or Update
        public async Task ScheduleReportAsync(Report report)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            // Always remove existing triggers for this report first
            await UnscheduleReportAsync(report.Id);

            if (report.Status != "active")
                return;

            var triggers = BuildTriggers(report);
            if (triggers.Count == 0)
            {
                _logger.LogInformation("No triggers built for report {ReportId}", report.Id);
                return;
            }

            // Ensure the job exists (it's durable so it persists without triggers)
            if (!await scheduler.CheckExists(Jobs.ReportExecutionJob.Key))
            {
                var job = JobBuilder.Create<Jobs.ReportExecutionJob>()
                    .WithIdentity(Jobs.ReportExecutionJob.Key)
                    .StoreDurably()
                    .Build();
                await scheduler.AddJob(job, replace: false);
            }

            foreach (var trigger in triggers)
            {
                await scheduler.ScheduleJob(trigger);
                _logger.LogInformation("Scheduled trigger {TriggerKey} for report {ReportId}", trigger.Key, report.Id);
            }
        }

        // Call this on Delete or Deactivate
        public async Task UnscheduleReportAsync(int reportId)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            // Find and delete all triggers with this report's group
            var group = TriggerGroupFor(reportId);
            var triggerKeys = await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.GroupEquals(group));
            if (triggerKeys.Count > 0)
            {
                await scheduler.UnscheduleJobs(triggerKeys.ToList());
                _logger.LogInformation("Unscheduled {Count} trigger(s) for report {ReportId}", triggerKeys.Count, reportId);
            }
        }

        private List<ITrigger> BuildTriggers(Report report)
        {
            var triggers = new List<ITrigger>();
            var group = TriggerGroupFor(report.Id);

            if (report.ExecutionType == "single")
            {
                var trigger = BuildSingleTrigger(report, group);
                if (trigger is not null) triggers.Add(trigger);
            }
            else if (report.ExecutionType == "scheduled")
            {
                triggers.AddRange(BuildScheduledTriggers(report, group));
            }

            return triggers;
        }

        // ── Single Run ────────────────────────────────────────────────────────

        private ITrigger? BuildSingleTrigger(Report report, string group)
        {
            DateTimeOffset fireAt;

            if (report.SingleRunTiming == "immediately")
            {
                // Fire 5 seconds from now
                fireAt = DateTimeOffset.UtcNow.AddSeconds(5);
            }
            else if (report.SingleRunTiming == "scheduled" && report.SingleRunDateTime.HasValue)
            {
                fireAt = new DateTimeOffset(report.SingleRunDateTime.Value, TimeSpan.Zero);
                if (fireAt <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("Single run datetime for report {ReportId} is in the past, skipping.", report.Id);
                    return null;
                }
            }
            else
            {
                return null;
            }

            return TriggerBuilder.Create()
                .WithIdentity($"single", group)
                .ForJob(Jobs.ReportExecutionJob.Key)
                .UsingJobData("reportId", report.Id)
                .StartAt(fireAt)
                .WithSimpleSchedule(s => s.WithRepeatCount(0))
                .Build();
        }

        // ── Scheduled Runs ────────────────────────────────────────────────────

        private List<ITrigger> BuildScheduledTriggers(Report report, string group)
        {
            return report.ScheduleFrequency switch
            {
                "daily" => BuildDailyTriggers(report, group),
                "weekly" => BuildWeeklyTriggers(report, group),
                "monthly" => BuildMonthlyTriggers(report, group),
                "custom_dates" => BuildCustomDatesTriggers(report, group),
                "custom_recurring" => BuildCustomRecurringTriggers(report, group),
                _ => new List<ITrigger>()
            };
        }

        private List<ITrigger> BuildDailyTriggers(Report report, string group)
        {
            var (hour, minute) = ParseTimeAsUtc(report.ScheduleTime);

            var trigger = TriggerBuilder.Create()
                .WithIdentity("daily", group)
                .ForJob(Jobs.ReportExecutionJob.Key)
                .UsingJobData("reportId", report.Id)
                .WithCronSchedule($"0 {minute} {hour} * * ?", x => x.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            return new List<ITrigger> { trigger };
        }

        private List<ITrigger> BuildWeeklyTriggers(Report report, string group)
        {
            if (string.IsNullOrWhiteSpace(report.ScheduleDaysOfWeek))
                return new List<ITrigger>();

            var days = System.Text.Json.JsonSerializer.Deserialize<List<string>>(report.ScheduleDaysOfWeek)
                       ?? new List<string>();

            if (days.Count == 0)
                return new List<ITrigger>();

            var (hour, minute) = ParseTimeAsUtc(report.ScheduleTime);

            // Map day names to cron day-of-week abbreviations
            var cronDays = days
                .Select(d => d.Trim().ToLowerInvariant() switch
                {
                    "monday" => "MON",
                    "tuesday" => "TUE",
                    "wednesday" => "WED",
                    "thursday" => "THU",
                    "friday" => "FRI",
                    "saturday" => "SAT",
                    "sunday" => "SUN",
                    _ => null
                })
                .Where(d => d is not null)
                .ToList();

            if (cronDays.Count == 0)
                return new List<ITrigger>();

            var dayList = string.Join(",", cronDays);

            var trigger = TriggerBuilder.Create()
                .WithIdentity("weekly", group)
                .ForJob(Jobs.ReportExecutionJob.Key)
                .UsingJobData("reportId", report.Id)
                .WithCronSchedule($"0 {minute} {hour} ? * {dayList}", x => x.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            return new List<ITrigger> { trigger };
        }

        private List<ITrigger> BuildMonthlyTriggers(Report report, string group)
        {
            if (!report.ScheduleDayOfMonth.HasValue)
                return new List<ITrigger>();

            var day = Math.Clamp(report.ScheduleDayOfMonth.Value, 1, 31);
            var (hour, minute) = ParseTimeAsUtc(report.ScheduleTime);

            var trigger = TriggerBuilder.Create()
                .WithIdentity("monthly", group)
                .ForJob(Jobs.ReportExecutionJob.Key)
                .UsingJobData("reportId", report.Id)
                // LW handles months shorter than the day value gracefully (last day of month)
                .WithCronSchedule($"0 {minute} {hour} {day}W * ?", x => x.InTimeZone(TimeZoneInfo.Utc))
                .Build();

            return new List<ITrigger> { trigger };
        }

        private List<ITrigger> BuildCustomDatesTriggers(Report report, string group)
        {
            if (string.IsNullOrWhiteSpace(report.ScheduleCustomDates))
                return new List<ITrigger>();

            var dates = System.Text.Json.JsonSerializer.Deserialize<List<string>>(report.ScheduleCustomDates)
                        ?? new List<string>();

            var triggers = new List<ITrigger>();
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < dates.Count; i++)
            {
                if (!DateTime.TryParse(dates[i], out var date))
                    continue;

                // Custom dates fire at midnight UTC
                var fireAt = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);

                if (fireAt <= now)
                    continue;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"custom_date_{i}", group)
                    .ForJob(Jobs.ReportExecutionJob.Key)
                    .UsingJobData("reportId", report.Id)
                    .StartAt(fireAt)
                    .WithSimpleSchedule(s => s.WithRepeatCount(0))
                    .Build();

                triggers.Add(trigger);
            }

            return triggers;
        }

        private List<ITrigger> BuildCustomRecurringTriggers(Report report, string group)
        {
            if (string.IsNullOrWhiteSpace(report.ScheduleCustomRecurring))
                return new List<ITrigger>();

            var items = System.Text.Json.JsonSerializer.Deserialize<List<CustomRecurringEntry>>(report.ScheduleCustomRecurring)
                        ?? new List<CustomRecurringEntry>();

            var triggers = new List<ITrigger>();
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!DateTime.TryParse(item.Date, out var date))
                    continue;

                var (hour, minute) = ParseTimeAsUtc(item.Time);
                var fireAt = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero);

                if (fireAt <= now)
                    continue;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"custom_rec_{i}", group)
                    .ForJob(Jobs.ReportExecutionJob.Key)
                    .UsingJobData("reportId", report.Id)
                    .StartAt(fireAt)
                    .WithSimpleSchedule(s => s.WithRepeatCount(0))
                    .Build();

                triggers.Add(trigger);
            }

            return triggers;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string TriggerGroupFor(int reportId) => $"report_{reportId}";

        private static (int hour, int minute) ParseTimeAsUtc(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return (8, 0); // 09:00 WAT = 08:00 UTC

            var parts = timeStr.Split(':');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out var h) &&
                int.TryParse(parts[1], out var m))
            {
                var localDt = new DateTime(2000, 1, 1,
                    Math.Clamp(h, 0, 23), Math.Clamp(m, 0, 59), 0,
                    DateTimeKind.Unspecified);

                var utcDt = TimeZoneInfo.ConvertTimeToUtc(localDt, AppTimeZone);
                return (utcDt.Hour, utcDt.Minute);
            }

            return (8, 0);
        }

        private class CustomRecurringEntry
        {
            public string Date { get; set; } = string.Empty;
            public string Time { get; set; } = string.Empty;
        }
    }
}