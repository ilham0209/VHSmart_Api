using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using VHSmart_Api.Shared.Infrastructure.Behavior;
using VHSmart_Api.Shared.Infrastructure.Notifications;
using VHSmart_Api.Shared.Infrastructure.Persistence;
using VHSmart_Api.Shared.Infrastructure.Security;
using VHSmart_Api.Shared.Infrastructure.Seeding;
using VHSmart_Api.Shared.Infrastructure.Sequences;
using VHSmart_Api.Shared.Infrastructure.Storage;
using VHSmart_Api.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as their spec value in JSON ("COMPANY"), never as a number (CodingRules 11).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddDbContext<VHSmartDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VHSmart")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, JwtCurrentUser>();

// Issues the D-29 claims at login / Switch Company; scoped like the rest of the request
// services (it reads configuration only, never a DbContext).
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Shared services (CodingRules 2): both are scoped because they write through the request's
// VHSmartDbContext (F-08).
builder.Services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Relative FileStorage:RootPath is resolved against the content root; the storage root is
// created lazily on the first upload.
var fileStorageRoot = builder.Configuration["FileStorage:RootPath"];
if (string.IsNullOrWhiteSpace(fileStorageRoot))
    fileStorageRoot = "App_Data/Files";
if (!Path.IsPathRooted(fileStorageRoot))
    fileStorageRoot = Path.Combine(builder.Environment.ContentRootPath, fileStorageRoot);
builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(fileStorageRoot));

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // No key configured yet = every token is rejected (fail closed), never a hard-coded fallback key (D-29).
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    });
// AddAuthorization only TryAdds the default policy provider, so ours goes in first.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
// Reads AdmRolePermissions (A-01, D-19); the matrix is cached per role and invalidated when a
// role is edited.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionService, RolePermissionService>();
builder.Services.AddAuthorization(options =>
{
    // Endpoints with no explicit policy still need a signed-in user (CodingRules 8.2).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(signingKey))
{
    app.Logger.LogWarning(
        "Jwt:SigningKey is not configured, so issued tokens will be rejected. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" <key>");
}

// Database.md 14: first platform admin via an explicit command (password from the secret
// store). Runs before the pipeline and never serves a request.
if (args is [PlatformAdminSeeder.Command, ..])
{
    var seedResult = await PlatformAdminSeeder.RunAsync(app.Services);
    if (seedResult.Success)
        app.Logger.LogInformation("{Message}", seedResult.Message);
    else
        app.Logger.LogError("{Message}", seedResult.Message);
    Environment.ExitCode = seedResult.Success ? 0 : 1;
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "VH-Smart API";
        options.ShowSidebar = true;
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseCors("CorsPolicy");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Probes are not data endpoints: without AllowAnonymous the fallback policy answers 401.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
