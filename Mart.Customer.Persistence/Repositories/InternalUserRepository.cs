using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class InternalUserRepository : IInternalUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public InternalUserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InternalUserAccountDto?> GetByLoginIdAsync(string loginId, CancellationToken cancellationToken = default)
    {
        var normalizedLoginId = loginId.Trim();

        var account = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Username == normalizedLoginId && user.IsActive)
            .Select(user => new
            {
                user.UserId,
                user.Username,
                user.DisplayName,
                user.PasswordHash,
                user.PasswordSalt,
                Role = user.UserRoles
                    .Where(userRole => userRole.IsActive && userRole.Role.IsActive)
                    .OrderBy(userRole => userRole.Role.RoleCode == "SUPER_ADMIN" ? 0 : 1)
                    .ThenByDescending(userRole => userRole.AssignedOn)
                    .Select(userRole => userRole.Role.RoleCode)
                    .FirstOrDefault() ?? "User",
                Access = _dbContext.UserMartAccesses
                    .Where(access =>
                        access.UserId == user.UserId &&
                        access.CanAccess &&
                        access.FranchiseId.HasValue &&
                        access.FranchiseId > 0 &&
                        access.MartStoreId.HasValue &&
                        access.MartStoreId > 0)
                    .OrderBy(access => access.UserMartAccessId)
                    .Select(access => new
                    {
                        FranchiseId = access.FranchiseId!.Value,
                        StoreId = access.MartStoreId!.Value
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        return new InternalUserAccountDto(
            account.UserId.ToString(),
            account.Username,
            !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName : account.Username,
            account.Role,
            account.PasswordHash ?? string.Empty,
            account.PasswordSalt ?? string.Empty,
            account.Access?.FranchiseId,
            account.Access?.StoreId);
    }

    public Task<MartUserAccessScopeDto?> GetAccessScopeAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserMartAccesses
            .AsNoTracking()
            .Where(access =>
                access.UserId == userId &&
                access.CanAccess &&
                access.FranchiseId.HasValue &&
                access.FranchiseId > 0 &&
                access.MartStoreId.HasValue &&
                access.MartStoreId > 0)
            .OrderBy(access => access.UserMartAccessId)
            .Select(access => new MartUserAccessScopeDto(
                access.FranchiseId!.Value,
                access.MartStoreId!.Value))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
