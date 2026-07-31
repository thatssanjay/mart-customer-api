using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Cashback.Dtos;
using MediatR;

namespace Mart.Customer.Application.Cashback.Queries.GetActiveCashbackSettings;

public sealed class GetActiveCashbackSettingsQueryHandler
    : IRequestHandler<GetActiveCashbackSettingsQuery, IReadOnlyList<CashbackSettingDto>>
{
    private readonly ICashbackConfigurationRepository _repository;

    public GetActiveCashbackSettingsQueryHandler(ICashbackConfigurationRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<CashbackSettingDto>> Handle(
        GetActiveCashbackSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return _repository.GetActiveAsync(DateTime.UtcNow, cancellationToken);
    }
}
