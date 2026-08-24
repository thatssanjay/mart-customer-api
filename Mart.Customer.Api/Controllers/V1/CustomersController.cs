using Asp.Versioning;
using Mart.Customer.Api.Contracts.Customers;
using Mart.Customer.Api.Contracts.Wallets;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Customers.Commands.UpdateCustomer;
using Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;
using Mart.Customer.Application.Customers.Queries.GetCustomers;
using Mart.Customer.Application.Customers.Queries.SearchCustomers;
using Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;
using Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
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
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchCustomers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var customers = await _sender.Send(new SearchCustomersQuery(search), cancellationToken);
        return Ok(customers);
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

    [HttpPost("~/api/customers/{customerId:long}/wallets/provision")]
    public async Task<IActionResult> ProvisionWallets(
        [FromRoute] ProvisionCustomerWalletsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ProvisionCustomerWalletsCommand(request.CustomerId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("~/api/customers/{customerId:long}/wallets")]
    public async Task<IActionResult> GetWallets(
        long customerId,
        [FromQuery] GetCustomerWalletsRequest request,
        CancellationToken cancellationToken)
    {
        var wallets = await _sender.Send(
            new GetCustomerWalletsQuery(customerId, request.IncludeInactive),
            cancellationToken);

        return Ok(wallets);
    }

    [HttpGet("~/api/customers/{customerId:long}/wallets/{walletTypeId:int}")]
    public async Task<IActionResult> GetWalletDetail(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken)
    {
        var wallet = await _sender.Send(
            new GetCustomerWalletDetailQuery(customerId, walletTypeId),
            cancellationToken);

        return wallet is null
            ? NotFound(new { message = "Active customer wallet not found." })
            : Ok(wallet);
    }

    [HttpPatch("~/api/customers/{customerId:long}/wallets/{walletTypeId:int}/status")]
    public async Task<IActionResult> UpdateWalletStatus(
        long customerId,
        int walletTypeId,
        UpdateCustomerWalletStatusRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await _sender.Send(
            new UpdateCustomerWalletStatusCommand(
                customerId,
                walletTypeId,
                request.IsActive),
            cancellationToken);

        return wallet is null
            ? NotFound(new { message = "Customer wallet not found." })
            : Ok(wallet);
    }

    [HttpGet("~/api/customers/{customerId:long}/wallets/{walletTypeId:int}/transactions")]
    public async Task<IActionResult> GetWalletTransactions(
        long customerId,
        int walletTypeId,
        [FromQuery] GetCustomerWalletTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var transactions = await _sender.Send(
            new GetCustomerWalletTransactionsQuery(
                customerId,
                walletTypeId,
                request.PageNumber,
                request.PageSize,
                request.TransactionType,
                request.FromDate,
                request.ToDate,
                request.ReferenceType,
                request.ReferenceId),
            cancellationToken);

        return transactions is null
            ? NotFound(new { message = "Active customer wallet not found." })
            : Ok(transactions);
    }

    [HttpGet("~/api/customers/{customerId:long}/wallets/{walletTypeId:int}/buckets")]
    public async Task<IActionResult> GetWalletBuckets(
        long customerId,
        int walletTypeId,
        [FromQuery] GetCustomerWalletBucketsRequest request,
        CancellationToken cancellationToken)
    {
        var buckets = await _sender.Send(
            new GetCustomerWalletBucketsQuery(
                customerId,
                walletTypeId,
                request.AvailableOnly,
                request.IncludeSourceTransaction),
            cancellationToken);

        return buckets is null
            ? NotFound(new { message = "Active customer wallet not found." })
            : Ok(buckets);
    }

    [HttpGet("~/api/customers/{customerId:long}/wallets/{walletTypeId:int}/expiry-summary")]
    public async Task<IActionResult> GetWalletExpirySummary(
        [FromRoute] GetCustomerWalletExpirySummaryRequest request,
        CancellationToken cancellationToken)
    {
        var summary = await _sender.Send(
            new GetCustomerWalletExpirySummaryQuery(
                request.CustomerId,
                request.WalletTypeId),
            cancellationToken);

        return summary is null
            ? NotFound(new { message = "Active customer wallet not found." })
            : Ok(summary);
    }
}
