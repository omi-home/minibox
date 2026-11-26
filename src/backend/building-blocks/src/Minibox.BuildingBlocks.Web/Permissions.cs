using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Security;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Tầng 1 của kiểm quyền (spec §3.6): endpoint đòi người gọi đã đăng nhập và có mã quyền ở ít nhất một phạm vi
/// trong claim <c>perms</c>; thiếu → 403, chưa đăng nhập → 401. Phạm vi của bản ghi kiểm tiếp trong handler bằng
/// <see cref="AuthorizerExtensions.EnsureAsync"/>.</summary>
/// <param name="permission">Mã quyền <c>miền.tài-nguyên.hành-động</c>.</param>
/// <example><c>app.MapPost("/adjustments/{id}/approve", [RequirePermission("inventory.adjustment.approve")] (…) => …)</c></example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute, IAuthorizationRequirementData
{
    /// <summary>Mã quyền cần có.</summary>
    public string Permission { get; } = permission;

    /// <inheritdoc />
    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Permission)];
}

sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (PermsClaim.Read(context.User).Values.Any(codes => codes.Contains(requirement.Permission)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Tầng 2: quyền trong phạm vi của bản ghi — có ở phạm vi <c>*</c> hoặc đúng phạm vi đó.</summary>
sealed class ClaimsAuthorizer(IHttpContextAccessor accessor) : IAuthorizer
{
    public ValueTask<bool> HasPermissionAsync(string permission, string scope, CancellationToken cancellationToken = default)
    {
        var perms = PermsClaim.Read(accessor.HttpContext?.User);
        return ValueTask.FromResult(Has(perms, "*", permission) || Has(perms, scope, permission));
    }

    static bool Has(Dictionary<string, string[]> perms, string scope, string permission) =>
        perms.TryGetValue(scope, out var codes) && codes.Contains(permission);
}

static class PermsClaim
{
    /// <summary>Đọc claim <c>perms</c>; thiếu hoặc sai dạng → rỗng (fail-closed).</summary>
    public static Dictionary<string, string[]> Read(ClaimsPrincipal? user)
    {
        var json = user?.FindFirstValue(AccessTokenClaims.Permissions);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
