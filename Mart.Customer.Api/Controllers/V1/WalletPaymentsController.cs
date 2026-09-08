using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Payments;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Payments.Services;
using Mart.Customer.Shared.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/wallet-payments")]
public sealed class WalletPaymentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;
    private readonly IWalletPaymentRequestService _walletPayments;

    public WalletPaymentsController(
        ISender sender,
        IMartUserContext currentUser,
        IWalletPaymentRequestService walletPayments)
    {
        _sender = sender;
        _currentUser = currentUser;
        _walletPayments = walletPayments;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateWalletPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CartNumber) || request.CartNumber.Length > 50)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["cartNumber"] = ["Cart number is required and cannot exceed 50 characters."]
            }));
        }

        var access = await GetAccessAsync(cancellationToken);
        var payment = await _walletPayments.CreateAsync(
            request.CartNumber,
            access.FranchiseId,
            access.StoreId,
            cancellationToken);

        return payment is null
            ? NotFound(new { message = "Cart not found." })
            : Ok(payment);
    }

    [HttpGet("{paymentToken}/status")]
    public async Task<IActionResult> GetStatus(
        string paymentToken,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync(cancellationToken);
        var payment = await _walletPayments.GetStatusAsync(
            paymentToken,
            access.FranchiseId,
            access.StoreId,
            cancellationToken);

        return payment is null
            ? NotFound(new { message = "Wallet payment request not found." })
            : Ok(payment);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("{paymentToken}")]
    public async Task<IActionResult> GetForCustomer(
        string paymentToken,
        [FromQuery] GetWalletPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CartNumber) || request.CartNumber.Length > 50)
        {
            errors["cartNumber"] = ["Cart number is required and cannot exceed 50 characters."];
        }

        if (request.CustomerId is null or <= 0)
        {
            errors["customerId"] = ["Customer ID must be greater than zero."];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var payment = await _walletPayments.GetCartForCustomerAsync(
            paymentToken,
            request.CartNumber!.Trim(),
            request.CustomerId!.Value,
            _currentUser.UserId,
            cancellationToken);

        return payment is null
            ? NotFound(new { message = "Wallet payment request not found." })
            : Ok(payment);
    }

    private Task<Mart.Customer.Application.Auth.Dtos.MartUserAccessScopeDto> GetAccessAsync(
        CancellationToken cancellationToken) =>
        _sender.Send(new GetMartUserAccessScopeQuery(_currentUser.UserId), cancellationToken);
}
