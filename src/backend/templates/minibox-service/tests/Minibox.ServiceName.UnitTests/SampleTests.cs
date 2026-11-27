using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.ServiceName.Application.Features.Samples.Commands.CreateSample;
using Minibox.ServiceName.Domain.Aggregates.Samples;
using Minibox.ServiceName.Domain.Errors;

namespace Minibox.ServiceName.UnitTests;

/// <summary>Test đơn vị mẫu cho Domain (bất biến aggregate) và Application (validator, handler với fake repository). Thay bằng test thật.</summary>
public class SampleTests
{
    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory(DisplayName = "Sample.Create từ chối tên rỗng hoặc quá dài")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void create_rejects_invalid_name(string? name) =>
        Assert.Equal(SampleErrors.NameInvalid, Sample.Create(name!).Error);

    [Fact(DisplayName = "Sample.Create cắt khoảng trắng và sinh Id UUIDv7")]
    public void create_trims_and_uses_v7()
    {
        var sample = Sample.Create("  mẫu ").Value;
        Assert.Equal("mẫu", sample.Name);
        Assert.Equal(7, sample.Id.Version);
    }

    [Fact(DisplayName = "CreateSampleValidator chặn tên quá dài")]
    public void validator_rejects_too_long() =>
        Assert.False(new CreateSampleValidator().Validate(new CreateSample(new string('a', Sample.NameMaxLength + 1), "k")).IsValid);

    [Fact(DisplayName = "CreateSampleHandler thêm aggregate qua repository và trả Id")]
    public async Task handler_adds_and_returns_id()
    {
        var repo = new FakeRepository();
        var result = await new CreateSampleHandler(repo).Handle(new CreateSample("mẫu", "k"), Ct);
        Assert.Equal(result.Value, Assert.Single(repo.Added).Id);
    }

    sealed class FakeRepository : IRepository<Sample, Guid>
    {
        public List<Sample> Added { get; } = [];
        public Task<Sample?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Added.Find(s => s.Id == id));
        public Task<Sample?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
        public Task AddAsync(Sample aggregate, CancellationToken cancellationToken = default) { Added.Add(aggregate); return Task.CompletedTask; }
        public Task<Result> UpdateAsync(Sample aggregate, long expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    }
}
