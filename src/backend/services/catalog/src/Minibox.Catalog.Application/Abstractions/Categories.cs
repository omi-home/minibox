using Minibox.BuildingBlocks.Application;
using Minibox.Catalog.Application.Features.Categories;
using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Application.Abstractions;

/// <summary>Vị trí một danh mục trong cây, đọc khi đã khoá cây.</summary>
/// <param name="Id">Id.</param>
/// <param name="ParentId">Cha; <c>null</c> = gốc.</param>
/// <param name="Archived">Đã xóa mềm.</param>
public sealed record CategoryNode(Guid Id, Guid? ParentId, bool Archived);

/// <summary>Repository của <see cref="Category"/>.</summary>
public interface ICategoryRepository : IRepository<Category, Guid>
{
    /// <summary>Khoá tuần tự mọi thay đổi cấu trúc cây tới hết transaction rồi đọc vị trí của mọi danh mục.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mọi danh mục (kể cả đã xóa).</returns>
    /// <remarks>Khoá để hai lệnh song song (A vào B và B vào A, hoặc thêm con vào danh mục đang bị xóa) không cùng lọt kiểm.</remarks>
    Task<IReadOnlyList<CategoryNode>> LockTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>Kiểm có slug nào trong <paramref name="texts"/> đã được danh mục khác dùng ở cùng locale.</summary>
    /// <param name="categoryId">Danh mục đang ghi (bỏ qua slug của chính nó).</param>
    /// <param name="texts">Bản dịch sắp ghi.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>true</c> nếu trùng.</returns>
    Task<bool> AnySlugTakenAsync(Guid categoryId, IReadOnlyCollection<CategoryText> texts, CancellationToken cancellationToken = default);
}

/// <summary>Dòng bản dịch của danh mục đang hoạt động cho cây công khai.</summary>
/// <param name="Id">Id danh mục.</param>
/// <param name="ParentId">Cha.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Locale">Locale của bản dịch.</param>
/// <param name="Name">Tên.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug.</param>
public sealed record CategoryTextRow(Guid Id, Guid? ParentId, int SortOrder, string Locale, string Name, string? Description, string Slug);

/// <summary>Danh mục kèm mọi bản dịch cho trang quản trị.</summary>
/// <param name="Id">Id.</param>
/// <param name="ParentId">Cha.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Archived">Đã xóa mềm.</param>
/// <param name="Version">Phiên bản để gửi lại khi sửa.</param>
/// <param name="Translations">Bản dịch.</param>
public sealed record CategoryAdminRow(Guid Id, Guid? ParentId, int SortOrder, bool Archived, long Version, CategoryTranslationDto[] Translations);

/// <summary>Port đọc danh mục (phía query): Infrastructure chiếu thẳng ra DTO, không tải aggregate.</summary>
public interface ICategoryReadService
{
    /// <summary>Đọc mọi danh mục kèm mọi bản dịch.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Danh sách phẳng, kể cả danh mục đã xóa.</returns>
    Task<CategoryAdminRow[]> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Đọc bản dịch thuộc <paramref name="locales"/> của danh mục đang hoạt động.</summary>
    /// <param name="locales">Chuỗi fallback (<c>ILocaleReplica.ChainAsync</c>).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mọi dòng khớp (nhiều locale mỗi danh mục); chọn bản tốt nhất bằng <c>LocaleChain.PickPerKey</c>.</returns>
    Task<CategoryTextRow[]> ListActiveAsync(IReadOnlyList<string> locales, CancellationToken cancellationToken = default);
}
