using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Orders;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Orders.Queries.GetOrderCheckoutPreview;
using Mart.Customer.Application.Orders.Commands.CheckoutOrder;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Queries.GetOrderDetailByInvoiceNumber;
using Mart.Customer.Application.Orders.Queries.GetOrderDetail;
using Mart.Customer.Application.Orders.Queries.GetInvoiceDetails;
using Mart.Customer.Application.Orders.Queries.GetCustomerOrders;
using Mart.Customer.Application.Orders.Queries.SearchOrders;
using Mart.Customer.Application.Orders.Queries.VerifyOrder;
using Mart.Customer.Application.Orders.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;
    private readonly IInvoiceService _invoiceService;

    public OrdersController(
        ISender sender,
        IMartUserContext currentUser,
        IInvoiceService invoiceService)
    {
        _sender = sender;
        _currentUser = currentUser;
        _invoiceService = invoiceService;
    }

    [HttpGet("~/api/v{version:apiVersion}/customers/{customerId:long}/orders")]
    public async Task<IActionResult> GetCustomerOrders(
        long customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(
                _currentUser.LoginType,
                "customer",
                StringComparison.OrdinalIgnoreCase) &&
            _currentUser.UserId != customerId)
        {
            return Forbid();
        }

        var orders = await _sender.Send(
            new GetCustomerOrdersQuery(customerId, pageNumber, pageSize),
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> GetById(
        long orderId,
        CancellationToken cancellationToken)
    {
        var accessScope = await GetOrderDetailAccessScopeAsync(cancellationToken);

        var result = await _sender.Send(
            new GetOrderDetailQuery(orderId, accessScope),
            cancellationToken);

        return ToOrderDetailActionResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? customerName,
        [FromQuery] string? mobileNumber,
        [FromQuery] string? invoiceNumber,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var orders = await _sender.Send(
            new SearchOrdersQuery(
                access.FranchiseId,
                access.StoreId,
                customerName,
                mobileNumber,
                invoiceNumber,
                fromDate,
                toDate,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("by-invoice/{invoiceNumber}")]
    public async Task<IActionResult> GetByInvoiceNumber(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var accessScope = await GetOrderDetailAccessScopeAsync(cancellationToken);
        var result = await _sender.Send(
            new GetOrderDetailByInvoiceNumberQuery(invoiceNumber, accessScope),
            cancellationToken);

        return ToOrderDetailActionResult(result);
    }

    [AllowAnonymous]
    [HttpGet("verify/{verificationCode}")]
    public async Task<IActionResult> Verify(
        string verificationCode,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new VerifyOrderQuery(verificationCode),
            cancellationToken);

        return result is null
            ? NotFound(new { message = "Order verification code is invalid." })
            : Ok(result);
    }

    [HttpGet("{orderId:long}/invoice")]
    public async Task<IActionResult> GetInvoice(
        long orderId,
        CancellationToken cancellationToken)
    {
        var accessScope = await GetOrderDetailAccessScopeAsync(cancellationToken);
        var result = await _invoiceService.GetInvoiceAsync(
            orderId,
            accessScope,
            cancellationToken);

        return result.Status switch
        {
            InvoiceDownloadStatus.Found => File(
                result.Content!,
                "application/pdf",
                result.FileName),
            InvoiceDownloadStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "Order not found." })
        };
    }

    [HttpGet("{orderId:long}/invoice-pdf")]
    public async Task<IActionResult> GetOriginalInvoicePdf(
        long orderId,
        [FromQuery] string? disposition,
        CancellationToken cancellationToken)
    {
        var isAttachment = string.Equals(disposition, "attachment", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(disposition) &&
            !isAttachment &&
            !string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["disposition"] = ["Disposition must be either 'inline' or 'attachment'."]
            }));
        }

        var accessScope = await GetOrderDetailAccessScopeAsync(cancellationToken);
        var result = await _invoiceService.GetOriginalInvoiceAsync(
            orderId,
            accessScope,
            cancellationToken);

        return result.Status switch
        {
            InvoiceDownloadStatus.Found when isAttachment => File(
                result.Content!,
                "application/pdf",
                result.FileName),
            InvoiceDownloadStatus.Found => InlinePdf(result.Content!, result.FileName!),
            InvoiceDownloadStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "Invoice PDF not found." })
        };
    }

    [HttpGet("{orderId:long}/invoice-details")]
    public async Task<IActionResult> GetInvoiceDetails(
        long orderId,
        CancellationToken cancellationToken)
    {
        var accessScope = await GetOrderDetailAccessScopeAsync(cancellationToken);
        var result = await _sender.Send(
            new GetInvoiceDetailsQuery(orderId, accessScope),
            cancellationToken);

        return result.Status switch
        {
            InvoiceDetailsResultStatus.Found => Ok(result.Invoice),
            InvoiceDetailsResultStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "Order not found." })
        };
    }

    [HttpPost("checkout-preview")]
    public async Task<IActionResult> CheckoutPreview(
        CheckoutPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var preview = await _sender.Send(
            new GetOrderCheckoutPreviewQuery(
                request.CartNumber,
                access.FranchiseId,
                access.StoreId,
                request.WalletTypeId,
                request.RedemptionAmount),
            cancellationToken);

        return preview is null
            ? NotFound(new { message = "Cart not found." })
            : Ok(preview);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        CheckoutOrderRequest request,
        CancellationToken cancellationToken)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var order = await _sender.Send(
            new CheckoutOrderCommand(
                request.CartNumber,
                access.FranchiseId,
                access.StoreId,
                _currentUser.UserId,
                request.WalletTypeId,
                request.RedemptionAmount,
                request.WalletPaymentToken,
                request.Payments.Select(payment => new CheckoutPayment(
                    payment.PaymentMode,
                    payment.Amount,
                    payment.TransactionReference)).ToList()),
            cancellationToken);

        return order is null
            ? NotFound(new { message = "Cart not found." })
            : Ok(order);
    }

    private async Task<OrderDetailAccessScope> GetOrderDetailAccessScopeAsync(
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                _currentUser.LoginType,
                "customer",
                StringComparison.OrdinalIgnoreCase))
        {
            return new OrderDetailAccessScope(_currentUser.UserId, null, null);
        }

        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        return new OrderDetailAccessScope(null, access.FranchiseId, access.StoreId);
    }

    private IActionResult ToOrderDetailActionResult(OrderDetailResult result) =>
        result.Status switch
        {
            OrderDetailResultStatus.Found => Ok(result.Order),
            OrderDetailResultStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "Order not found." })
        };

    private IActionResult InlinePdf(Stream content, string fileName)
    {
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(content, "application/pdf");
    }
}
