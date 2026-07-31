using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Auth.Commands.GenerateInternalUserToken;

public sealed class GenerateInternalUserTokenCommandHandler : IRequestHandler<GenerateInternalUserTokenCommand, AuthTokenDto>
{
    private readonly IInternalUserRepository _internalUserRepository;
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly IJwtTokenService _jwtTokenService;

    public GenerateInternalUserTokenCommandHandler(
        IInternalUserRepository internalUserRepository,
        IPasswordVerifier passwordVerifier,
        IJwtTokenService jwtTokenService)
    {
        _internalUserRepository = internalUserRepository;
        _passwordVerifier = passwordVerifier;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthTokenDto> Handle(GenerateInternalUserTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _internalUserRepository.GetByLoginIdAsync(request.UserId, cancellationToken);
        if (user is null || !_passwordVerifier.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new DomainException("Invalid user id or password.");
        }

        return _jwtTokenService.GenerateToken(
            new TokenSubject(
                user.UserId,
                user.DisplayName,
                user.Role,
                "internalUser",
                new Dictionary<string, string>
                {
                    ["username"] = user.Username
                }));
    }
}
