using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Application.Wallets.Services;

public static class WalletRedemptionBucketCalculator
{
    public static IReadOnlyList<RedeemPreviewBucketDto> Calculate(
        IEnumerable<WalletBalanceBucketDto> buckets,
        decimal amount,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        if (amount <= 0)
        {
            throw new DomainException("Redemption amount must be greater than zero.");
        }

        var remainingAmount = amount;
        var allocations = new List<RedeemPreviewBucketDto>();

        foreach (var bucket in buckets
                     .Where(bucket =>
                         bucket.AvailableAmount > 0 &&
                         (!bucket.ExpiryDate.HasValue || bucket.ExpiryDate.Value > asOfUtc))
                     .OrderBy(bucket => bucket.ExpiryDate == null)
                     .ThenBy(bucket => bucket.ExpiryDate)
                     .ThenBy(bucket => bucket.WalletBalanceBucketId))
        {
            if (remainingAmount == 0)
            {
                break;
            }

            var redeemAmount = Math.Min(bucket.AvailableAmount, remainingAmount);
            allocations.Add(new RedeemPreviewBucketDto(
                bucket.WalletBalanceBucketId,
                bucket.SourceTransactionId,
                bucket.AvailableAmount,
                redeemAmount,
                bucket.AvailableAmount - redeemAmount,
                bucket.ExpiryDate));
            remainingAmount -= redeemAmount;
        }

        if (remainingAmount > 0)
        {
            throw new DomainException("Insufficient wallet balance.");
        }

        return allocations;
    }
}
