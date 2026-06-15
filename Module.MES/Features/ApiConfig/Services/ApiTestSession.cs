using ControlLibrary.Models.MediatorModels.MES;
using Shared.Infrastructure.Mediator;

namespace Module.MES.Features.ApiConfig.Services;

public sealed class ApiTestSession
{
    private readonly IMediator _mediator;

    public ApiTestSession(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public Task<ExecuteMesResponse> ExecuteAsync(string apiName, string requestPayload, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(new ExecuteMesRequest(apiName, requestPayload), cancellationToken);
    }
}
