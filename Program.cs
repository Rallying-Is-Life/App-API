using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Map existing env vars (Auth__*) into AzureAd config section expected by Identity.Web
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AzureAd:Instance"] = "https://login.microsoftonline.com",
    ["AzureAd:TenantId"] = Environment.GetEnvironmentVariable("Auth__TenantId") ?? "",
    ["AzureAd:ClientId"] = (Environment.GetEnvironmentVariable("Auth__Audience")
                            ?? Environment.GetEnvironmentVariable("Auth__ClientId") ?? ""),
    ["AzureAd:ClientSecret"] = Environment.GetEnvironmentVariable("Auth__ClientSecret") ?? "",
    ["AzureAd:CallbackPath"] = "/signin-oidc"
});

builder.Services.AddControllers();

builder.Services.AddAuthentication("AzureAd")
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), "AzureAd");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Read", policy => policy.AddRequirements(new App_API.Authorization.ScopePolicy("Api.Read")));
    options.AddPolicy("Write", policy => policy.AddRequirements(new App_API.Authorization.ScopePolicy("Api.Write")));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new App_API.Authorization.ScopePolicy("Api.Read"))
        .Build();
});
builder.Services.AddSingleton<IAuthorizationHandler, App_API.Authorization.ScopeHandler>();

builder.Services.AddCors(options => options.AddPolicy("AllowFE", p => p.WithOrigins(Environment.GetEnvironmentVariable("feRallyingIsLifeBaseURL") ?? "https://localhost:3000").AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFE");
app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
