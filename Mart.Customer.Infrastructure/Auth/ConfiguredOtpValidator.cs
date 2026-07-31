using Mart.Customer.Application.Abstractions.Auth;
using Microsoft.Extensions.Configuration;

namespace Mart.Customer.Infrastructure.Auth;

public sealed class ConfiguredOtpValidator : IOtpValidator
{
    private readonly string _customerLoginOtp;

    public ConfiguredOtpValidator(IConfiguration configuration)
    {
        _customerLoginOtp = configuration["Auth:CustomerLoginOtp"] ?? "123456";
    }

    public Task<bool> IsValidAsync(string mobileNumber, string otp, CancellationToken cancellationToken = default)
    {
        _ = mobileNumber;
        _ = cancellationToken;

        return Task.FromResult(string.Equals(otp.Trim(), _customerLoginOtp, StringComparison.Ordinal));
    }
}
