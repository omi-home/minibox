using System.Diagnostics;

namespace Minibox.Repo.Tests;

/// <summary>Sinh service từ template vào services/ (để dùng Directory.Build.props và building blocks thật), build + test, rồi xoá.</summary>
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

    [Fact(DisplayName = "ac174_5 ac193_3 dotnet new minibox-service -n Demo sinh 5 project + 3 project test, build và test pass")]
    public void ac174_5_ac193_3_template_generates_buildable_service()
    {
        var hive = Directory.CreateTempSubdirectory("minibox-hive").FullName; // không đụng danh sách template của máy
        var outDir = Path.Combine(Backend, "services", $"ac174-{Guid.NewGuid():N}"[..14]);
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

            Run(outDir, "new", "sln", "-n", "Demo", "--format", "slnx");
            Run(outDir, ["sln", "Demo.slnx", "add", .. Directory.GetFiles(outDir, "*.csproj", SearchOption.AllDirectories)]);
            Run(outDir, "test", "--solution", "Demo.slnx");
        }
        finally
        {
            TryDelete(outDir);
            TryDelete(hive);
        }
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
