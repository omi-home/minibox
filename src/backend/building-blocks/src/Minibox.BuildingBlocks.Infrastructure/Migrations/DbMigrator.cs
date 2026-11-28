using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Support;

namespace Minibox.BuildingBlocks.Infrastructure.Migrations;

/// <summary>Bộ chạy script SQL thuần của service bằng DbUp (spec §4.4).</summary>
/// <remarks>
/// Script nhúng vào assembly của service với LogicalName giữ đường dẫn dưới <c>database/schemas/&lt;svc&gt;/</c>:
/// <c>scripts/pre-deployment/</c> → <c>scripts/migrations/</c> → <c>scripts/post-deployment/</c>, trong mỗi nhóm theo tên,
/// mỗi script một transaction. Journal ở <c>&lt;schema&gt;.schema_versions</c> chỉ ghi script <c>Migrations/</c> (chạy một lần);
/// Pre/Post-Deployment chạy mọi lần nên phải idempotent.
/// </remarks>
public static class DbMigrator
{
    /// <summary>Tiền tố LogicalName của script chạy mọi lần, trước migration.</summary>
    public const string PreDeploymentPrefix = "scripts/pre-deployment/";

    /// <summary>Tiền tố LogicalName của script migration (chạy một lần theo journal).</summary>
    public const string MigrationsPrefix = "scripts/migrations/";

    /// <summary>Tiền tố LogicalName của script chạy mọi lần, sau migration.</summary>
    public const string PostDeploymentPrefix = "scripts/post-deployment/";

    /// <summary>Cờ dòng lệnh bật chế độ migrate của service.</summary>
    public const string MigrateFlag = "--migrate";

    /// <summary>Chạy Pre-Deployment, các migration chưa chạy rồi Post-Deployment lên schema của service.</summary>
    /// <param name="connectionString">Kết nối bằng role <c>&lt;svc&gt;_migrator</c> (chủ schema).</param>
    /// <param name="schema">Schema của service, chứa journal <c>schema_versions</c>.</param>
    /// <param name="scripts">Assembly chứa script nhúng.</param>
    /// <param name="include">Lọc thêm theo tên script (mặc định: mọi script dưới ba tiền tố).</param>
    /// <returns><c>true</c> nếu mọi script thành công; <c>false</c> nếu một script lỗi (script đó và các script sau không chạy/không ghi journal).</returns>
    public static bool Migrate(string connectionString, string schema, Assembly scripts, Func<string, bool>? include = null)
    {
        Func<string, bool> Under(string prefix) => n => n.StartsWith(prefix, StringComparison.Ordinal) && (include?.Invoke(n) ?? true);
        var result = DeployChanges.To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(scripts, Under(PreDeploymentPrefix), new SqlScriptOptions { ScriptType = ScriptType.RunAlways, RunGroupOrder = 1 })
            .WithScriptsEmbeddedInAssembly(scripts, Under(MigrationsPrefix), new SqlScriptOptions { ScriptType = ScriptType.RunOnce, RunGroupOrder = 2 })
            .WithScriptsEmbeddedInAssembly(scripts, Under(PostDeploymentPrefix), new SqlScriptOptions { ScriptType = ScriptType.RunAlways, RunGroupOrder = 3 })
            .JournalToPostgresqlTable(schema, "schema_versions")
            .WithTransactionPerScript()
            // Không dùng biến $name$ của DbUp: chúng nuốt dollar-quote của PostgreSQL ($body$, $tran$ trong DDL Wolverine).
            .WithVariablesDisabled()
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
    /// <remarks>
    /// Chuỗi kết nối migrator lấy từ biến môi trường do job deploy cấp từ secret, không đặt trong appsettings và không dùng cho runtime.
    /// Lỗi Npgsql chỉ in ra log của job migrate. <paramref name="schema"/> luôn là hằng số trong code, không lấy từ input/config.
    /// </remarks>
    /// <example><code>if (DbMigrator.RunIfRequested(args, "order", typeof(Program).Assembly, () => Environment.GetEnvironmentVariable("MINIBOX_MIGRATOR_CONNECTION")!) is int code) return code;</code></example>
    public static int? RunIfRequested(string[] args, string schema, Assembly scripts, Func<string> connectionString) =>
        args.Contains(MigrateFlag) ? (Migrate(connectionString(), schema, scripts) ? 0 : 1) : null;
}
