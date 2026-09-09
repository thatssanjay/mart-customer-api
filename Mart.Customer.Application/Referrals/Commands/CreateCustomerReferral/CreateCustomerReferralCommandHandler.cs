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
        var mobile = CustomerReferral.NormalizeMobile(request.ReferredMobileNumber);
        if (mobile == CustomerReferral.NormalizeMobile(referrer.MobileNumber))
            throw new DomainException("You cannot refer your own mobile number.");
        if (await referrals.ExistsForMobileAsync(request.ReferrerCustomerId, mobile, cancellationToken))
            throw new DomainException("You have already referred this mobile number.");

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        var referral = CustomerReferral.Create(request.ReferrerCustomerId, mobile, code, DateTime.UtcNow);
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
