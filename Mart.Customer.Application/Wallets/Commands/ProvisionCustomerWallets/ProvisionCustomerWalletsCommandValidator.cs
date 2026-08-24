using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;

public sealed class ProvisionCustomerWalletsCommandValidator
    : AbstractValidator<ProvisionCustomerWalletsCommand>
{
    public ProvisionCustomerWalletsCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .GreaterThan(0);
    }
}
