using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Carts.Queries.GetCustomerCarts;
using Mart.Customer.Application.Carts.Commands.UpdateCartPaymentStatus;
using Mart.Customer.Api.Contracts.Carts;
using Mart.Customer.Api.Contracts.Customers;
using Mart.Customer.Api.Contracts.Wallets;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Customers.Commands.UpdateCustomer;
using Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;
using Mart.Customer.Application.Customers.Queries.GetCustomers;
using Mart.Customer.Application.Customers.Queries.SearchCustomers;
using Mart.Customer.Application.Cashback.Queries.GetStoreWalletConfigurations;
using Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;
using Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletTransactions;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBalances;
using Mart.Customer.Application.Wallets.Commands.TopUpWallet;
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

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("~/api/v{version:apiVersion}/customer/carts")]
    public async Task<IActionResult> GetCarts(
        [FromQuery, Required, MaxLength(50)] string cartNumber,
        [FromQuery, Required, MaxLength(30)] string status,
        [FromServices] IMartUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var carts = await _sender.Send(
            new GetCustomerCartsQuery(currentUser.UserId, status, CartNumber: cartNumber),
            cancellationToken);

        return Ok(carts);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpPatch("~/api/v{version:apiVersion}/customer/carts/payment-status")]
    public async Task<IActionResult> UpdateCartPaymentStatus(
        UpdateCartPaymentStatusRequest request,
        [FromServices] IMartUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateCartPaymentStatusCommand(request.CartId, request.Status, currentUser.UserId),
            cancellationToken);

        return Ok(result);
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

    [HttpGet("store-wallet-configurations")]
    public async Task<IActionResult> GetStoreWalletConfigurations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var configurations = await _sender.Send(
            new GetStoreWalletConfigurationsQuery(pageNumber, pageSize),
            cancellationToken);

        return Ok(configurations);
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

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("wallet-balances")]
    public async Task<IActionResult> GetWalletBalances(
        [FromServices] IMartUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var balances = await _sender.Send(
            new GetCustomerWalletBalancesQuery(currentUser.UserId), cancellationToken);

        return balances is null
            ? NotFound(new { message = "Customer not found." })
            : Ok(balances);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpPost("wallet-top-ups")]
    public async Task<IActionResult> TopUpWallet(
        TopUpWalletRequest request,
        [FromServices] IMartUserContext currentUser,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new TopUpWalletCommand(
                currentUser.UserId,
                request.WalletCode,
                request.Amount,
                request.PaymentMode,
                request.CardLast4,
                request.ReferenceNumber,
                request.PaymentReference,
                currentUser.UserName ?? $"customer-{currentUser.UserId}"),
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
            new GetCustomerWalletDetailQuery(customerId, walletTypeId) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
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
                request.IsActive) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
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
                request.ReferenceId) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
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
                request.IncludeSourceTransaction) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
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
                request.WalletTypeId) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
            cancellationToken);

        return summary is null
            ? NotFound(new { message = "Active customer wallet not found." })
            : Ok(summary);
    }
}
