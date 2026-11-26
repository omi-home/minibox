using System.Reflection;
using System.Text.Json;
using Dapper;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Kho <c>Idempotency-Key</c> trong bảng <c>idempotency_key</c> của schema service (mẫu
/// <c>database/templates/idempotency-key.sql</c>), ghi trên transaction của <see cref="NpgsqlUnitOfWork"/>.</summary>
/// <remarks>Giữ chỗ bằng <c>INSERT … ON CONFLICT DO NOTHING</c>: request trùng khoá đang chạy song song bị PostgreSQL bắt chờ tới khi
/// request trước commit (→ đọc kết quả của nó) hoặc rollback (→ giữ chỗ được). Chỉ lưu giá trị của kết quả thành công (thường là id).</remarks>
/// <param name="unitOfWork">Unit of Work của command (đã mở transaction).</param>
public sealed class PostgresIdempotencyStore(NpgsqlUnitOfWork unitOfWork) : IIdempotencyStore
{
    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Bản ghi đã commit nhưng thiếu kết quả (dữ liệu hỏng).</exception>
    public async ValueTask<IdempotencyRecord<TResponse>?> ReserveAsync<TResponse>(string key, string requestHash, CancellationToken cancellationToken = default)
        where TResponse : Result
    {
        var reserved = await unitOfWork.Connection.ExecuteAsync(Command(
            "INSERT INTO idempotency_key (key, request_hash) VALUES (@key, @requestHash) ON CONFLICT (key) DO NOTHING",
            new { key, requestHash }, cancellationToken));
        if (reserved == 1) return null;

        var row = await unitOfWork.Connection.QuerySingleAsync<(string RequestHash, string? Response)>(Command(
            "SELECT request_hash, response::text FROM idempotency_key WHERE key = @key", new { key }, cancellationToken));
        return new(row.RequestHash, SuccessJson<TResponse>.Read(
            row.Response ?? throw new InvalidOperationException($"Khoá idempotency '{key}' không có kết quả.")));
    }

    /// <inheritdoc/>
    public async ValueTask CompleteAsync<TResponse>(string key, TResponse response, CancellationToken cancellationToken = default) where TResponse : Result =>
        await unitOfWork.Connection.ExecuteAsync(Command(
            "UPDATE idempotency_key SET response = @json::jsonb WHERE key = @key",
            new { key, json = SuccessJson<TResponse>.Write(response) }, cancellationToken));

    CommandDefinition Command(string sql, object parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, unitOfWork.Transaction, cancellationToken: cancellationToken);

    /// <summary>JSON của kết quả thành công: <c>{}</c> cho <see cref="Result"/>, giá trị cho <see cref="Result{T}"/>.</summary>
    static class SuccessJson<TResponse> where TResponse : Result
    {
        static readonly Type? ValueType = typeof(TResponse) == typeof(Result) ? null : typeof(TResponse).GetGenericArguments()[0];

        public static string Write(TResponse response) => ValueType is null
            ? "{}"
            : JsonSerializer.Serialize(typeof(TResponse).GetProperty(nameof(Result<object>.Value))!.GetValue(response), ValueType);

        public static TResponse Read(string json) => ValueType is null
            ? (TResponse)Result.Success()
            : (TResponse)typeof(TResponse).GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [ValueType])!
                .Invoke(null, [JsonSerializer.Deserialize(json, ValueType)])!;
    }
}
