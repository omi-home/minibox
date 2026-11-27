using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Features.Assignments;
using Minibox.Identity.Application.Features.Assignments.Commands.GrantAssignment;
using Minibox.Identity.Application.Features.Assignments.Commands.RevokeAssignment;

namespace Minibox.Identity.Api.Endpoints.Assignments;

/// <summary>Thân request gán vai trò.</summary>
/// <param name="Role">Mã vai trò (<c>warehouse</c>, <c>qc</c>…).</param>
/// <param name="Scope">Phạm vi <c>*</c>, <c>wh:n</c> hoặc <c>farm:n</c>.</param>
public sealed record AssignmentRequest(string Role, string Scope);

/// <summary>Quản trị phân công (spec §3.6): mã quyền ở bất kỳ phạm vi để vào endpoint, phạm vi của phân công kiểm trong handler;
/// đổi quyền là thao tác nhạy cảm (step-up).</summary>
public static class AssignmentEndpoints
{
    /// <summary>Map <c>POST /admin/accounts/{accountId}/assignments</c> và <c>DELETE …/assignments/{role}/{scope}</c>.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/accounts/{accountId:guid}/assignments").WithTags("Assignments");
        group.MapPost("", [RequirePermission(AssignmentAccess.ManagePermission)] [Sensitive] async (
                Guid accountId, AssignmentRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GrantAssignment(accountId, body.Role, body.Scope), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        group.MapDelete("{role}/{scope}", [RequirePermission(AssignmentAccess.ManagePermission)] [Sensitive] async (
                Guid accountId, string role, string scope, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RevokeAssignment(accountId, role, scope), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
