using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.VerifyOrder;

public sealed record VerifyOrderQuery(string VerificationCode)
    : IRequest<OrderVerificationDto?>;
