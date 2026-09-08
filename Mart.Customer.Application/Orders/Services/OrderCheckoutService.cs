using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Application.Inventory.Services;
using Mart.Customer.Application.Orders.Commands.CheckoutOrder;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Payments.Services;
using Mart.Customer.Application.Wallets.Commands.CreditWallet;
using Mart.Customer.Application.Wallets.Commands.RedeemWallet;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Orders;
using MediatR;
using Mart.Customer.Application.Wallets.Commands.EnsureStoreWallet;
using Microsoft.Extensions.Logging;

namespace Mart.Customer.Application.Orders.Services;

public sealed class OrderCheckoutService : IOrderCheckoutService
{
    private const string OrderReferenceType = "ORDER";
    private const string AppPaymentMode = "APP";
    private readonly ICustomerCartRepository _cartRepository;
    private readonly ICustomerOrderRepository _orderRepository;
    private readonly IOrderCheckoutCalculator _checkoutCalculator;
    private readonly ICashbackConfigurationRepository _cashbackRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;
    private readonly IInventoryStockService _inventoryStockService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly IInvoiceService _invoiceService;
    private readonly IWalletPaymentRequestService _walletPaymentRequestService;
    private readonly ILogger<OrderCheckoutService> _logger;

    public OrderCheckoutService(
        ICustomerCartRepository cartRepository,
        ICustomerOrderRepository orderRepository,
        IOrderCheckoutCalculator checkoutCalculator,
        ICashbackConfigurationRepository cashbackRepository,
        IWalletTypeRepository walletTypeRepository,
        ICustomerSubscriptionRepository subscriptionRepository,
        IInventoryStockService inventoryStockService,
        IUnitOfWork unitOfWork,
        ISender sender,
        IInvoiceService invoiceService,
        IWalletPaymentRequestService walletPaymentRequestService,
        ILogger<OrderCheckoutService> logger)
    {
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _checkoutCalculator = checkoutCalculator;
        _cashbackRepository = cashbackRepository;
        _walletTypeRepository = walletTypeRepository;
        _subscriptionRepository = subscriptionRepository;
        _inventoryStockService = inventoryStockService;
        _unitOfWork = unitOfWork;
        _sender = sender;
        _invoiceService = invoiceService;
        _walletPaymentRequestService = walletPaymentRequestService;
        _logger = logger;
    }

