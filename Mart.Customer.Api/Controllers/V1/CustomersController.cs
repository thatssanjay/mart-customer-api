using Asp.Versioning;
using Mart.Customer.Api.Contracts.Customers;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("by-mobile/{mobileNumber}")]
    public async Task<IActionResult> GetCustomerByMobileNumber(string mobileNumber, CancellationToken cancellationToken)
    {
        var customer = await _sender.Send(new GetCustomerByMobileQuery(mobileNumber), cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _sender.Send(
            new CreateCustomerCommand(
                request.CustomerCode,
                request.FirstName,
                request.LastName,
                request.DisplayName,
                request.MobileNumber,
                request.Email,
                request.Gender,
                request.DateOfBirth,
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.State,
                request.Country,
                request.PinCode,
                request.PreferredLanguage,
                request.RegistrationSource,
                request.IsMobileVerified,
                request.IsEmailVerified,
                request.IsActive,
                request.IsBlocked,
                request.LastLoginOn,
                request.CreatedOn,
                request.ModifiedOn),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCustomerByMobileNumber),
            new { mobileNumber = customer.MobileNumber, version = "1.0" },
            customer);
    }
}
