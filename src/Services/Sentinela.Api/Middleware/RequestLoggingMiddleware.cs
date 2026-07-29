using System.Diagnostics;

namespace Sentinela.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        
        _logger.LogInformation("HTTP {Method} {Path} from {RemoteIp}", 
            request.Method, request.Path, context.Connection.RemoteIpAddress);
        
        await _next(context);
        
        stopwatch.Stop();
        _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            request.Method, request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
}
