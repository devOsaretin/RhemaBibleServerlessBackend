using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class InvocationLoggingPipeline
{
  /// <summary>
  /// Registers outermost worker middleware: ambient invocation id, logging scope, and telemetry for uncaught exceptions.
  /// </summary>
  public static FunctionsApplicationBuilder AddInvocationLogging(this FunctionsApplicationBuilder builder)
  {
    builder.Use(next =>
    {
      return async context =>
      {
        InvocationAmbientContext.Set(context);
        var logger = context.InstanceServices
          .GetRequiredService<ILoggerFactory>()
          .CreateLogger("AzureFunctions.Invocation");

        using (logger.BeginScope(new Dictionary<string, object?>
        {
          ["InvocationId"] = context.InvocationId,
          ["FunctionName"] = context.FunctionDefinition.Name
        }))
        {
          try
          {
            await next(context);
          }
          catch (Exception ex)
          {
            logger.LogError(
              ex,
              "Unhandled exception escaping function {FunctionName}; InvocationId={InvocationId}",
              context.FunctionDefinition.Name,
              context.InvocationId);
            throw;
          }
          finally
          {
            InvocationAmbientContext.Clear();
          }
        }
      };
    });

    return builder;
  }
}
