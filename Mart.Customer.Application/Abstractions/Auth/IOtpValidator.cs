namespace Mart.Customer.Application.Abstractions.Auth;

public interface IOtpValidator
{
    Task<bool> IsValidAsync(string mobileNumber, string otp, CancellationToken cancellationToken = default);
}
