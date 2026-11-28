using Mediator;
using Microsoft.AspNetCore.Mvc;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Web;
using Minibox.ServiceName.Application.Features.Samples.Commands.CreateSample;
using Minibox.ServiceName.Application.Features.Samples.Queries.ListSamples;

namespace Minibox.ServiceName.Api.Endpoints.Samples;

/// <summary>Body của <c>POST /samples</c>.</summary>
/// <param name="Name">Tên hiển thị.</param>
public sealed record CreateSampleRequest(string Name);

/// <summary>Endpoint mẫu (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class SampleEndpoints
{
    /// <summary>Map <c>POST /samples</c> (tạo, bắt buộc <c>Idempotency-Key</c>, trả Id) và <c>GET /samples</c> (danh sách).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/samples").WithTags("Samples");
        group.MapPost("", async (CreateSampleRequest request, [FromHeader(Name = IdempotencyKey.HeaderName)] string idempotencyKey,
                ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateSample(request.Name, idempotencyKey), ct)).ToHttpResult());
        group.MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListSamples(), ct)).ToHttpResult());
        return endpoints;
    }
}
