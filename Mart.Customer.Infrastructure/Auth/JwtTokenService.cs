using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mart.Customer.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AuthTokenDto GenerateToken(TokenSubject subject)
    {
        var now = DateTime.UtcNow;
        var expiresOn = now.AddHours(_options.ExpiryHours <= 0 ? 24 : _options.ExpiryHours);
        var expiryText = $"{(int)(expiresOn - now).TotalHours} hours";
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, subject.UserId),
            new(ClaimTypes.Role, subject.Role),
            new(MartTokenClaims.UserId, subject.UserId),
            new(MartTokenClaims.Role, subject.Role),
            new("expText", expiryText),
            new(MartTokenClaims.LoginType, subject.LoginType)
        };

        if (!string.IsNullOrWhiteSpace(subject.Name))
        {
            claims.Add(new Claim(ClaimTypes.Name, subject.Name));
            if (!subject.Claims.ContainsKey(MartTokenClaims.UserName))
            {
                claims.Add(new Claim(MartTokenClaims.UserName, subject.Name));
            }
        }

        foreach (var claim in subject.Claims)
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now,
            expiresOn,
            credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthTokenDto(
            subject.UserId,
            subject.Name,
            subject.Role,
            expiryText,
            accessToken,
            "Bearer",
            expiresOn,
            (int)(expiresOn - now).TotalSeconds);
    }
}
