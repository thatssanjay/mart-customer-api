namespace Mart.Customer.Application.Subscriptions.Dtos;

public sealed record SubscriptionPlanDto(
    int SubscriptionId,
    string PlanName,
    decimal SubscriptionFee,
    int DurationValue,
    string DurationType,
    decimal ExtraPointPercentage,
    decimal FeeToWalletPercentage,
    int WalletTypeId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo);
