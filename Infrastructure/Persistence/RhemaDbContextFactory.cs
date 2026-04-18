using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RhemaBibleAppServerless.Infrastructure.Persistence;

public sealed class RhemaDbContextFactory : IDesignTimeDbContextFactory<RhemaDbContext>
{
  public RhemaDbContext CreateDbContext(string[] args)
  {
    var connectionString = ResolveDesignTimeConnectionString();

    var options = new DbContextOptionsBuilder<RhemaDbContext>()
      .UseNpgsql(connectionString)
      .UseSnakeCaseNamingConvention()
      .Options;

    return new RhemaDbContext(options);
  }

  private static string ResolveDesignTimeConnectionString()
  {
    var fromEnv = Environment.GetEnvironmentVariable("RHEMA_POSTGRES_CONNECTION");
    if (!string.IsNullOrWhiteSpace(fromEnv))
      return fromEnv;

    var fromLocalSettings = TryReadPostgresConnectionFromLocalSettings();
    if (!string.IsNullOrWhiteSpace(fromLocalSettings))
      return fromLocalSettings;

    throw new InvalidOperationException(
      "Design-time PostgreSQL connection not configured. Add Postgres__ConnectionString under Values in Api.Functions/local.settings.json, " +
      "or set environment variable RHEMA_POSTGRES_CONNECTION.");
  }

  private static string? TryReadPostgresConnectionFromLocalSettings()
  {
    foreach (var settingsPath in EnumerateLocalSettingsCandidates())
    {
      if (!File.Exists(settingsPath))
        continue;

      try
      {
        using var stream = File.OpenRead(settingsPath);
        using var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("Values", out var values))
          continue;
        if (!values.TryGetProperty("Postgres__ConnectionString", out var el))
          continue;
        var s = el.GetString();
        if (!string.IsNullOrWhiteSpace(s))
          return s;
      }
      catch (JsonException)
      {
        // ignore invalid JSON and try other paths
      }
    }

    return null;
  }

  private static IEnumerable<string> EnumerateLocalSettingsCandidates()
  {
    foreach (var root in EnumerateSearchRoots())
    {
      yield return Path.Combine(root, "Api.Functions", "local.settings.json");
      yield return Path.Combine(root, "local.settings.json");
    }
  }

  private static IEnumerable<string> EnumerateSearchRoots()
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      var dir = start;
      for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
      {
        var full = Path.GetFullPath(dir);
        if (seen.Add(full))
          yield return full;

        dir = Directory.GetParent(dir)?.FullName;
      }
    }
  }
}
