using Minibox.Identity.Application.Accounts;
using Npgsql;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Đọc quyền hiệu lực từ <c>role_assignment</c> ⋈ <c>role_permission</c>.</summary>
/// <param name="dataSource">Kết nối role <c>identity_app</c>.</param>
public sealed class PostgresPermissions(NpgsqlDataSource dataSource) : IPermissions
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT ra.scope, array_agg(DISTINCT rp.permission_code COLLATE "C" ORDER BY rp.permission_code COLLATE "C")
            FROM role_assignment ra JOIN role_permission rp ON rp.role_id = ra.role_id
            WHERE ra.account_id = $1
            GROUP BY ra.scope
            """);
        cmd.Parameters.Add(new() { Value = accountId });
        var result = new Dictionary<string, IReadOnlyCollection<string>>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetString(0)] = reader.GetFieldValue<string[]>(1);
        return result;
    }
}
