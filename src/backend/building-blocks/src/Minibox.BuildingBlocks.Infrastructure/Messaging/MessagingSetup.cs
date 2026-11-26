using JasperFx;
using JasperFx.CodeGeneration;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

namespace Minibox.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Cấu hình Wolverine chung cho mọi service (spec §2.1).</summary>
public static class MessagingSetup
{
    /// <summary>Bật outbox/inbox bền trong schema của service và RabbitMQ.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Bảng Wolverine do migration DbUp tạo; runtime <c>AutoCreate.None</c> nên role app chỉ cần DML.</item>
    /// <item>Mọi listener dùng inbox bền: message trùng id chỉ được xử lý một lần; mọi endpoint gửi dùng outbox bền.</item>
    /// <item>Production nạp code sinh sẵn (<c>TypeLoadMode.Static</c>, chạy <c>codegen write</c> lúc build image, không kéo Roslyn);
    /// Development sinh code lúc chạy (cần <c>WolverineFx.RuntimeCompilation</c>).</item>
    /// </list>
    /// </remarks>
    /// <param name="options">Tuỳ chọn Wolverine.</param>
    /// <param name="connectionString">Chuỗi kết nối role app, lấy từ secret (không để trong appsettings).</param>
    /// <param name="schema">Schema của service; hằng số, không lấy từ input.</param>
    /// <param name="rabbitMq">URI RabbitMQ (credential từ secret).</param>
    /// <returns>Cấu hình RabbitMQ để khai báo tiếp queue/exchange.</returns>
    public static RabbitMqTransportExpression UseMiniboxMessaging(this WolverineOptions options, string connectionString, string schema, Uri rabbitMq)
    {
        options.PersistMessagesWithPostgresql(connectionString, schema);
        options.AutoBuildMessageStorageOnStartup = AutoCreate.None;
        options.Policies.UseDurableInboxOnAllListeners();
        options.Policies.UseDurableOutboxOnAllSendingEndpoints();
        options.Services.CritterStackDefaults(x =>
        {
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
            x.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
        });
        return options.UseRabbitMq(rabbitMq).AutoProvision();
    }
}
