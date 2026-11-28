using Minibox.BuildingBlocks.Infrastructure.Localization;
using Minibox.Localization.Contracts.Events;
using Wolverine.Util;

namespace Minibox.Localization.IntegrationTests;

/// <summary>#396: consumer bản sao locale ở building block khai lại record; tên message Wolverine và các field phải khớp
/// <see cref="LocaleChangedV1"/> của localization để service khác nhận được sự kiện.</summary>
public sealed class LocaleChangedConsumerTests
{
    [Fact(DisplayName = "LocaleChangedMessage của building block cùng tên message Wolverine và cùng field với LocaleChangedV1")]
    public void Consumer_record_matches_contract()
    {
        Assert.Equal(typeof(LocaleChangedV1).ToMessageTypeName(), typeof(LocaleChangedMessage).ToMessageTypeName());
        static IEnumerable<(string, Type)> Fields(Type t) => t.GetProperties().Select(p => (p.Name, p.PropertyType)).Order();
        Assert.Equal(Fields(typeof(LocaleChangedV1)), Fields(typeof(LocaleChangedMessage)));
    }
}
