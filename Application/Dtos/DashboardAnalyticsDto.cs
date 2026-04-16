


public record DashboardAnalyticsDto(
    long TotalUsers,
    long TotalPremiumUsers,
    long TotalFreeUsers,
    long ActiveUsers,
    long NewUsersThisMonth,
    long PremiumMonthlyUsers,
    long PremiumYearlyUsers,
    long LegacyPremiumUsers
    )
{
    public double PremiumPercentage => TotalUsers > 0 ? (double)TotalPremiumUsers / TotalUsers * 100 : 0;
    public double ActivePercentage => TotalUsers > 0 ? (double)ActiveUsers / TotalUsers * 100 : 0;
    public long InactiveUsers => TotalUsers - ActiveUsers;
    
    public double PremiumMonthlyPercentage => TotalPremiumUsers > 0 
        ? (double)PremiumMonthlyUsers / TotalPremiumUsers * 100 
        : 0;
    
    public double PremiumYearlyPercentage => TotalPremiumUsers > 0 
        ? (double)PremiumYearlyUsers / TotalPremiumUsers * 100 
        : 0;
}