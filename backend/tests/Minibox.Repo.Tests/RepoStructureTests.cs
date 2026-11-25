namespace Minibox.Repo.Tests;

public class RepoStructureTests
{
    static string BackendDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Minibox.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy backend/Minibox.slnx");
    }

    // ponytail: chỉ kiểm phần backend của spec §8.1; phần còn lại thêm khi task tương ứng xong.
    [Theory(DisplayName = "ac174_1 backend có khung theo spec §8.1")]
    [InlineData("Minibox.slnx")]
    [InlineData("global.json")]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Packages.props")]
    [InlineData("src/building-blocks")]
    [InlineData("src/services")]
    public void ac174_1_backend_skeleton(string path)
    {
        var full = Path.Combine(BackendDir(), path);
        Assert.True(File.Exists(full) || Directory.Exists(full), $"Thiếu {path}");
    }
}
