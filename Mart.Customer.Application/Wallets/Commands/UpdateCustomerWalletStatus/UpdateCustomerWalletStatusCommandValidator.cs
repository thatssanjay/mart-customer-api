using FluentValidation;

namespace Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;

public sealed class UpdateCustomerWalletStatusCommandValidator
    : AbstractValidator<UpdateCustomerWalletStatusCommand>
{
    public UpdateCustomerWalletStatusCommandValidator()
    {
        RuleFor(command => command.CustomerId).GreaterThan(0);
        RuleFor(command => command.WalletTypeId).GreaterThan(0);
        RuleFor(command => command.IsActive).NotNull();
    }
}
