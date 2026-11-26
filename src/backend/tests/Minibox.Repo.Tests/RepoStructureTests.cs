namespace Minibox.Repo.Tests;

public class RepoStructureTests
{
    internal static string BackendDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Minibox.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy src/backend/Minibox.slnx");
    }

    static string RepoDir() => Path.GetFullPath(Path.Combine(BackendDir(), "..", ".."));

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    // ponytail: chỉ kiểm phần đã dựng; thêm database/, contracts/, deploy/, scripts/ khi task tương ứng xong.
    [Theory(DisplayName = "ac174_1 repo có khung theo spec §8.1")]
    [InlineData("src/backend/Minibox.slnx")]
    [InlineData("src/backend/global.json")]
    [InlineData("src/backend/Directory.Build.props")]
    [InlineData("src/backend/Directory.Packages.props")]
    [InlineData("src/backend/building-blocks")]
    [InlineData("src/backend/services")]
    [InlineData("src/frontend/package.json")]
    [InlineData("src/frontend/pnpm-workspace.yaml")]
    [InlineData("src/frontend/pnpm-lock.yaml")]
    [InlineData("docs")]
    public void ac174_1_repo_skeleton(string path) =>
        Assert.True(Exists(Path.Combine(RepoDir(), path)), $"Thiếu {path}");

    [Theory(DisplayName = "ac174_1 root sạch: không có file của frontend/backend")]
    [InlineData("package.json")]
    [InlineData("pnpm-lock.yaml")]
    [InlineData("pnpm-workspace.yaml")]
    [InlineData("node_modules")]
    [InlineData("frontend")]
    [InlineData("backend")]
    public void ac174_1_root_clean(string path) =>
        Assert.False(Exists(Path.Combine(RepoDir(), path)), $"Root không được có {path}");
}
