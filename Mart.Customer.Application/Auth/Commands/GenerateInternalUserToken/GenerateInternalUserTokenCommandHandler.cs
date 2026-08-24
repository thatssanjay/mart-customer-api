using System.Globalization;
using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Shared.Auth;
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

        if (user.FranchiseId is null or <= 0 || user.StoreId is null or <= 0)
        {
            throw new DomainException("The user does not have an active franchise and store assignment.");
        }

        return _jwtTokenService.GenerateToken(
            new TokenSubject(
                user.UserId,
                user.DisplayName,
                user.Role,
                "internalUser",
                new Dictionary<string, string>
                {
                    [MartTokenClaims.UserName] = user.Username,
                    [MartTokenClaims.FranchiseId] = user.FranchiseId.Value.ToString(CultureInfo.InvariantCulture),
                    [MartTokenClaims.StoreId] = user.StoreId.Value.ToString(CultureInfo.InvariantCulture)
                }));
    }
}
