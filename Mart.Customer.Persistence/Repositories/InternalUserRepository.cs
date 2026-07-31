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

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Username == normalizedLoginId && user.IsActive)
            .Select(user => new InternalUserAccountDto(
                user.UserId.ToString(),
                user.Username,
                !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : user.Username,
                user.UserRoles
                    .Where(userRole => userRole.IsActive && userRole.Role.IsActive)
                    .OrderBy(userRole => userRole.Role.RoleCode == "SUPER_ADMIN" ? 0 : 1)
                    .ThenByDescending(userRole => userRole.AssignedOn)
                    .Select(userRole => userRole.Role.RoleCode)
                    .FirstOrDefault() ?? "User",
                user.PasswordHash ?? string.Empty,
                user.PasswordSalt ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
