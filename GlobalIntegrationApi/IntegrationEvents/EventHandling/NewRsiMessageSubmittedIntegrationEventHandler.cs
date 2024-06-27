using EventBus.Abstractions;
using Events.Common.Events;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.Events;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;

namespace GlobalIntegrationApi.IntegrationEvents.EventHandling
{
    public class NewRsiMessageSubmittedIntegrationEventHandler : IIntegrationEventHandler<NewRsiMessageSubmittedIntegrationEvent>
    {
        private readonly ILogger<NewRsiMessageSubmittedIntegrationEventHandler> _logger;
        private readonly GlobalIntegrationContext _globalIntContext;
        private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
        private readonly IIntegrationEventLogService _eventLogService;
        private readonly IHubContext<StatusHub> _hubContext;

        public NewRsiMessageSubmittedIntegrationEventHandler(GlobalIntegrationContext context, Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory, ILogger<NewRsiMessageSubmittedIntegrationEventHandler> logger, IHubContext<StatusHub> hubContext)
        {
            _globalIntContext = context ?? throw new ArgumentException(nameof(context));
            _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentException(nameof(integrationEventLogServiceFactory));
            _eventLogService = _integrationEventLogServiceFactory(_globalIntContext.Database.GetDbConnection());
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task Handle(NewRsiMessageSubmittedIntegrationEvent @event)
        {
            // Send update to SignalR clients
            await _hubContext.Clients.All.SendAsync("SubmittedStatusUpdate", new
            {
                Identifier = @event.RsiMessage.Identifier,
                EventName = @event.EventName,
                CreationDate = @event.CreationDate,
                CollectionCode = @event.RsiMessage.CollectionCode
            });

            //TODO Agg try catch around this also what role does IMediator play in the Context????
            //Console.WriteLine($"New RSI GLOBAAAAALL !!!!!!!!  INtegration message submitted: {@event.RsiMessage.Identifier}");
            _logger.LogInformation("Global Integration event received: { @event } for Idebtifier: { @identifier }", @event.GetType(), @event.RsiMessage.Identifier);
            await using var transaction = await _globalIntContext.BeginTransactionAsync();
            {
                //TODO could loop through backed up messages here if required???
                await _eventLogService.SaveEventAsync(@event, _globalIntContext.GetCurrentTransaction());
                await _globalIntContext.CommitTransactionAsync(transaction);
            }
        }
    }
}
