using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.ServiceName.Domain.Aggregates.Samples;

namespace Minibox.ServiceName.Application.Features.Samples.Commands.CreateSample;

/// <summary>Lệnh tạo <see cref="Sample"/>; cần quyền <c>servicename.sample.create</c>, gửi lặp cùng <c>Idempotency-Key</c> trả lại Id lần đầu.</summary>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="IdempotencyKey">Khoá do client sinh (header <c>Idempotency-Key</c>).</param>
public sealed record CreateSample(string Name, string IdempotencyKey) : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => "servicename.sample.create";
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction (lỗi trả 400, không ghi gì).</summary>
public sealed class CreateSampleValidator : AbstractValidator<CreateSample>
{
    /// <summary>Khai báo luật: tên bắt buộc, tối đa <see cref="Sample.NameMaxLength"/> ký tự.</summary>
    public CreateSampleValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(Sample.NameMaxLength);
}

/// <summary>Tạo aggregate rồi thêm qua repository; transaction, audit, outbox và idempotency do pipeline lo.</summary>
/// <param name="samples">Repository của <see cref="Sample"/>.</param>
public sealed class CreateSampleHandler(IRepository<Sample, Guid> samples) : ICommandHandler<CreateSample, Guid>
{
    /// <inheritdoc/>
    public async ValueTask<Result<Guid>> Handle(CreateSample command, CancellationToken cancellationToken)
    {
        var sample = Sample.Create(command.Name);
        if (sample.IsFailure) return sample.Error;
        await samples.AddAsync(sample.Value, cancellationToken);
        return sample.Value.Id;
    }
}
