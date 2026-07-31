using Mart.Customer.Application.Cashback.Dtos;
using MediatR;

namespace Mart.Customer.Application.Cashback.Queries.GetActiveCashbackSettings;

public sealed record GetActiveCashbackSettingsQuery : IRequest<IReadOnlyList<CashbackSettingDto>>;
