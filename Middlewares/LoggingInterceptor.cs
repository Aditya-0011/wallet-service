using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Wallet.Middlewares;

public sealed class LoggingInterceptor(ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await continuation(request, context);
            sw.Stop();

            logger.LogInformation("gRPC success {method} {status} {durationMs}",
                context.Method,
                "OK",
                sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch (RpcException ex)
        {
            sw.Stop();
            var level = ex.StatusCode < StatusCode.Internal ? LogLevel.Warning : LogLevel.Error;

            logger.Log(level, ex, "gRPC error {method} {status} {durationMs} {error}",
                context.Method,
                ex.StatusCode.ToString(),
                sw.Elapsed.TotalMilliseconds,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "gRPC error {method} {status} {durationMs} {error}",
                context.Method,
                "Unknown",
                sw.Elapsed.TotalMilliseconds,
                ex.Message);
            throw;
        }
    }
}
