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

builder.Services.AddControllers();
// Auth middleware (selective — controllers use [Authorize] / [AllowAnonymous])
var tenantId = Environment.GetEnvironmentVariable("Auth__TenantId") ?? "";
var apiAudience = Environment.GetEnvironmentVariable("Auth__Audience") ?? Environment.GetEnvironmentVariable("Auth__ClientId") ?? "";
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o => {
        o.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        o.Audience = apiAudience;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters {
            ValidateAudience = true,
            ValidAudience = apiAudience,
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
            NameClaimType = "name",
        };
    });
builder.Services.AddAuthorization(options => {
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
