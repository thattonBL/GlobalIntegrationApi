using Azure.Core;
using EventBus.Abstractions;
using GlobalIntegrationApi.Dtos;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace GlobalIntegrationApi.Services;

public class GlobalIntegrationServices : IGlobalIntegrationServices
{
    private readonly IEventBus _eventBus;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly ILogger<GlobalIntegrationServices> _logger;
    private readonly IHubContext<StatusHub> _hubContext;

    public GlobalIntegrationServices(GlobalIntegrationContext globalIntegrationContext, IEventBus eventBus, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, ILogger<GlobalIntegrationServices> logger, IHubContext<StatusHub> hubContext)
    {
        _eventBus = eventBus;
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory;
        _globalIntContext = globalIntegrationContext;
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger;
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }
    public async Task<bool> StopNamedCosumer(string consumerId)
    {
        _logger.LogInformation("Stopping named consumer for for consumerId: { @consumerId }", consumerId);
       
        // Send update to SignalR clients
        await _hubContext.Clients.All.SendAsync("StoppedNamedConsumer", new
        {
            ConsumerId = consumerId
        });

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

        // Send update to SignalR clients
        await _hubContext.Clients.All.SendAsync("StoppedNamedConsumer", new
        {
            ConsumerId = consumerId
        });

        var restartConsumerRequestIntegrationEvent = new RestartConsumerRequestIntegrationEvent(consumerId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            await _eventLogService.SaveEventAsync(restartConsumerRequestIntegrationEvent, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }
        await Task.Run(() => _eventBus.Publish(restartConsumerRequestIntegrationEvent));
        return true;
    }

    // Fetch statuses from the database
    public async Task<List<StatusDto>> GetStatusesAsync()
    {
        if (_globalIntContext.IntegrationEventLogs == null)
        {
            return new List<StatusDto>();
        }

        var logs = await _globalIntContext.IntegrationEventLogs.ToListAsync();

        var statuses = logs.Select(log =>
        {
            var content = JObject.Parse(log.Content);
            var eventName = content["EventName"]?.ToString();
            var creationTime = log.CreationTime.ToString();
            var identifier = content["RsiMessage"]?["Identifier"]?.ToString() ?? content["RsiMessageId"]?.ToString();
            var collectionCode = content["RsiMessage"]?["CollectionCode"]?.ToString() ?? string.Empty;
            var transactionId = log.TransactionId;

            return new StatusDto
            {
                EventId = log.EventId.ToString(),
                EventName = eventName,
                Identifier = identifier,
                CreationTime = creationTime,
                CollectionCode = collectionCode,
                TransactionId = transactionId
            };
        }).ToList();

        return statuses;
    }

}
