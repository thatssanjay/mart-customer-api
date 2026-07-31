namespace Mart.Customer.Application.Abstractions.Auth;

public interface IPasswordVerifier
{
    bool Verify(string password, string storedHash, string storedSalt);
}
