using EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using GlobalIntegrationApi.Queries;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Data.Common;

namespace GlobalIntegrationApi.IntegrationEvents.EventHandling;

public class RsiMessagePublishedIntegrationEventHandler : IIntegrationEventHandler<RsiMessagePublishedIntegrationEvent>
{
    private readonly ILogger<RsiMessagePublishedIntegrationEventHandler> _logger;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly IHubContext<StatusHub, INotificationClient> _hubContext;
    private readonly IGlobalDataQueries _globalDataQueries;

    public RsiMessagePublishedIntegrationEventHandler(GlobalIntegrationContext context, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, 
        ILogger<RsiMessagePublishedIntegrationEventHandler> logger, IHubContext<StatusHub, INotificationClient> hubContext, IGlobalDataQueries globalDataQueries)
    {
        _globalIntContext = context ?? throw new ArgumentException(nameof(context));
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentException(nameof(integrationEventLogServiceFactory));
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _globalDataQueries = globalDataQueries ?? throw new ArgumentNullException(nameof(globalDataQueries));
    }

    public async Task Handle(RsiMessagePublishedIntegrationEvent @event)
    {
        //TODO Agg try catch around this also what role does IMediator play in the Context????
        //Console.WriteLine($"New RSI PUBLISSHHHED ------  GLOBAAAAALL !!!!!!!!  Integration message submitted: {@event.RsiMessageId}");
        _logger.LogInformation("Global Integration event received: { @event } for Identifier: { @identifier }", @event.GetType(), @event.RsiMessageId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            //TODO could loop through backed up messages here if required???
            await _eventLogService.SaveEventAsync(@event, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }

        //update the client here with the new data once we are sure it has been committed to the database
        //if the database rolls back then the data won't be in the database and the client will just recieve the data it already had
        var newAuditForId = await _globalDataQueries.GetAuditForIdentifier(@event.RsiMessageId);
        await _hubContext.Clients.All.SendStatusUpdate(@event.RsiMessageId, JsonConvert.SerializeObject(newAuditForId));
    }
}
