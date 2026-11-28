using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Minibox.BuildingBlocks.Application.Behaviors;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Đăng ký log, trace, metric dùng chung cho mọi service (spec §8.5).</summary>
public static class ObservabilitySetup
{
    /// <summary>Thay log mặc định bằng một dòng JSON mỗi bản ghi ra stdout (<see cref="MiniboxJsonFormatter"/>: có <c>trace_id</c>, đã che
    /// dữ liệu nhạy cảm) và bật OpenTelemetry cho trace (ASP.NET Core, HttpClient, <c>Minibox.Application</c>, Wolverine, Npgsql) và metric.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Trace qua RabbitMQ: Wolverine ghi <c>traceparent</c> vào header message và tiếp tục trace ở phía nhận.</item>
    /// <item>Trace/metric chỉ xuất OTLP khi có <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (Collector); log không đi OTLP mà Collector đọc stdout
    /// của container — một chỗ che dữ liệu duy nhất.</item>
    /// <item>Nhãn môi trường <c>deployment.environment</c> lấy từ <c>Minibox:Environment</c>, mặc định tên môi trường host.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">Host builder của service.</param>
    /// <param name="serviceName">Tên service (vd. <c>identity</c>), thành <c>service.name</c>.</param>
    /// <returns><paramref name="builder"/> để gọi tiếp.</returns>
    public static IHostApplicationBuilder AddMiniboxObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.FormatterName = MiniboxJsonFormatter.FormatterName);
        builder.Logging.AddConsoleFormatter<MiniboxJsonFormatter, ConsoleFormatterOptions>();

        var environment = builder.Configuration["Minibox:Environment"] ?? builder.Environment.EnvironmentName;
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName).AddAttributes([new("deployment.environment", environment.ToLowerInvariant())]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(ApplicationTelemetry.Source.Name, "Wolverine", "Npgsql"))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Wolverine"));
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();
        return builder;
    }
}

/// <summary>Console formatter in mỗi bản ghi log thành một dòng JSON: <c>ts</c>, <c>level</c>, <c>category</c>, <c>message</c>,
/// <c>trace_id</c>, <c>span_id</c>, <c>fields</c>, <c>exception</c>.</summary>
/// <remarks>Che theo <see cref="LogRedactor"/>: field có tên nhạy cảm (mật khẩu, token, OTP...) thành <c>***</c>, JWT trong chuỗi bị xoá,
/// email/số điện thoại che một phần. <c>message</c> được dựng lại từ template với giá trị đã che, không dùng chuỗi gốc.</remarks>
public sealed partial class MiniboxJsonFormatter() : ConsoleFormatter(FormatterName)
{
    /// <summary>Tên đăng ký formatter, đặt vào <see cref="ConsoleLoggerOptions.FormatterName"/>.</summary>
    public const string FormatterName = "minibox-json";

    const string OriginalFormat = "{OriginalFormat}";

    /// <inheritdoc/>
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var fields = new Dictionary<string, string?>();
        string? template = null;
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> pairs)
            foreach (var (key, value) in pairs)
                if (key == OriginalFormat) template = value?.ToString();
                else fields[key] = LogRedactor.Redact(key, value);

        var message = template is null
            ? LogRedactor.RedactText(logEntry.Formatter(logEntry.State, logEntry.Exception))
            : Placeholder().Replace(template, m => fields.TryGetValue(m.Groups[1].Value, out var v) ? v ?? "" : m.Value);

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("ts", DateTimeOffset.UtcNow);
            json.WriteString("level", logEntry.LogLevel.ToString());
            json.WriteString("category", logEntry.Category);
            json.WriteString("message", message);
            if (Activity.Current is { } activity)
            {
                json.WriteString("trace_id", activity.TraceId.ToHexString());
                json.WriteString("span_id", activity.SpanId.ToHexString());
            }
            if (logEntry.EventId.Id != 0) json.WriteNumber("event_id", logEntry.EventId.Id);
            if (fields.Count > 0)
            {
                json.WriteStartObject("fields");
                foreach (var (key, value) in fields) json.WriteString(key, value);
                json.WriteEndObject();
            }
            if (logEntry.Exception is { } ex) json.WriteString("exception", LogRedactor.RedactText(ex.ToString()));
            json.WriteEndObject();
        }
        textWriter.Write(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        textWriter.Write(Environment.NewLine);
    }

    // {Name} hoặc {Name:format}/{Name,align} trong message template.
    [GeneratedRegex(@"\{@?([A-Za-z0-9_]+)(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();
}

/// <summary>Luật che dữ liệu nhạy cảm trước khi ghi log (spec §8.5).</summary>
public static partial class LogRedactor
{
    const string Hidden = "***";

    /// <summary>Che giá trị của một field log.</summary>
    /// <param name="key">Tên field; tên chứa password/secret/token/otp/authorization/cookie/recoverycode/apikey thì che toàn bộ.</param>
    /// <param name="value">Giá trị gốc.</param>
    /// <returns><c>***</c> với field nhạy cảm; ngược lại chuỗi đã qua <see cref="RedactText"/>; <see langword="null"/> nếu giá trị null.</returns>
    public static string? Redact(string key, object? value) =>
        value is null ? null : SensitiveKey().IsMatch(key) ? Hidden : RedactText(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "");

    /// <summary>Che trong chuỗi tự do: xoá JWT và chuỗi Bearer, che email (giữ ký tự đầu + tên miền) và số điện thoại (giữ 3 số cuối).</summary>
    /// <param name="text">Chuỗi gốc.</param>
    /// <returns>Chuỗi đã che.</returns>
    public static string RedactText(string text)
    {
        text = Jwt().Replace(text, Hidden);
        text = Bearer().Replace(text, "Bearer " + Hidden);
        text = Email().Replace(text, m => $"{m.Groups[1].Value[0]}***@{m.Groups[2].Value}");
        return Phone().Replace(text, m => Hidden + m.Value[^3..]);
    }

    [GeneratedRegex("password|passwd|pwd|secret|token|otp|authorization|cookie|recoverycode|apikey", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveKey();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"([A-Za-z0-9._%+-]+)@([A-Za-z0-9.-]+\.[A-Za-z]{2,})")]
    private static partial Regex Email();

    // ponytail: số Việt Nam 0xxxxxxxxx / +84xxxxxxxxx; số nước ngoài định dạng khác chưa che — thêm khi có khách quốc tế.
    [GeneratedRegex(@"(?<!\d)(?:\+84|0)\d{9,10}(?!\d)")]
    private static partial Regex Phone();
}
