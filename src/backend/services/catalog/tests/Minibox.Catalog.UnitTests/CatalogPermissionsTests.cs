using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Minibox.Catalog.Application;

namespace Minibox.Catalog.UnitTests;

/// <summary>Mã quyền trong <see cref="CatalogPermissions"/> phải được seed vào identity, nếu không command khai báo quyền luôn 403.</summary>
public sealed class CatalogPermissionsTests
{
    static readonly string[] Codes = typeof(CatalogPermissions).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (string)f.GetRawConstantValue()!).ToArray();

    static string IdentityMigrations([CallerFilePath] string here = "") =>
        string.Join('\n', Directory.GetFiles(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!,
            "../../../../../../database/schemas/identity/scripts/migrations")), "*.sql").Select(File.ReadAllText));

    [Fact(DisplayName = "Mọi mã quyền catalog có dạng catalog.<đối tượng>.<hành động> và được seed vào bảng permission của identity")]
    public void every_code_is_seeded()
    {
        var sql = IdentityMigrations();
        Assert.NotEmpty(Codes);
        foreach (var code in Codes)
        {
            Assert.Matches(@"^catalog\.[a-z]+\.[a-z]+$", code);
            Assert.Matches(new Regex($@"\('{Regex.Escape(code)}',\s*'"), sql);
        }
    }

    [Theory(DisplayName = "Vai trò Nội dung có quyền soạn và gửi duyệt nhưng không tự duyệt, không đặt giá")]
    [InlineData(CatalogPermissions.ProductManage, true)]
    [InlineData(CatalogPermissions.ProductSubmit, true)]
    [InlineData(CatalogPermissions.CategoryManage, true)]
    [InlineData(CatalogPermissions.CollectionManage, true)]
    [InlineData(CatalogPermissions.AttributeManage, true)]
    [InlineData(CatalogPermissions.FileManage, true)]
    [InlineData(CatalogPermissions.ProductApprove, false)]
    [InlineData(CatalogPermissions.PriceWrite, false)]
    public void content_role_assignment(string code, bool granted) =>
        Assert.Equal(granted, IdentityMigrations().Contains($"('content', '{code}')"));
}
