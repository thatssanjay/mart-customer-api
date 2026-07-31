using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpValidator, ConfiguredOtpValidator>();
        services.AddScoped<IPasswordVerifier, Pbkdf2PasswordVerifier>();

        return services;
    }
}
