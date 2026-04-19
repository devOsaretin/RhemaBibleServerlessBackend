using Microsoft.Azure.Functions.Worker;

/// <summary>
/// Per-invocation identifiers set by worker middleware so HTTP helpers can correlate logs and responses.
/// </summary>
internal static class InvocationAmbientContext
{
  private static readonly AsyncLocal<Snapshot?> Current = new();

  public static void Set(FunctionContext context)
  {
    Current.Value = new Snapshot(context.InvocationId, context.FunctionDefinition.Name);
  }

  public static void Clear() => Current.Value = null;

  public static string? InvocationId => Current.Value?.InvocationId;

  public static string? FunctionName => Current.Value?.FunctionName;

  private sealed record Snapshot(string InvocationId, string FunctionName);
}
