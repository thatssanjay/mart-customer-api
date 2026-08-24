using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderCheckoutPreview;

public sealed record GetOrderCheckoutPreviewQuery(
    string? CartNumber,
    long FranchiseId,
    long MartStoreId,
    int? WalletTypeId,
    decimal? RedemptionAmount) : IRequest<OrderCheckoutPreviewDto?>;
