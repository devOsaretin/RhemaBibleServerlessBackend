using System.Globalization;
using System.Text;

public static class DashboardStatisticsCsvFormatter
{
    public static string ToCsv(DashboardStatisticsExportDto dto)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        void Row(string section, string key, object value)
        {
            sb.Append(Escape(section)).Append(',').Append(Escape(key)).Append(',')
                .Append(Escape(Convert.ToString(value, inv) ?? "")).AppendLine();
        }

        sb.AppendLine("section,key,value");

        Row("meta", "generatedAtUtc", dto.GeneratedAtUtc.ToString("o", inv));

        var o = dto.Overview;
        Row("overview", "totalUsers", o.TotalUsers);
        Row("overview", "activeUsers", o.ActiveUsers);
        Row("overview", "suspendedUsers", o.SuspendedUsers);
        Row("overview", "emailVerifiedUsers", o.EmailVerifiedUsers);
        Row("overview", "emailNotVerifiedUsers", o.EmailNotVerifiedUsers);

        var s = dto.Subscriptions;
        Row("subscriptions", "totalPremiumUsers", s.TotalPremiumUsers);
        Row("subscriptions", "totalFreeUsers", s.TotalFreeUsers);
        Row("subscriptions", "premiumMonthlyUsers", s.PremiumMonthlyUsers);
        Row("subscriptions", "premiumYearlyUsers", s.PremiumYearlyUsers);
        Row("subscriptions", "legacyPremiumUsers", s.LegacyPremiumUsers);
        Row("subscriptions", "premiumPercentageOfTotal", s.PremiumPercentageOfTotal);
        Row("subscriptions", "freePercentageOfTotal", s.FreePercentageOfTotal);

        var g = dto.Growth;
        Row("growth", "newUsersThisUtcMonth", g.NewUsersThisUtcMonth);
        Row("growth", "newUsersPreviousUtcMonth", g.NewUsersPreviousUtcMonth);
        Row("growth", "newUsersLast7Days", g.NewUsersLast7Days);
        Row("growth", "monthOverMonthNewUserPercentChange", g.MonthOverMonthNewUserPercentChange?.ToString(inv) ?? "");

        var c = dto.Content;
        Row("content", "totalNotes", c.TotalNotes);
        Row("content", "totalSavedVerses", c.TotalSavedVerses);

        var a = dto.AiFreeTier;
        Row("aiFreeTier", "currentUtcMonthKey", a.CurrentUtcMonthKey);
        Row("aiFreeTier", "freeCallsLimitPerMonth", a.FreeCallsLimitPerMonth);
        Row("aiFreeTier", "usersWithUsageTrackedThisMonth", a.UsersWithUsageTrackedThisMonth);
        Row("aiFreeTier", "totalFreeAiCallsUsedThisMonth", a.TotalFreeAiCallsUsedThisMonth);
        Row("aiFreeTier", "usersAtOrOverFreeLimitThisMonth", a.UsersAtOrOverFreeLimitThisMonth);

        var act = dto.Activity;
        Row("activity", "activitiesLast30Days", act.ActivitiesLast30Days);
        Row("activity", "aiAnalysisActivitiesLast30Days", act.AiAnalysisActivitiesLast30Days);
        Row("activity", "addNoteActivitiesLast30Days", act.AddNoteActivitiesLast30Days);
        Row("activity", "readBibleActivitiesLast30Days", act.ReadBibleActivitiesLast30Days);

        sb.AppendLine();
        sb.AppendLine("signupsByDay,utcDate,count");
        foreach (var day in dto.SignupsLast30DaysByUtcDay)
        {
            sb.Append("signupsLast30Days").Append(',').Append(Escape(day.UtcDateKey)).Append(',')
                .Append(day.Count).AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

