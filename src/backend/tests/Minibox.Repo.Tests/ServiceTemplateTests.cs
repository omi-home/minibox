using System.Diagnostics;

namespace Minibox.Repo.Tests;

/// <summary>Sinh service từ template vào repo tạm ngoài workspace (Directory.* và building blocks chép từ repo thật), build + test, rồi xoá.</summary>
public class ServiceTemplateTests
{
    static readonly string Backend = RepoStructureTests.BackendDir();

    static void Run(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // dotnet test của repo đặt biến MSBuild/test host cho tiến trình con; xoá để lệnh lồng chạy như ở terminal.
        foreach (var key in psi.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase) || k.StartsWith("TESTINGPLATFORM", StringComparison.OrdinalIgnoreCase)).ToList())
            psi.Environment.Remove(key);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"dotnet {string.Join(' ', args)} → {p.ExitCode}\n{stdout.Result}\n{stderr.Result}");
    }

    [Fact(DisplayName = "ac174_5 ac193_3 ac455_1 dotnet new minibox-service -n Demo sinh 5 project + 3 project test theo khung §5.2, build và test pass")]
    public void ac174_5_ac193_3_ac455_1_template_generates_buildable_service()
    {
        var hive = Directory.CreateTempSubdirectory("minibox-hive").FullName; // không đụng danh sách template của máy
        var repo = Directory.CreateTempSubdirectory("minibox-template").FullName;
        var outDir = Path.Combine(MirrorBackend(repo), "services", "demo");
        try
        {
            Run(Backend, "new", "install", Path.Combine(Backend, "templates", "minibox-service"), "--debug:custom-hive", hive);
            Run(Backend, "new", "minibox-service", "-n", "Demo", "-o", outDir, "--debug:custom-hive", hive);

            var projects = Directory.GetFiles(outDir, "*.csproj", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension).Order().ToArray();
            Assert.Equal(
                new[] { "Api", "Application", "ArchitectureTests", "Contracts", "Domain", "Infrastructure", "IntegrationTests", "UnitTests" }
                    .Select(n => $"Minibox.Demo.{n}"),
                projects);

            // Khung bên trong layer spec §5.2 + migration hạ tầng (audit, Idempotency-Key, outbox Wolverine) cho pipeline Mediator.
            foreach (var path in new[]
            {
                "src/Minibox.Demo.Domain/Aggregates/Samples/Sample.cs",
                "src/Minibox.Demo.Domain/Errors/SampleErrors.cs",
                "src/Minibox.Demo.Application/Abstractions/ISampleReadService.cs",
                "src/Minibox.Demo.Application/Features/Samples/Commands/CreateSample/CreateSample.cs",
                "src/Minibox.Demo.Application/Features/Samples/Queries/ListSamples/ListSamples.cs",
                "src/Minibox.Demo.Infrastructure/Persistence/Configurations/SampleConfiguration.cs",
                "src/Minibox.Demo.Api/Endpoints/Samples/SampleEndpoints.cs",
                "tests/Minibox.Demo.ArchitectureTests/StructureRulesTests.cs",
                "../../../../database/schemas/demo/scripts/migrations/0002_audit-log.sql",
                "../../../../database/schemas/demo/scripts/migrations/0003_idempotency-key.sql",
                "../../../../database/schemas/demo/scripts/migrations/0004_wolverine.sql",
            })
                Assert.True(File.Exists(Path.Combine(outDir, path)), $"Thiếu {path}");

            Run(outDir, "new", "sln", "-n", "Demo", "--format", "slnx");
            Run(outDir, ["sln", "Demo.slnx", "add", .. Directory.GetFiles(outDir, "*.csproj", SearchOption.AllDirectories)]);
            Run(outDir, "test", "--solution", "Demo.slnx");
            Assert.True(File.Exists(Path.Combine(repo, "contracts", "openapi", "demo.json")), "build service mới phải sinh contracts/openapi/demo.json");
        }
        finally
        {
            TryDelete(repo);
            TryDelete(hive);
        }
    }

    /// Sinh service ngoài workspace (IDE không thấy csproj mới nên không build lại obj/ sau khi xoá): dựng repo tạm có
    /// src/backend với Directory.*, global.json và building-blocks/src thật (không bin/obj), giữ đúng đường dẫn tương đối của template.
    static string MirrorBackend(string repo)
    {
        var backend = Path.Combine(repo, "src", "backend");
        Directory.CreateDirectory(backend);
        foreach (var file in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json" })
            File.Copy(Path.Combine(Backend, file), Path.Combine(backend, file));
        var source = Path.Combine(Backend, "building-blocks", "src");
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                continue;
            var target = Path.Combine(backend, "building-blocks", "src", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return backend;
    }

    // Windows: IDE/build server có thể giữ handle thư mục vừa build vài giây; dọn dẹp không được làm hỏng kết quả test.
    static void TryDelete(string dir)
    {
        for (var i = 0; Directory.Exists(dir); i++)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) when (i < 10) { Thread.Sleep(1000); }
            catch (IOException) { return; }
        }
    }
}
