using Asp.Versioning;
using Mart.Customer.Api.Contracts.Customers;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Customers.Commands.UpdateCustomer;
using Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;
using Mart.Customer.Application.Customers.Queries.GetCustomers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
// [Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? displayName = null,
        [FromQuery] string? mobileNumber = null,
        CancellationToken cancellationToken = default)
    {
        var customers = await _sender.Send(
            new GetCustomersQuery(pageNumber, pageSize, displayName, mobileNumber),
            cancellationToken);

        return Ok(customers);
    }

    [HttpGet("by-mobile/{mobileNumber}")]
    public async Task<IActionResult> GetCustomerByMobileNumber(string mobileNumber, CancellationToken cancellationToken)
    {
        var customer = await _sender.Send(new GetCustomerByMobileQuery(mobileNumber), cancellationToken);
        return customer is null ? NoContent() : Ok(customer);
    }
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok("Customer Controller Working");
    }
    [HttpPost("CreateCustomer")]
    public async Task<IActionResult> CreateCustomer(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var mobileNumber = request.MobileNumber ?? string.Empty;
        var customer = await _sender.Send(
            new CreateCustomerCommand(
                null,
                "FirstName",
                "LastName",
                request.DisplayName,
                mobileNumber,
                request.Email,
                "Gender",
                null,
                request.AddressLine1,
                "AddressLine2",
                "City",
                "State",
                "Country",
                "PinCode",
                "PreferredLanguage",
                "RegistrationSource",
                false,
                false,
                true,
                false,
                null,
                null),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCustomerByMobileNumber),
            new { mobileNumber = customer.MobileNumber, version = "1.0" },
            customer);
    }

    [HttpPut("UpdateCustomer/{customerId:long}")]
    public async Task<IActionResult> UpdateCustomer(
        long customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _sender.Send(
            new UpdateCustomerCommand(
                customerId,
                request.DisplayName,
                request.MobileNumber ?? string.Empty,
                request.Email,
                request.AddressLine1),
            cancellationToken);

        return customer is null
            ? NotFound(new { message = "Customer not found." })
            : Ok(customer);
    }
}
