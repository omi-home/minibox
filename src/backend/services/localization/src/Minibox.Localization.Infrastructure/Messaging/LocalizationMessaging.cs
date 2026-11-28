using Minibox.BuildingBlocks.Infrastructure.Localization;
using Minibox.Localization.Contracts.Events;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Minibox.Localization.Infrastructure.Messaging;

/// <summary>Định tuyến integration event của localization ra RabbitMQ.</summary>
public static class LocalizationMessaging
{
    /// <summary>Exchange fanout nhận <c>localization.MessagesPublished.v1</c>; BFF shop/admin mỗi tiến trình bind một queue tạm vào đây.</summary>
    /// <remarks>Tên và kiểu (fanout, durable) khớp <c>MESSAGES_PUBLISHED_EXCHANGE</c> ở <c>@minibox/api-clients/events</c>; đổi một bên phải đổi bên kia.</remarks>
    public const string MessagesPublishedExchange = "localization.MessagesPublished.v1";

    /// <summary>Phát <see cref="MessagesPublishedV1"/> và <see cref="LocaleChangedV1"/> qua outbox tới exchange fanout tương ứng
    /// (<see cref="MessagesPublishedExchange"/>, <see cref="LocaleReplicaSetup.LocaleChangedExchange"/>).</summary>
    /// <param name="options">Tuỳ chọn Wolverine đã gọi <c>UseMiniboxMessaging</c>.</param>
    public static void PublishLocalizationEvents(this WolverineOptions options)
    {
        options.PublishMessage<MessagesPublishedV1>().ToRabbitExchange(MessagesPublishedExchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.IsDurable = true;
        });
        options.PublishMessage<LocaleChangedV1>().ToRabbitExchange(LocaleReplicaSetup.LocaleChangedExchange, e =>
        {
            e.ExchangeType = ExchangeType.Fanout;
            e.IsDurable = true;
        });
    }
}
