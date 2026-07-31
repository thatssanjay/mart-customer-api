using Mart.Customer.Application.Auth.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IInternalUserRepository
{
    Task<InternalUserAccountDto?> GetByLoginIdAsync(string loginId, CancellationToken cancellationToken = default);
}
