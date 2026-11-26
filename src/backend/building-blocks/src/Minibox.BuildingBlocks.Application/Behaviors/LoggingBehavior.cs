using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary>Behavior ngoài cùng: span trace + log kết quả, thời gian xử lý của mỗi message.</summary>
/// <remarks>Thứ tự đăng ký trong <c>AddMediator</c> (spec §5.4): <see cref="LoggingBehavior{TMessage, TResponse}"/>,
/// <see cref="ValidationBehavior{TMessage, TResponse}"/>, <see cref="AuthorizationBehavior{TMessage, TResponse}"/>,
/// <see cref="IdempotencyBehavior{TMessage, TResponse}"/>, <see cref="TransactionBehavior{TMessage, TResponse}"/>.</remarks>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="logger">Logger.</param>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TMessage).Name;
        using var activity = ApplicationTelemetry.Source.StartActivity(name);
        var start = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(message, cancellationToken);
            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (response.IsSuccess)
                LogSuccess(name, ms);
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, response.Error.Code);
                LogFailure(name, response.Error.Code, ms);
            }
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            LogException(ex, name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(LogLevel.Information, "{Message} thành công sau {ElapsedMs:F1} ms")]
    partial void LogSuccess(string message, double elapsedMs);

    [LoggerMessage(LogLevel.Warning, "{Message} thất bại {ErrorCode} sau {ElapsedMs:F1} ms")]
    partial void LogFailure(string message, string errorCode, double elapsedMs);

    [LoggerMessage(LogLevel.Error, "{Message} lỗi hạ tầng sau {ElapsedMs:F1} ms")]
    partial void LogException(Exception exception, string message, double elapsedMs);
}

/// <summary>Telemetry của tầng Application.</summary>
public static class ApplicationTelemetry
{
    /// <summary>Nguồn span <c>Minibox.Application</c>, đăng ký với OpenTelemetry.</summary>
    public static readonly ActivitySource Source = new("Minibox.Application");
}
