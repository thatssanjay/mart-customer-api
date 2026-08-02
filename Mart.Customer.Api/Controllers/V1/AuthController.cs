using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Auth;
using Mart.Customer.Application.Auth.Commands.GenerateCustomerToken;
using Mart.Customer.Application.Auth.Commands.GenerateInternalUserToken;
using Mart.Customer.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> GenerateToken(GenerateTokenRequest request, CancellationToken cancellationToken)
    {
        var token = request.LoginType.Trim().ToLowerInvariant() switch
        {
            "customer" or "mobile" => await _sender.Send(
                new GenerateCustomerTokenCommand(request.MobileNumber ?? string.Empty, request.Otp ?? string.Empty),
                cancellationToken),
            "internaluser" or "internal-user" or "user" or "web" => await _sender.Send(
                new GenerateInternalUserTokenCommand(request.UserId ?? string.Empty, request.Password ?? string.Empty),
                cancellationToken),
            _ => throw new DomainException("Invalid login type.")
        };

        return Ok(token);
    }

    [Authorize]
    [HttpGet("token/validate")]
    public IActionResult ValidateToken([FromServices] IMartUserContext currentUser)
    {
        return Ok(new
        {
            isValid = true,
            userId = currentUser.UserId,
            franchiseId = currentUser.FranchiseId,
            storeId = currentUser.StoreId,
            userName = currentUser.UserName,
            role = currentUser.Role
        });
    }
}
