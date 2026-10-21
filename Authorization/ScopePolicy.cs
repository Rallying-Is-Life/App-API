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
        // scope claim is space-delimited, e.g. "Api.Write Api.Read"
        if (scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(requirement.Scope))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
