using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.AppProviders;
using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Carts.Queries.GetCustomerCarts;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Customers.Commands.UpdateAppCustomerProfile;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Customers.Queries.GetAppCustomerByMobile;
using Mart.Customer.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
[Route("api/v{version:apiVersion}/app-providers")]
public sealed class AppProvidersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;
    private readonly IOtpValidator _otpValidator;

    public AppProvidersController(
        ISender sender,
        IMartUserContext currentUser,
        IOtpValidator otpValidator)
    {
        _sender = sender;
        _currentUser = currentUser;
        _otpValidator = otpValidator;
    }

    [AllowAnonymous]
    [HttpGet("customers/by-mobile/{mobileNumber}")]
    public async Task<IActionResult> GetCustomerByMobileNumber(
        string mobileNumber,
        [FromQuery, Required, MaxLength(10)] string otp,
        CancellationToken cancellationToken)
    {
        await EnsureValidOtpAsync(mobileNumber, otp, cancellationToken);

        var customer = await _sender.Send(
            new GetAppCustomerByMobileQuery(mobileNumber),
            cancellationToken);

        return customer is null
            ? NotFound(new { message = "Customer not found." })
            : Ok(customer);
    }

    [HttpPut("customers/{userId:long}/profile")]
    public async Task<IActionResult> UpdateCustomerProfile(
        long userId,
        UpdateAppCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (userId != _currentUser.UserId)
        {
            return Forbid();
        }

        var customer = await _sender.Send(
            new UpdateAppCustomerProfileCommand(
                userId,
                request.DisplayName,
                request.EmailAddress,
                request.Address),
            cancellationToken);

        return customer is null
            ? NotFound(new { message = "Customer not found." })
            : Ok(customer);
    }

    [AllowAnonymous]
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer(
        CreateAppCustomerRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureValidOtpAsync(
            request.MobileNumber ?? string.Empty,
            request.Otp ?? string.Empty,
            cancellationToken);

        var customer = await _sender.Send(
            new CreateCustomerCommand(
                null,
                request.DisplayName,
                null,
                request.DisplayName,
                request.MobileNumber ?? string.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "India",
                null,
                "Hindi",
                "mobile app",
                true,
                false,
                true,
                false,
                null,
                null,
                request.ReferralCode),
            cancellationToken);

        var response = new AppCustomerProfileDto(
            customer.CustomerId,
            customer.MobileNumber,
            customer.DisplayName,
            customer.Email,
            customer.AddressLine1);

        return CreatedAtAction(
            nameof(GetCustomerByMobileNumber),
            new { mobileNumber = customer.MobileNumber, version = "1.0" },
            response);
    }

    private async Task EnsureValidOtpAsync(
        string mobileNumber,
        string otp,
        CancellationToken cancellationToken)
    {
        if (!await _otpValidator.IsValidAsync(mobileNumber, otp, cancellationToken))
        {
            throw new DomainException("Invalid OTP.");
        }
    }

    [HttpGet("carts")]
    public async Task<IActionResult> GetCarts(
        [FromQuery] long customerId,
        [FromQuery] string? cartStatus,
        CancellationToken cancellationToken)
    {
        if (customerId != _currentUser.UserId)
        {
            return Forbid();
        }

        var carts = await _sender.Send(
            new GetCustomerCartsQuery(customerId, cartStatus),
            cancellationToken);

        return Ok(carts);
    }
}
