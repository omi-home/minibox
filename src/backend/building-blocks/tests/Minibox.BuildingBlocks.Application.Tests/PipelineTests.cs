using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Tests;

// Message + handler mẫu; source generator của Mediator sinh pipeline thật cho chúng.
public sealed record CreateItem(string Name, string? Key = null) : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    public string IdempotencyKey { get; } = Key ?? Guid.NewGuid().ToString("N");
    public string Permission => "item.create";
}

public sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class CreateItemHandler(Log log) : ICommandHandler<CreateItem, Guid>
{
    public ValueTask<Result<Guid>> Handle(CreateItem command, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return command.Name switch
        {
            "boom" => throw new InvalidOperationException("lỗi sau khi ghi"),
            "reject" => ValueTask.FromResult<Result<Guid>>(Error.Failure("Item.Rejected", "từ chối")),
            _ => ValueTask.FromResult<Result<Guid>>(Guid.NewGuid()),
        };
    }
}

public sealed record DeleteItem(Guid Id) : ICommand;

public sealed class DeleteItemHandler(Log log) : ICommandHandler<DeleteItem>
{
    public ValueTask<Result> Handle(DeleteItem command, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed record GetItem(Guid Id) : IQuery<string>;

public sealed class GetItemHandler(Log log) : IQueryHandler<GetItem, string>
{
    public ValueTask<Result<string>> Handle(GetItem query, CancellationToken cancellationToken)
    {
        log.Add("handler");
        return ValueTask.FromResult<Result<string>>("item");
    }
}

public sealed class Log : List<string>;

sealed class FakeUnitOfWork(Log log) : IUnitOfWork
{
    public Task BeginAsync(CancellationToken cancellationToken = default) { log.Add("begin"); return Task.CompletedTask; }
    public Task CommitAsync(CancellationToken cancellationToken = default) { log.Add("commit"); return Task.CompletedTask; }
    public Task RollbackAsync(CancellationToken cancellationToken = default) { log.Add("rollback"); return Task.CompletedTask; }
}

sealed class FakeUser : ICurrentUser
{
    public string? UserId { get; set; } = "u1";
}

sealed class FakeAuthorizer : IAuthorizer
{
    public bool Allow { get; set; } = true;
    public ValueTask<bool> HasPermissionAsync(string permission, string scope, CancellationToken cancellationToken = default) => ValueTask.FromResult(Allow);
}

sealed class MemoryIdempotencyStore : IIdempotencyStore
{
    readonly Dictionary<string, Result> _items = [];
    public ValueTask<TResponse?> GetAsync<TResponse>(string key, CancellationToken cancellationToken = default) where TResponse : Result =>
        ValueTask.FromResult(_items.TryGetValue(key, out var r) ? (TResponse)r : null);
    public ValueTask SaveAsync<TResponse>(string key, TResponse response, CancellationToken cancellationToken = default) where TResponse : Result
    {
        _items[key] = response;
        return ValueTask.CompletedTask;
    }
}

public sealed class PipelineTests
{
    const string K1 = "key-0000000000000001";
    const string K2 = "key-0000000000000002";
    readonly Log _log = [];
    readonly FakeUser _user = new();
    readonly FakeAuthorizer _authorizer = new();
    readonly IMediator _mediator;

    public PipelineTests()
    {
        var services = new ServiceCollection()
            .AddSingleton(_log)
            .AddSingleton<ICurrentUser>(_user)
            .AddSingleton<IAuthorizer>(_authorizer)
            .AddSingleton<IUnitOfWork, FakeUnitOfWork>()
            .AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>()
            .AddSingleton<IValidator<CreateItem>, CreateItemValidator>()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Singleton;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(AuthorizationBehavior<,>),
                typeof(IdempotencyBehavior<,>),
                typeof(TransactionBehavior<,>),
            ];
        });
        _mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    ValueTask<TResponse> Send<TResponse>(Mediator.ICommand<TResponse> command) => _mediator.Send(command, TestContext.Current.CancellationToken);

    ValueTask<TResponse> Send<TResponse>(Mediator.IQuery<TResponse> query) => _mediator.Send(query, TestContext.Current.CancellationToken);

    [Fact(DisplayName = "ac187_1: command lỗi validation trả lỗi Validation, không gọi handler, không mở transaction")]
    public async Task ac187_1_validation_failure_short_circuits()
    {
        var result = await Send(new CreateItem(""));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(ValidationBehavior<CreateItem, Result<Guid>>.ErrorCode, result.Error.Code);
        Assert.Empty(_log);
    }

    [Fact(DisplayName = "ac187_2: exception sau khi ghi → rollback, không commit, exception lan ra ngoài")]
    public async Task ac187_2_exception_rolls_back()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Send(new CreateItem("boom")));
        Assert.Equal(["begin", "handler", "rollback"], _log);
    }

    [Fact(DisplayName = "ac187_2: Result thất bại → rollback, không commit")]
    public async Task ac187_2_failure_result_rolls_back()
    {
        var result = await Send(new CreateItem("reject"));

        Assert.Equal("Item.Rejected", result.Error.Code);
        Assert.Equal(["begin", "handler", "rollback"], _log);
    }

    [Fact(DisplayName = "Command thành công → begin, handler, commit (cả ICommand không giá trị)")]
    public async Task success_commits()
    {
        Assert.True((await Send(new CreateItem("ok"))).IsSuccess);
        Assert.True((await Send(new DeleteItem(Guid.NewGuid()))).IsSuccess);
        Assert.Equal(["begin", "handler", "commit", "begin", "handler", "commit"], _log);
    }

    [Fact(DisplayName = "Query không mở transaction")]
    public async Task query_skips_transaction()
    {
        Assert.Equal("item", (await Send(new GetItem(Guid.NewGuid()))).Value);
        Assert.Equal(["handler"], _log);
    }

    [Fact(DisplayName = "Thiếu quyền → Forbidden, không gọi handler")]
    public async Task forbidden_short_circuits()
    {
        _authorizer.Allow = false;
        var result = await Send(new CreateItem("ok"));

        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Empty(_log);
    }

    [Fact(DisplayName = "ac187_5: gửi lặp cùng Idempotency-Key trả kết quả lần đầu, handler chạy một lần")]
    public async Task ac187_5_same_key_returns_first_result()
    {
        var first = await Send(new CreateItem("ok", K1));
        var second = await Send(new CreateItem("ok", K1));

        Assert.Equal(first.Value, second.Value);
        Assert.Equal(["begin", "handler", "commit"], _log);
    }

    [Fact(DisplayName = "Idempotency-Key gắn người gọi; kết quả thất bại không lưu")]
    public async Task idempotency_scoped_by_user_and_skips_failures()
    {
        var first = await Send(new CreateItem("ok", K1));
        _user.UserId = "u2";
        var other = await Send(new CreateItem("ok", K1));
        Assert.NotEqual(first.Value, other.Value);

        await Send(new CreateItem("reject", K2));
        await Send(new CreateItem("reject", K2));
        Assert.Equal(4, _log.Count(x => x == "handler"));
    }

    [Theory(DisplayName = "Idempotency-Key rỗng/ngắn/có khoảng trắng → Validation, không gọi handler")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("key 0000000000000001")]
    public async Task invalid_idempotency_key_rejected(string key)
    {
        var result = await Send(new CreateItem("ok", key));

        Assert.Equal(IdempotencyBehavior<CreateItem, Result<Guid>>.InvalidKeyCode, result.Error.Code);
        Assert.Empty(_log);
    }
}
