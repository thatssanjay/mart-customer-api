using System.ComponentModel.DataAnnotations;

namespace Mart.Customer.Api.Contracts.Referrals;

public sealed class CreateCustomerReferralRequest
{
    [Required, RegularExpression("^[0-9]{10}$", ErrorMessage = "Enter a valid 10 digit mobile number.")]
    public string? MobileNumber { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Referral configuration is required.")]
    public int ReferralConfigId { get; init; }
}
