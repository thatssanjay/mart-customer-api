using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.CreateCart;

public sealed class CreateCartCommandHandler : IRequestHandler<CreateCartCommand, CreatedCartDto>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCartCommandHandler(ICustomerCartRepository cartRepository, IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatedCartDto> Handle(CreateCartCommand request, CancellationToken cancellationToken)
    {
        var cartNumber = await GenerateUniqueCartNumberAsync(cancellationToken);
        var cart = CustomerCart.Create(
            request.CustomerId,
            request.FranchiseId,
            request.MartStoreId,
            cartNumber,
            request.AddedByCashierId);

        await _cartRepository.AddAsync(cart, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedCartDto(cart.CustomerCartId, cart.CartNumber);
    }

    private async Task<string> GenerateUniqueCartNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var cartNumber = ReferenceCodeGenerator.GenerateWithPrefix("CART");
            if (!await _cartRepository.ExistsByCartNumberAsync(cartNumber, cancellationToken))
            {
                return cartNumber;
            }
        }

        throw new DomainException("Unable to generate a unique cart number.");
    }
}
