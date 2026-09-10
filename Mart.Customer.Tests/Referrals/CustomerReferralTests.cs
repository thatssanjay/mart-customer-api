using FluentValidation.TestHelper;
using Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Referrals;

namespace Mart.Customer.Tests.Referrals;

public sealed class CustomerReferralTests
{
    [Fact]
    public void Create_NormalizesMobileAndStartsWaiting()
    {
        var createdOn = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        var referral = CustomerReferral.Create(7, 3, 500m, 75m, 25m, "98765 43210", "martabc123", createdOn);

        Assert.Equal(7, referral.ReferrerCustomerId);
        Assert.Equal(3, referral.ReferralConfigId);
        Assert.Equal(500m, referral.MinimumPurchaseAmount);
        Assert.Equal(75m, referral.ReferrerRewardPoint);
        Assert.Equal(25m, referral.ReferredCustomerRewardPoint);
        Assert.Equal("9876543210", referral.ReferredMobileNumber);
        Assert.Equal("MARTABC123", referral.ReferralCode);
        Assert.Equal(CustomerReferral.WaitingStatus, referral.Status);
        Assert.True(referral.IsActive);
        Assert.Null(referral.ReferredCustomerId);
        Assert.Null(referral.OnboardedOn);
    }

    [Fact]
    public void MarkOnboarded_ChangesWaitingReferralToActiveAndPreventsReuse()
    {
        var referral = CustomerReferral.Create(7, 3, 500m, 75m, 25m, "9876543210", "MARTABC123", DateTime.UtcNow);
        var onboardedOn = DateTime.UtcNow.AddMinutes(1);

        referral.MarkOnboarded(11, onboardedOn);

        Assert.Equal(CustomerReferral.ActiveStatus, referral.Status);
        Assert.Equal(11, referral.ReferredCustomerId);
        Assert.Equal(onboardedOn, referral.OnboardedOn);
        Assert.False(referral.IsActive);
        Assert.Throws<DomainException>(() => referral.MarkOnboarded(12, onboardedOn.AddMinutes(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12345678901")]
    [InlineData("12345abcde")]
    public void Validator_RejectsInvalidMobileNumbers(string mobileNumber)
    {
        var validator = new CreateCustomerReferralCommandValidator();
        var result = validator.TestValidate(new CreateCustomerReferralCommand(7, mobileNumber, 3));

        result.ShouldHaveValidationErrorFor(command => command.ReferredMobileNumber);
    }

    [Fact]
    public void Validator_RejectsMissingReferralConfiguration()
    {
        var validator = new CreateCustomerReferralCommandValidator();
        var result = validator.TestValidate(new CreateCustomerReferralCommand(7, "9876543210", 0));

        result.ShouldHaveValidationErrorFor(command => command.ReferralConfigId);
    }
}
