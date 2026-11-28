using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Minibox.BuildingBlocks.Application.Behaviors;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Minibox.BuildingBlocks.Web.Tests;

public sealed record TracePing(Guid Id);

public static class TracePingHandler
{
    public static readonly ConcurrentDictionary<Guid, ActivityTraceId> Seen = [];

    public static void Handle(TracePing ping) => Seen[ping.Id] = Activity.Current?.TraceId ?? default;
}

/// <summary>Spec §8.5: log JSON có trace_id, không chứa mật khẩu/token/OTP, che một phần email/số điện thoại; trace nối qua RabbitMQ.</summary>
public sealed class ObservabilityTests
{
    static JsonElement Format<TState>(TState state, Func<TState, Exception?, string> formatter, Exception? ex = null)
    {
        var writer = new StringWriter();
        new MiniboxJsonFormatter().Write(new LogEntry<TState>(LogLevel.Information, "Test.Category", new EventId(7), state, ex, formatter), null, writer);
        return JsonDocument.Parse(writer.ToString()).RootElement;
    }

    static JsonElement Log(string template, params object?[] args)
    {
        // Cùng đường đi với ILogger.LogInformation(template, args): state là FormattedLogValues.
        JsonElement result = default;
        var logger = new CapturingLogger(json => result = json);
        logger.LogInformation(template, args);
        return result;
    }

    sealed class CapturingLogger(Action<JsonElement> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink(Format(state, formatter, exception));
    }

    [Fact(DisplayName = "ac224_2 log JSON có trace_id/span_id của Activity hiện tại")]
    public void ac224_2_json_log_has_trace_id()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var json = Log("Đăng nhập {AccountId}", Guid.Empty);
        Assert.Equal(activity.TraceId.ToHexString(), json.GetProperty("trace_id").GetString());
        Assert.Equal(activity.SpanId.ToHexString(), json.GetProperty("span_id").GetString());
        Assert.Equal("Information", json.GetProperty("level").GetString());
        Assert.Equal("Test.Category", json.GetProperty("category").GetString());
        Assert.Equal($"Đăng nhập {Guid.Empty}", json.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "ac224_2 log không chứa mật khẩu, token, OTP (cả trong message và field)")]
    public void ac224_2_secrets_are_redacted()
    {
        const string jwt = "eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl";
        var json = Log("Login {Password} {RefreshToken} {Otp} {TotpCode} {Authorization} {Note}",
            "hunter2-secret", "rt-0123456789abcdef", "123456", "654321", "Bearer abc", $"token {jwt}");
        var text = json.GetRawText();
        foreach (var leaked in new[] { "hunter2-secret", "rt-0123456789abcdef", "123456", "654321", "Bearer abc", jwt })
            Assert.DoesNotContain(leaked, text);
        Assert.Equal("***", json.GetProperty("fields").GetProperty("Password").GetString());
    }

    [Fact(DisplayName = "ac224_2 log che một phần email và số điện thoại")]
    public void ac224_2_pii_is_masked()
    {
        var json = Log("Gửi mã tới {Email} và {Phone}", "nguyen.van.a@example.com", "0912345678");
        var text = json.GetRawText();
        Assert.DoesNotContain("nguyen.van.a@example.com", text);
        Assert.DoesNotContain("0912345678", text);
        Assert.Contains("n***@example.com", text);
        Assert.Contains("***678", text);
    }

    [Fact(DisplayName = "ac224_2 exception trong log cũng được che")]
    public void ac224_2_exception_is_redacted()
    {
        JsonElement json = default;
        new CapturingLogger(j => json = j).LogError(new InvalidOperationException("sai cho a.b@example.com"), "Lỗi");
        Assert.Contains("InvalidOperationException", json.GetProperty("exception").GetString());
        Assert.DoesNotContain("a.b@example.com", json.GetRawText());
    }

    [Fact(DisplayName = "ac224_2 AddMiniboxObservability dùng console formatter JSON có che dữ liệu")]
    public void ac224_2_host_uses_redacting_formatter()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMiniboxObservability("demo");
        using var host = builder.Build();
        Assert.Contains(host.Services.GetServices<ILoggerProvider>(), p => p is ConsoleLoggerProvider);
        Assert.Equal(MiniboxJsonFormatter.FormatterName, host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);
        Assert.NotNull(host.Services.GetService<TracerProvider>());
    }

    [Fact(DisplayName = "ac224_1 trace nối liền qua RabbitMQ: handler ở phía nhận cùng trace_id với phía gửi")]
    public async Task ac224_1_trace_propagates_over_rabbitmq()
    {
        await using var mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
        await mq.StartAsync(TestContext.Current.CancellationToken);
        var spans = new List<Activity>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMiniboxObservability("demo");
        builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddInMemoryExporter(spans));
        builder.UseWolverine(o =>
        {
            o.UseRabbitMq(new Uri(mq.GetConnectionString())).AutoProvision();
            o.PublishMessage<TracePing>().ToRabbitQueue("trace-ping");
            o.ListenToRabbitQueue("trace-ping");
            o.Discovery.DisableConventionalDiscovery().IncludeType(typeof(TracePingHandler));
        });
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var id = Guid.NewGuid();
        ActivityTraceId sent;
        using (var root = ApplicationTelemetry.Source.StartActivity("RegisterAccount"))
        {
            Assert.NotNull(root); // nguồn Minibox.Application đã được đăng ký với OpenTelemetry
            sent = root.TraceId;
            await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new TracePing(id));
        }

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!TracePingHandler.Seen.ContainsKey(id) && DateTime.UtcNow < deadline)
            await Task.Delay(100, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(sent, TracePingHandler.Seen[id]);
        Assert.Contains(spans, s => s.TraceId == sent && s.Source.Name == "Wolverine"); // span Wolverine được xuất cùng trace
    }
}
