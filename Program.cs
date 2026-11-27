using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Map existing env vars (Auth__*) into AzureAd config section expected by Identity.Web
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AzureAd:Instance"] = "https://login.microsoftonline.com",
    // Read from deployed setting names (ClientID / ClientSecret) with Auth__* fallback
    ["AzureAd:TenantId"] = (Environment.GetEnvironmentVariable("TenantID") ?? Environment.GetEnvironmentVariable("Auth__TenantId") ?? ""),
    ["AzureAd:ClientId"] = (Environment.GetEnvironmentVariable("ClientID") ?? Environment.GetEnvironmentVariable("Auth__ClientId") ?? Environment.GetEnvironmentVariable("Auth__Audience") ?? ""),
    ["AzureAd:ClientSecret"] = (Environment.GetEnvironmentVariable("ClientSecret") ?? Environment.GetEnvironmentVariable("Auth__ClientSecret") ?? "")
});

// CIAM (Entra External ID) support
// If CiamTenantName is set, use the CIAM authority and issuer; else use regular Entra ID
var tenantId = Environment.GetEnvironmentVariable("TenantID") ?? Environment.GetEnvironmentVariable("Auth__TenantId") ?? Environment.GetEnvironmentVariable("AzureAd__TenantId") ?? "";
var ciamTenantName = Environment.GetEnvironmentVariable("CiamTenantName") ?? "";
var apiAudiences = new[]
{
    Environment.GetEnvironmentVariable("Auth__Audience"),
    Environment.GetEnvironmentVariable("Auth__ClientId"),
    Environment.GetEnvironmentVariable("ClientID")
}
.Where(audience => !string.IsNullOrWhiteSpace(audience))
.Cast<string>()
.Distinct(StringComparer.OrdinalIgnoreCase)
.ToArray();

// Build the metadata authority and token issuer for CIAM vs regular Entra ID.
// CIAM discovery uses the tenant-name host; CIAM access tokens use the tenant-ID host as issuer.
var authority = string.IsNullOrEmpty(ciamTenantName)
    ? $"https://login.microsoftonline.com/{tenantId}/v2.0"
    : $"https://{ciamTenantName}.ciamlogin.com/{tenantId}/v2.0";

var validIssuer = string.IsNullOrEmpty(ciamTenantName)
    ? authority
    : $"https://{tenantId}.ciamlogin.com/{tenantId}/v2.0";

builder.Services.AddControllers();
// Auth middleware (selective — controllers use [Authorize] / [AllowAnonymous])
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = authority;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = apiAudiences,
            ValidateIssuer = true,
            ValidIssuer = validIssuer,
            NameClaimType = "name",
        };
    });
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();