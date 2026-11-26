using Microsoft.AspNetCore.Authorization;
namespace App_API.Authorization;
public class ScopePolicy : IAuthorizationRequirement
{
    public string Scope { get; }
    public ScopePolicy(string scope) => Scope = scope;
}
public class ScopeHandler : AuthorizationHandler<ScopePolicy>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopePolicy requirement)
    {
        var scopeClaim = context.User.FindFirst("scp")?.Value ?? "";
        var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Match bare scope (Api.Read) or prefixed scope (api://rallyingislife-api/Api.Read)
        if (scopes.Any(s => s == requirement.Scope || s.EndsWith("/" + requirement.Scope, StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