    public async Task<OrderCheckoutDto?> CheckoutAsync(
        CheckoutOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedPayments = NormalizeAndValidatePayments(command.Payments);
        CheckoutTransactionResult? transactionResult;

        try
        {
            transactionResult = await _unitOfWork.ExecuteInTransactionAsync(
                token => ExecuteCheckoutTransactionAsync(command, normalizedPayments, token),
                cancellationToken);
        }
        catch (Exception checkoutException) when (checkoutException is not OperationCanceledException)
        {
            transactionResult = await ResolveConcurrentCheckoutAsync(
                command,
                normalizedPayments,
                cancellationToken);
            if (transactionResult is null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(checkoutException).Throw();
            }
        }

        if (transactionResult is null)
        {
            return null;
        }

        var order = transactionResult.Order;
        if (!transactionResult.IsIdempotentRetry)
        {
            _logger.LogInformation(
                "Checkout committed for order {CustomerOrderId} and invoice {InvoiceNumber}",
                order.CustomerOrderId,
                order.InvoiceNumber);
        }

        if (string.Equals(order.InvoiceStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            await TryArchiveInvoiceAsync(order, cancellationToken);
        }

        return ToDto(order, transactionResult.IsIdempotentRetry);
    }

    private async Task<CheckoutTransactionResult?> ExecuteCheckoutTransactionAsync(
        CheckoutOrderCommand command,
        IReadOnlyList<NormalizedPayment> payments,
        CancellationToken cancellationToken)
    {
        var isAppPayment = payments.Any(payment =>
            string.Equals(payment.PaymentMode, AppPaymentMode, StringComparison.OrdinalIgnoreCase));
        var cart = await _cartRepository.GetByCartNumberForCheckoutAsync(
            command.CartNumber!.Trim(),
            command.FranchiseId,
            command.MartStoreId,
            cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var existingOrder = await _orderRepository.GetByCartIdAsync(
            cart.CustomerCartId,
            tracking: true,
            cancellationToken);
        if (existingOrder is not null)
        {
            EnsureRetryIsConsistent(existingOrder, command, payments);
            return new CheckoutTransactionResult(existingOrder, true);
        }

        var preview = await _checkoutCalculator.CalculateAsync(
            cart,
            isAppPayment ? command.WalletTypeId : null,
            isAppPayment ? command.RedemptionAmount : null,
            cancellationToken);
        EnsurePaymentsMatch(preview.FinalPayableAmount, payments);

        if (isAppPayment)
        {
            var appPayment = payments.Single(payment =>
                string.Equals(payment.PaymentMode, AppPaymentMode, StringComparison.OrdinalIgnoreCase));
            var paymentReference = await _walletPaymentRequestService.ValidateForCheckoutAsync(
                cart,
                command.WalletPaymentToken,
                preview.FinalPayableAmount,
                cancellationToken);
            if (!string.Equals(
                    appPayment.TransactionReference,
                    paymentReference,
                    StringComparison.Ordinal))
            {
                throw new DomainException("The wallet payment reference does not match the paid request.");
            }

            await _sender.Send(
                new EnsureStoreWalletCommand(cart.CustomerId, cart.MartStoreId), cancellationToken);
        }

        var orderDate = DateTime.UtcNow;
        var order = CustomerOrder.Create(
            cart.CustomerCartId,
            ReferenceCodeGenerator.GenerateWithPrefix("INV", 9),
            cart.CustomerId,
            cart.FranchiseId,
            cart.MartStoreId,
            orderDate,
            preview.TotalItemCount,
            preview.GrossAmount,
            preview.DiscountAmount,
            preview.GSTAmount,
            preview.NetAmount,
            isAppPayment ? command.WalletTypeId : null,
            preview.RedemptionAmount,
            preview.FinalPayableAmount,
            command.CashierId);

        foreach (var item in preview.Items)
        {
            order.AddItem(
                item.CustomerCartItemId,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.MRP,
                item.GrossAmount,
                item.DiscountAmount,
                item.GSTPercent,
                item.GSTAmount,
                item.LineTotal);
        }

        foreach (var payment in payments)
        {
            order.AddPayment(payment.PaymentMode, payment.Amount, payment.TransactionReference);
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _inventoryStockService.DeductSaleStockAsync(
            order.CustomerOrderId,
            cancellationToken);

        if (isAppPayment && preview.RedemptionAmount > 0)
        {
            await _sender.Send(
                new RedeemWalletCommand(
                    cart.CustomerId,
                    command.WalletTypeId!.Value,
                    preview.RedemptionAmount,
                    OrderReferenceType,
                    order.CustomerOrderId,
                    "Order redemption",
                    command.CashierId.ToString(), cart.MartStoreId),
                cancellationToken);
        }

        if (isAppPayment)
        {
            await CreditConfiguredRewardAsync(order, command.CashierId, cancellationToken);
            await CreditConfiguredCashbackAsync(order, command.CashierId, cancellationToken);
        }

        cart.MarkPaid(preview.RedemptionAmount, orderDate);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CheckoutTransactionResult(order, false);
    }

    private async Task CreditConfiguredCashbackAsync(
        CustomerOrder order,
        long cashierId,
        CancellationToken cancellationToken)
    {
        var settings = await _cashbackRepository.GetActiveAsync(order.OrderDate, cancellationToken);
        var setting = settings.SingleOrDefault(item => item.StoreId == order.MartStoreId);
        if (setting is null || order.FinalPayableAmount < setting.MinimumPurchaseAmount)
        {
            return;
        }

        var cashback = RoundMoney(order.FinalPayableAmount * setting.CashbackPercentage / 100m);
        if (setting.MaximumCashbackPerOrder > 0)
        {
            cashback = Math.Min(cashback, setting.MaximumCashbackPerOrder);
        }

        if (cashback <= 0)
        {
            return;
        }

        var walletTypes = await _walletTypeRepository.GetAsync(activeOnly: true, cancellationToken);
        var cashbackWalletType = walletTypes.SingleOrDefault(walletType =>
            string.Equals(walletType.Code, "CASHBACK", StringComparison.OrdinalIgnoreCase));
        if (cashbackWalletType is null)
        {
            return;
        }

        await _sender.Send(
            new CreditWalletCommand(
                order.CustomerId,
                cashbackWalletType.Id,
                cashback,
                OrderReferenceType,
                order.CustomerOrderId,
                "Order cashback",
                setting.CashbackValidityDays > 0
                    ? order.OrderDate.AddDays(setting.CashbackValidityDays)
                    : null,
                cashierId.ToString(), order.MartStoreId),
            cancellationToken);
        order.SetCashbackEarned(cashback);
    }

    private async Task CreditConfiguredRewardAsync(
        CustomerOrder order,
        long cashierId,
        CancellationToken cancellationToken)
    {
        var configuration = await _subscriptionRepository.GetActiveOrderRewardConfigurationAsync(
            order.CustomerId,
            order.OrderDate,
            cancellationToken);
        if (configuration is null)
        {
            return;
        }

        var reward = RoundMoney(
            order.FinalPayableAmount * configuration.ExtraPointPercentage / 100m);
        if (reward <= 0)
        {
            return;
        }

        await _sender.Send(
            new CreditWalletCommand(
                order.CustomerId,
                configuration.WalletTypeId,
                reward,
                OrderReferenceType,
                order.CustomerOrderId,
                "Order reward",
                configuration.ExpiryDate,
                cashierId.ToString(), order.MartStoreId),
            cancellationToken);
        order.SetRewardEarned(reward);
    }

    private async Task<CheckoutTransactionResult?> ResolveConcurrentCheckoutAsync(
        CheckoutOrderCommand command,
        IReadOnlyList<NormalizedPayment> payments,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearChanges();
        var cart = await _cartRepository.GetByCartNumberForCheckoutAsync(
            command.CartNumber!.Trim(),
            command.FranchiseId,
            command.MartStoreId,
            cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var existingOrder = await _orderRepository.GetByCartIdAsync(
            cart.CustomerCartId,
            tracking: true,
            cancellationToken);
        if (existingOrder is null)
        {
            return null;
        }

        EnsureRetryIsConsistent(existingOrder, command, payments);
        return new CheckoutTransactionResult(existingOrder, true);
    }

    private async Task TryArchiveInvoiceAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        try
        {
            var archive = await _invoiceService.GenerateAndArchiveAsync(ToInvoiceSnapshot(order), cancellationToken);
            order.MarkInvoiceArchived(archive.ArchivePath);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            order.MarkInvoicePending();
            _logger.LogWarning(
                exception,
                "Invoice generation/archive is pending for order {CustomerOrderId} and invoice {InvoiceNumber}",
                order.CustomerOrderId,
                order.InvoiceNumber);
        }
    }

    private static IReadOnlyList<NormalizedPayment> NormalizeAndValidatePayments(
        IReadOnlyList<CheckoutPayment> payments)
    {
        var normalized = payments.Select(payment =>
        {
            var mode = payment.PaymentMode?.Trim().ToUpperInvariant() ?? string.Empty;
            var reference = string.IsNullOrWhiteSpace(payment.TransactionReference)
                ? null
                : payment.TransactionReference.Trim();

            if (payment.Amount <= 0 || decimal.Round(payment.Amount, 2) != payment.Amount)
            {
                throw new DomainException("Payment amount must be greater than zero and have no more than two decimal places.");
            }

            if (reference?.Length > 100)
            {
                throw new DomainException("Payment transaction reference cannot exceed 100 characters.");
            }

            if (mode is not "CASH" and not "UPI" and not "CARD" and not "POS" and not "WALLET" and not AppPaymentMode)
            {
                throw new DomainException("Payment mode must be Cash, UPI, Card, POS, Wallet, or APP.");
            }

            if (mode != "CASH" && reference is null)
            {
                throw new DomainException($"A transaction reference is required for {mode} payments.");
            }

            if (mode == "CASH" && reference is not null)
            {
                throw new DomainException("Cash payments cannot include a transaction reference.");
            }

            return new NormalizedPayment(
                mode switch
                {
                    "CASH" => "Cash",
                    "CARD" => "Card",
                    "POS" => "POS",
                    "WALLET" => "Wallet",
                    AppPaymentMode => AppPaymentMode,
                    _ => "UPI"
                },
                payment.Amount,
                reference);
        }).ToList();

        if (normalized.GroupBy(payment => payment.PaymentMode, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new DomainException("Each payment mode can only be supplied once.");
        }

        return normalized;
    }

    private static void EnsurePaymentsMatch(
        decimal finalPayableAmount,
        IReadOnlyList<NormalizedPayment> payments)
    {
        var paymentTotal = payments.Sum(payment => payment.Amount);
        if (paymentTotal != finalPayableAmount)
        {
            throw new DomainException("Payment total must equal the final payable amount.");
        }

        if (finalPayableAmount > 0 && payments.Count == 0)
        {
            throw new DomainException("At least one payment is required.");
        }
    }

    private static void EnsureRetryIsConsistent(
        CustomerOrder order,
        CheckoutOrderCommand command,
        IReadOnlyList<NormalizedPayment> payments)
    {
        var isAppPayment = payments.Any(payment =>
            string.Equals(payment.PaymentMode, AppPaymentMode, StringComparison.OrdinalIgnoreCase));
        var requestedWalletTypeId = isAppPayment ? command.WalletTypeId : null;
        var requestedRedemption = isAppPayment ? command.RedemptionAmount ?? 0m : 0m;
        if (order.RedemptionWalletTypeId != requestedWalletTypeId ||
            order.RedemptionAmount != requestedRedemption ||
            order.Payments.Count != payments.Count)
        {
            throw new DomainException("The cart has already been checked out with different payment details.");
        }

        var existing = order.Payments
            .OrderBy(payment => payment.PaymentMode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var requested = payments
            .OrderBy(payment => payment.PaymentMode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var index = 0; index < existing.Count; index++)
        {
            if (!string.Equals(existing[index].PaymentMode, requested[index].PaymentMode, StringComparison.OrdinalIgnoreCase) ||
                existing[index].Amount != requested[index].Amount ||
                !string.Equals(existing[index].TransactionReference, requested[index].TransactionReference, StringComparison.Ordinal))
            {
                throw new DomainException("The cart has already been checked out with different payment details.");
            }
        }
    }

    private static OrderCheckoutDto ToDto(CustomerOrder order, bool isIdempotentRetry) =>
        new(
            order.CustomerOrderId,
            order.CustomerCartId,
            order.InvoiceNumber,
            order.CustomerId,
            order.FranchiseId,
            order.MartStoreId,
            order.OrderDate,
            order.GrossAmount,
            order.DiscountAmount,
            order.GSTAmount,
            order.NetAmount,
            order.RedemptionAmount,
            order.FinalPayableAmount,
            order.RewardEarned,
            order.CashbackEarned,
            order.OrderStatus,
            order.InvoiceStatus,
            isIdempotentRetry,
            order.Items.Select(item => new OrderCheckoutItemDto(
                item.CustomerOrderItemId,
                item.CustomerCartItemId,
                item.ProductId,
                item.ProductNameSnapshot,
                item.Quantity,
                item.UnitPrice,
                item.MRP,
                item.GrossAmount,
                item.DiscountAmount,
                item.GSTPercent,
                item.GSTAmount,
                item.LineTotal)).ToList(),
            order.Payments.Select(payment => new OrderCheckoutPaymentDto(
                payment.CustomerOrderPaymentId,
                payment.PaymentMode,
                payment.Amount,
                payment.TransactionReference,
                payment.PaidOn)).ToList());

    private static OrderInvoiceSnapshotDto ToInvoiceSnapshot(CustomerOrder order)
    {
        var dto = ToDto(order, false);
        return new OrderInvoiceSnapshotDto(
            dto.CustomerOrderId,
            dto.InvoiceNumber,
            dto.CustomerId,
            dto.FranchiseId,
            dto.MartStoreId,
            order.CustomerCodeSnapshot,
            order.CustomerNameSnapshot,
            order.CustomerMobileSnapshot,
            order.CustomerAddressSnapshot,
            order.StoreNameSnapshot,
            order.StoreAddressSnapshot,
            dto.OrderDate,
            dto.GrossAmount,
            dto.DiscountAmount,
            dto.GSTAmount,
            dto.NetAmount,
            dto.RedemptionAmount,
            dto.FinalPayableAmount,
            order.InvoiceTemplateVersion,
            dto.Items,
            dto.Payments,
            order.RedeemPointsUsed,
            order.RewardEarned,
            order.CashbackEarned,
            order.CGSTAmount,
            order.SGSTAmount,
            order.IGSTAmount,
            order.RoundOffAmount,
            order.StoreGSTINSnapshot,
            order.StoreStateCodeSnapshot,
            order.VerificationCode);
    }

    private static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private sealed record NormalizedPayment(
        string PaymentMode,
        decimal Amount,
        string? TransactionReference);

    private sealed record CheckoutTransactionResult(CustomerOrder Order, bool IsIdempotentRetry);
}
