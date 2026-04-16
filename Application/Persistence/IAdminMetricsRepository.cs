namespace RhemaBibleAppServerless.Application.Persistence;

public interface IAdminMetricsRepository
{
  Task<DashboardAnalyticsDto> GetAnalyticsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
  Task<DashboardStatisticsRawData> GetStatisticsRawAsync(DateTime nowUtc, string aiMonthKeyUtc, int freeCallsLimitPerMonth, CancellationToken cancellationToken = default);
}
