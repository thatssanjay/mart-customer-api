using Microsoft.EntityFrameworkCore;
using Mart.Customer.Persistence.Auth;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Inventory;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    public DbSet<CashbackConfiguration> CashbackConfigurations => Set<CashbackConfiguration>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<UserMasterEntity> Users => Set<UserMasterEntity>();

    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    public DbSet<RoleMasterEntity> Roles => Set<RoleMasterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
