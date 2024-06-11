using Azure.Core;
using EventBus.Abstractions;
using GlobalIntegrationApi.IntegrationEvents.Events;
using IntegrationEventLogEF.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Data.Common;

namespace GlobalIntegrationApi.Services;

public class GlobalIntegrationServices : IGlobalIntegrationServices
{
    private readonly IEventBus _eventBus;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly ILogger<GlobalIntegrationServices> _logger;

    public GlobalIntegrationServices(GlobalIntegrationContext globalIntegrationContext, IEventBus eventBus, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, ILogger<GlobalIntegrationServices> logger)
    {
        _eventBus = eventBus;
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory;
        _globalIntContext = globalIntegrationContext;
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger;
    }
    public async Task<bool> StopNamedCosumer(string consumerId)
    {
        _logger.LogInformation("Stopping named consumer for for consumerId: { @consumerId }", consumerId);
        var stopConsumerRequestIntegrationEvent = new StopConsumerRequestIntegrationEvent(consumerId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            await _eventLogService.SaveEventAsync(stopConsumerRequestIntegrationEvent, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }      
        await Task.Run(() => _eventBus.Publish(stopConsumerRequestIntegrationEvent));
        return true;
    }

    public async Task<bool> RestartNamedCosumer(string consumerId)
    {
        _logger.LogInformation("Restarting named consumer for for consumerId: { @consumerId }", consumerId);
        var restartConsumerRequestIntegrationEvent = new RestartConsumerRequestIntegrationEvent(consumerId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            await _eventLogService.SaveEventAsync(restartConsumerRequestIntegrationEvent, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }
        await Task.Run(() => _eventBus.Publish(restartConsumerRequestIntegrationEvent));
        return true;
    }
}
