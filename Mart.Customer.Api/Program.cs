using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Middleware;
using Mart.Customer.Api.OpenApi;
using Mart.Customer.Application;
using Mart.Customer.Infrastructure;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/mart-customer-api-.log", rollingInterval: RollingInterval.Day);
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<AllowAnonymousOperationFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT only. Do not include the 'Bearer ' prefix."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (allowedCorsOrigins.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IMartUserContext, MartUserContext>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var signingKey = jwtSettings["SigningKey"] ?? "replace-this-development-key-with-a-secure-secret";
        var martSigningKey = jwtSettings["MartSigningKey"];
        var signingKeys = new[] { signingKey, martSigningKey }
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .Select(key => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)))
            .ToArray();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuers = new[] { jwtSettings["Issuer"], jwtSettings["MartIssuer"] }
                .Where(value => !string.IsNullOrWhiteSpace(value)),
            ValidAudiences = new[] { jwtSettings["Audience"], jwtSettings["MartAudience"] }
                .Where(value => !string.IsNullOrWhiteSpace(value)),
            IssuerSigningKeys = signingKeys,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(MartAuthorizationPolicies.MobileCustomer, policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim(MartTokenClaims.LoginType, "customer"));

    options.AddPolicy(MartAuthorizationPolicies.InternalUser, policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim(MartTokenClaims.LoginType, "internalUser"));

    options.AddPolicy(MartAuthorizationPolicies.MartAdmin, policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim(MartTokenClaims.LoginType, "internalUser")
            .RequireRole("MA"));

    options.AddPolicy(MartAuthorizationPolicies.FranchiseAdmin, policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim(MartTokenClaims.LoginType, "internalUser")
            .RequireRole(
                MartAuthorizationPolicies.FranchiseAdminRole,
                "FRENCHISE_ADMIN",
                "FRANCHISE ADMIN",
                "FRENCHISE ADMIN"));

    options.AddPolicy(MartAuthorizationPolicies.PendingPointsMartAdmin, policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim(
                MartTokenClaims.LegacyUserRole,
                MartAuthorizationPolicies.InternalUser));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddHealthChecks();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPersistence(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mart.Customer.Api v1");
        options.RoutePrefix = "swagger";
    });

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
