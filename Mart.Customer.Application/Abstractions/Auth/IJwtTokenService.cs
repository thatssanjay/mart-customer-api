using Mart.Customer.Application.Auth.Dtos;

namespace Mart.Customer.Application.Abstractions.Auth;

public interface IJwtTokenService
{
    AuthTokenDto GenerateToken(TokenSubject subject);
}
