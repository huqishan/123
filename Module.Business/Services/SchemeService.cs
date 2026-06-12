using ControlLibrary;
using ControlLibrary.Models.EventsModels.TestBusiness;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Mediator;

namespace Module.Business.Services;

public class SchemeService : ModuleService
{
    public SchemeService(
        IEventAggregator eventAggregator,
        IMediator mediator)
    {
        _EventAggregator = eventAggregator;
        SchemeExecutionService.ConfigureEventAggregator(eventAggregator);
        SchemeExecutionService.ConfigureMediator(mediator);
        _EventAggregator.GetEvent<ShemeExecutionMessage>().Subscribe(ShemeExecutionHandle);
    }

    private void ShemeExecutionHandle(ShemeExecutionMessage obj)
    {
        _ = SchemeExecutionService.ExecuteAsync(
            obj.StationName ?? string.Empty,
            obj.SchemeName ?? string.Empty);
    }
}
