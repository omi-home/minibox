using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;

namespace Minibox.Catalog.Application.Features.Taxa.Queries.GetTaxa;

/// <summary>Truy vấn công khai bộ sưu tập/thẻ đang hoạt động theo locale (không cần đăng nhập); bản dịch theo chuỗi fallback, thiếu hết dùng <c>vi</c>.</summary>
/// <param name="Kind">Loại: <c>collection</c> hoặc <c>tag</c>.</param>
/// <param name="Locale">Locale của request.</param>
public sealed record GetTaxa(string Kind, string Locale) : IQuery<TaxonView[]>;

/// <summary>Bộ sưu tập/thẻ công khai.</summary>
/// <param name="Id">Id.</param>
/// <param name="Locale">Locale của bản dịch đang hiển thị; khác locale yêu cầu là fallback (trang đặt <c>noindex</c>).</param>
/// <param name="Name">Tên.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug theo <paramref name="Locale"/>.</param>
public sealed record TaxonView(Guid Id, string Locale, string Name, string? Description, string Slug);

/// <summary>Kiểm loại và locale.</summary>
public sealed class GetTaxaValidator : AbstractValidator<GetTaxa>
{
    /// <summary>Khai báo luật: loại hợp lệ; locale không rỗng, tối đa 35 ký tự, dạng BCP 47.</summary>
    public GetTaxaValidator()
    {
        RuleFor(x => x.Kind).TaxonKind();
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(35).Matches(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*\z");
    }
}

/// <summary>Dựng chuỗi fallback, đọc bản dịch, chọn bản tốt nhất mỗi taxon, sắp theo thứ tự rồi tên.</summary>
/// <param name="locales">Bản sao locale.</param>
/// <param name="reader">Port đọc.</param>
public sealed class GetTaxaHandler(ILocaleReplica locales, ITaxonReadService reader) : IQueryHandler<GetTaxa, TaxonView[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<TaxonView[]>> Handle(GetTaxa query, CancellationToken cancellationToken)
    {
        var chain = await locales.ChainAsync(query.Locale, cancellationToken);
        var best = LocaleChain.PickPerKey(await reader.ListActiveAsync(query.Kind, chain, cancellationToken), r => r.Id, r => r.Locale, chain);
        return best.Values.OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .Select(r => new TaxonView(r.Id, r.Locale, r.Name, r.Description, r.Slug)).ToArray();
    }
}
