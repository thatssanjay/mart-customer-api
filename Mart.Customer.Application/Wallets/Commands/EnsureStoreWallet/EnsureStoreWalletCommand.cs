using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.EnsureStoreWallet;

// StoreId must come from the authorized cart/order, never from a payment request body.
public sealed record EnsureStoreWalletCommand(long CustomerId, long StoreId) : IRequest;
