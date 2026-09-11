namespace Mart.Customer.Api.Contracts.Promotions;

public sealed class AllocateStorePromotionRequest
{
    public long OrderId { get; init; }
    public string? PromoCode { get; init; }
}
