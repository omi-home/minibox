using System.Text.Json;
using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Accounts;
using Npgsql;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Phân công trên <c>role_assignment</c>; audit vào <c>audit_log</c> (append-only) cùng transaction.</summary>
/// <param name="dataSource">Kết nối role <c>identity_app</c>.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Khoá dòng <c>account</c> (<c>FOR UPDATE</c>) để ảnh chụp trước/sau của hai thay đổi song song không chồng nhau.</remarks>
public sealed partial class PostgresRoleAssignments(NpgsqlDataSource dataSource, TimeProvider time) : IRoleAssignments
{
    static readonly Error NotFound = Error.NotFound(IRoleAssignments.NotFoundCode, "Không tìm thấy tài khoản, vai trò hoặc phân công.");

    [GeneratedRegex(@"\A(\*|(wh|farm):[1-9][0-9]{0,18})\z")]
    private static partial Regex ScopePattern();

    sealed record Item(string Role, string Scope);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>?> PermissionsOfRoleAsync(string roleCode, CancellationToken cancellationToken = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT array_remove(array_agg(rp.permission_code), NULL)
            FROM role r LEFT JOIN role_permission rp ON rp.role_id = r.id WHERE r.code = $1 GROUP BY r.id
            """);
        cmd.Parameters.Add(new() { Value = roleCode });
        return await cmd.ExecuteScalarAsync(cancellationToken) as string[];
    }

    /// <inheritdoc />
    public async Task<Result> GrantAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default)
    {
        if (!ScopePattern().IsMatch(scope))
            return Error.Validation(IRoleAssignments.InvalidCode, "Phạm vi phải là *, wh:<id> hoặc farm:<id>.");
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var accountKind = await ScalarAsync<string>(conn, tx, "SELECT kind FROM account WHERE id = $1 FOR UPDATE", cancellationToken, accountId);
        Guid roleId;
        string roleKind;
        await using (var cmd = new NpgsqlCommand("SELECT id, kind FROM role WHERE code = $1", conn, tx) { Parameters = { new() { Value = roleCode } } })
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (accountKind is null || !await r.ReadAsync(cancellationToken))
                return NotFound;
            (roleId, roleKind) = (r.GetGuid(0), r.GetString(1));
        }
        if (roleKind != accountKind)
            return Error.Validation(IRoleAssignments.InvalidCode, "Vai trò không áp dụng cho loại tài khoản này.");

        var before = await SnapshotAsync(conn, tx, accountId, cancellationToken);
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO role_assignment (account_id, role_id, scope, granted_by) VALUES ($1, $2, $3, $4) ON CONFLICT DO NOTHING
            """, conn, tx) { Parameters = { new() { Value = accountId }, new() { Value = roleId }, new() { Value = scope }, new() { Value = actor } } })
            if (await insert.ExecuteNonQueryAsync(cancellationToken) == 0)
                return Result.Success(); // đã có: không đổi quyền, không audit

        await AuditAsync(conn, tx, actor, "RoleAssignmentGranted", accountId, roleCode, scope, before, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        if (await ScalarAsync<Guid?>(conn, tx, "SELECT id FROM account WHERE id = $1 FOR UPDATE", cancellationToken, accountId) is null)
            return NotFound;

        var before = await SnapshotAsync(conn, tx, accountId, cancellationToken);
        await using (var delete = new NpgsqlCommand("""
            DELETE FROM role_assignment WHERE account_id = $1 AND scope = $3 AND role_id = (SELECT id FROM role WHERE code = $2)
            """, conn, tx) { Parameters = { new() { Value = accountId }, new() { Value = roleCode }, new() { Value = scope } } })
            if (await delete.ExecuteNonQueryAsync(cancellationToken) == 0)
                return NotFound;

        await AuditAsync(conn, tx, actor, "RoleAssignmentRevoked", accountId, roleCode, scope, before, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result.Success();
    }

    async Task AuditAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid actor, string action, Guid accountId, string role, string scope,
        List<Item> before, CancellationToken ct)
    {
        var after = await SnapshotAsync(conn, tx, accountId, ct);
        var now = time.GetUtcNow();
        var payload = JsonSerializer.Serialize(new { role, scope, before, after }, JsonSerializerOptions.Web);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO audit_log (id, occurred_at, actor, action, aggregate_type, aggregate_id, payload)
            VALUES ($1, $2, $3, $4, 'Account', $5, $6::jsonb)
            """, conn, tx)
        {
            Parameters =
            {
                new() { Value = Guid.CreateVersion7(now) }, new() { Value = now }, new() { Value = actor.ToString() },
                new() { Value = action }, new() { Value = accountId.ToString() }, new() { Value = payload },
            },
        };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    static async Task<List<Item>> SnapshotAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid accountId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT r.code, ra.scope FROM role_assignment ra JOIN role r ON r.id = ra.role_id
            WHERE ra.account_id = $1 ORDER BY r.code COLLATE "C", ra.scope COLLATE "C"
            """, conn, tx) { Parameters = { new() { Value = accountId } } };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var items = new List<Item>();
        while (await r.ReadAsync(ct)) items.Add(new Item(r.GetString(0), r.GetString(1)));
        return items;
    }

    static async Task<T?> ScalarAsync<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, CancellationToken ct, object value)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx) { Parameters = { new() { Value = value } } };
        return await cmd.ExecuteScalarAsync(ct) is T v ? v : default;
    }
}
