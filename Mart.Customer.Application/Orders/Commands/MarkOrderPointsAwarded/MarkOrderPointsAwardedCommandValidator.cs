using FluentValidation;

namespace Mart.Customer.Application.Orders.Commands.MarkOrderPointsAwarded;

public sealed class MarkOrderPointsAwardedCommandValidator
    : AbstractValidator<MarkOrderPointsAwardedCommand>
{
    public MarkOrderPointsAwardedCommandValidator()
    {
        RuleFor(command => command.OrderId).GreaterThan(0);
        RuleFor(command => command.FranchiseId).GreaterThan(0);
        RuleFor(command => command.StoreId).GreaterThan(0);
    }
}
