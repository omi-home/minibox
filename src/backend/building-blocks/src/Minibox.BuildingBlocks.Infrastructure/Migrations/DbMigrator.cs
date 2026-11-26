using System.Reflection;
using DbUp;

namespace Minibox.BuildingBlocks.Infrastructure.Migrations;

/// <summary>Bộ chạy migration SQL thuần của service bằng DbUp (spec §4.4).</summary>
/// <remarks>
/// Script nhúng vào assembly của service với LogicalName <c>migrations/NNNN_mo-ta.sql</c>, chạy theo thứ tự tên,
/// mỗi script một transaction. Journal ở <c>&lt;schema&gt;.schema_versions</c>; script đã chạy không chạy lại (idempotent).
/// </remarks>
public static class DbMigrator
{
    /// <summary>Tiền tố LogicalName của script migration nhúng trong assembly.</summary>
    public const string ResourcePrefix = "migrations/";

    /// <summary>Cờ dòng lệnh bật chế độ migrate của service.</summary>
    public const string MigrateFlag = "--migrate";

    /// <summary>Áp dụng các script chưa chạy lên schema của service.</summary>
    /// <param name="connectionString">Kết nối bằng role <c>&lt;svc&gt;_migrator</c> (chủ schema).</param>
    /// <param name="schema">Schema của service, chứa journal <c>schema_versions</c>.</param>
    /// <param name="scripts">Assembly chứa script nhúng.</param>
    /// <param name="include">Lọc thêm theo tên script (mặc định: mọi script dưới <see cref="ResourcePrefix"/>).</param>
    /// <returns><c>true</c> nếu mọi script thành công; <c>false</c> nếu một script lỗi (script đó và các script sau không được ghi journal).</returns>
    public static bool Migrate(string connectionString, string schema, Assembly scripts, Func<string, bool>? include = null)
    {
        var result = DeployChanges.To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(scripts, n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && (include?.Invoke(n) ?? true))
            .JournalToPostgresqlTable(schema, "schema_versions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade();
        if (!result.Successful) Console.Error.WriteLine($"Migrate {schema} lỗi ở {result.ErrorScript?.Name}: {result.Error?.Message}");
        return result.Successful;
    }

    /// <summary>Chạy migrate rồi trả exit code nếu tham số có <see cref="MigrateFlag"/>; dùng ở đầu <c>Program.cs</c> của service.</summary>
    /// <param name="args">Tham số dòng lệnh của process.</param>
    /// <param name="schema">Schema của service.</param>
    /// <param name="scripts">Assembly chứa script nhúng.</param>
    /// <param name="connectionString">Lấy chuỗi kết nối migrator; chỉ gọi khi có cờ.</param>
    /// <returns><c>null</c> nếu không có cờ (service chạy bình thường); 0 nếu migrate thành công, 1 nếu lỗi.</returns>
    /// <example><code>if (DbMigrator.RunIfRequested(args, "order", typeof(Program).Assembly, () => cfg.GetConnectionString("Migrator")!) is int code) return code;</code></example>
    public static int? RunIfRequested(string[] args, string schema, Assembly scripts, Func<string> connectionString) =>
        args.Contains(MigrateFlag) ? (Migrate(connectionString(), schema, scripts) ? 0 : 1) : null;
}
