namespace Mart.Customer.Application.Subscriptions.Dtos;

public sealed record CustomerSubscriptionDto(
    long Id,
    int SubscriptionPlanId,
    string PlanName,
    decimal SubscriptionAmount,
    DateTime StartDate,
    DateTime ExpiryDate,
    string Status,
    decimal ExtraPointPercentage,
    decimal FeeToWalletPercentage,
    int? WalletTypeId,
    decimal? WalletCreditAmount,
    bool? IsPointCreated,
    long? PointReferenceId,
    long? PaymentTransactionId,
    DateTime CreatedOn,
    SubscriptionPlanDto SubscriptionPlan);
