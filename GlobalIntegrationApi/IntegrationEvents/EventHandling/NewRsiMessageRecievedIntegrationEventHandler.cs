using EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;

namespace GlobalIntegrationApi.IntegrationEvents.EventHandling;

public class NewRsiMessageRecievedIntegrationEventHandler : IIntegrationEventHandler<NewRsiMessageRecievedIntegrationEvent>
{
    private readonly ILogger<NewRsiMessageRecievedIntegrationEventHandler> _logger;
    private readonly GlobalIntegrationContext _globalIntContext;
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly IHubContext<StatusHub> _hubContext;

    public NewRsiMessageRecievedIntegrationEventHandler(GlobalIntegrationContext context, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, ILogger<NewRsiMessageRecievedIntegrationEventHandler> logger, IHubContext<StatusHub> hubContext)
    {
        _globalIntContext = context ?? throw new ArgumentException(nameof(context));
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentException(nameof(integrationEventLogServiceFactory));
        _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    public async Task Handle(NewRsiMessageRecievedIntegrationEvent @event)
    {
        // Send update to SignalR clients
        await _hubContext.Clients.All.SendAsync("ReceivedStatusUpdate", new
        {
            Identifier = @event.RsiMessageId,
            EventName = @event.EventName,
            CreationDate = DateTime.UtcNow
        });

        //TODO Agg try catch around this also what role does IMediator play in the Context????
        //Console.WriteLine($"New RSI RECIEVED ------  GLOBAAAAALL !!!!!!!!  Integration message submitted: {@event.RsiMessageId}");
        _logger.LogInformation("Global Integration event received: { @event } for Identifier: { @identifier }", @event.GetType(), @event.RsiMessageId);
        await using var transaction = await _globalIntContext.BeginTransactionAsync();
        {
            //TODO could loop through backed up messages here if required???
            await _eventLogService.SaveEventAsync(@event, _globalIntContext.GetCurrentTransaction());
            await _globalIntContext.CommitTransactionAsync(transaction);
        }
    }
}
