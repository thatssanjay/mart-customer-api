using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.ChangeCartStatus;

public sealed record ChangeCartStatusCommand(
    string CartNumber,
    string? CartStatus,
    long FranchiseId,
    long MartStoreId) : IRequest<ChangedCartStatusDto?>;
