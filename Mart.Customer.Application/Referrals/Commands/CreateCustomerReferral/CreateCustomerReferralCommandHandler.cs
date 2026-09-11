using System.Security.Cryptography;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Referrals.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Referrals;
using MediatR;

namespace Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;

public sealed class CreateCustomerReferralCommandHandler(
    ICustomerRepository customers,
    ICustomerReferralRepository referrals,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCustomerReferralCommand, CreatedCustomerReferralDto>
{
    public Task<CreatedCustomerReferralDto> Handle(
        CreateCustomerReferralCommand request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(token => CreateAsync(request, token), cancellationToken);

    private async Task<CreatedCustomerReferralDto> CreateAsync(
        CreateCustomerReferralCommand request,
        CancellationToken cancellationToken)
    {
        var referrer = await customers.GetByIdAsync(request.ReferrerCustomerId, cancellationToken)
            ?? throw new DomainException("Customer not found.");
        var configuration = await referrals.GetActiveConfigurationAsync(
            request.ReferralConfigId,
            cancellationToken);
        if (configuration is null)
            throw new DomainException("The referral offer is inactive or has expired.");
        var mobile = CustomerReferral.NormalizeMobile(request.ReferredMobileNumber);
        if (await customers.ExistsByMobileNumberAsync(mobile, cancellationToken) ||
            await referrals.ExistsForMobileAsync(mobile, cancellationToken))
            throw new DomainException("This mobile number is already onboarded/referred.");

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        var referral = CustomerReferral.Create(
            request.ReferrerCustomerId,
            request.ReferralConfigId,
            configuration.MinimumPurchaseAmount,
            configuration.ReferrerRewardPoint,
            configuration.ReferredCustomerRewardPoint,
            mobile,
            code,
            DateTime.UtcNow);
        await referrals.AddAsync(referral, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedCustomerReferralDto(
            referral.CustomerReferralId,
            referral.ReferredMobileNumber,
            referral.ReferralCode,
            referral.Status,
            referral.CreatedOn);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"MART{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";
            if (!await referrals.ReferralCodeExistsAsync(code, cancellationToken)) return code;
        }
        throw new DomainException("Unable to generate a unique referral code. Please try again.");
    }
}
