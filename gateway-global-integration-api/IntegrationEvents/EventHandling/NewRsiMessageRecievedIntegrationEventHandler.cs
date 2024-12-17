using BL.Gateway.EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using GlobalIntegrationApi.Queries;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data.Common;

namespace GlobalIntegrationApi.IntegrationEvents.EventHandling;

public class NewRsiMessageRecievedIntegrationEventHandler : IIntegrationEventHandler<NewRsiMessageRecievedIntegrationEvent>
{
    private readonly ILogger<NewRsiMessageRecievedIntegrationEventHandler> _logger;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly IHubContext<StatusHub, INotificationClient> _hubContext;
    private readonly IGlobalDataQueries _globalDataQueries;

    public NewRsiMessageRecievedIntegrationEventHandler(GlobalIntegrationContext context, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, 
        ILogger<NewRsiMessageRecievedIntegrationEventHandler> logger, IHubContext<StatusHub, INotificationClient> hubContext, IGlobalDataQueries globalDataQueries)
    {
        _globalIntContext = context ?? throw new ArgumentException(nameof(context));
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentException(nameof(integrationEventLogServiceFactory));
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _globalDataQueries = globalDataQueries ?? throw new ArgumentNullException(nameof(globalDataQueries));
    }

    public async Task Handle(NewRsiMessageRecievedIntegrationEvent @event)
    {
        //TODO Agg try catch around this also what role does IMediator play in the Context????
        //Console.WriteLine($"New RSI RECIEVED ------  GLOBAAAAALL !!!!!!!!  Integration message submitted: {@event.RsiMessageId}");
        _logger.LogInformation("Global Integration event received: { @event } for Identifier: { @identifier }", @event.GetType(), @event.RsiMessageId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            //TODO could loop through backed up messages here if required???
            await _eventLogService.SaveEventAsync(@event, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }

        //update the client here
        var newAuditForId = await _globalDataQueries.GetAuditForIdentifier(@event.RsiMessageId);
        await _hubContext.Clients.All.SendStatusUpdate(@event.RsiMessageId, JsonConvert.SerializeObject(newAuditForId));
    }
}
