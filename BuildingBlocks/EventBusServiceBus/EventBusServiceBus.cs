using Azure.Messaging.ServiceBus.Administration;
using Azure.Messaging.ServiceBus;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace EventBusServiceBus;

public class EventBusServiceBus : IEventBus, IAsyncDisposable
{
    private readonly IServiceBusPersisterConnection _serviceBusPersisterConnection;
    private readonly ILogger<EventBusServiceBus> _logger;
    private readonly IEventBusSubscriptionsManager _subsManager;
    private readonly IServiceProvider _serviceProvider;
    //private readonly string _topicName;
    private readonly string _subscriptionName;
    //private ServiceBusSender _sender;
    private List<ServiceBusProcessor> _processorList;
    private const string INTEGRATION_EVENT_SUFFIX = "IntegrationEvent";
    private Dictionary<string, ServiceBusSender> _senderDictionary;

    public EventBusServiceBus(IServiceBusPersisterConnection serviceBusPersisterConnection,
        ILogger<EventBusServiceBus> logger, IEventBusSubscriptionsManager subsManager, IServiceProvider serviceProvider, string subscriptionClientName)
    {
        _serviceBusPersisterConnection = serviceBusPersisterConnection;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
        _serviceProvider = serviceProvider;
        //_topicName = topicName;
        _subscriptionName = subscriptionClientName;
        //_sender = _serviceBusPersisterConnection.TopicClient.CreateSender(_topicName);
        _senderDictionary = new Dictionary<string, ServiceBusSender>();
        _processorList = new List<ServiceBusProcessor>();
        //RemoveDefaultRule();
        //RegisterSubscriptionClientMessageHandlerAsync().GetAwaiter().GetResult();
    }

    public void Publish(IntegrationEvent @event)
    {
        var myEventName = @event.GetType().GetProperty("EventName").GetValue(@event).ToString();

        if (!_senderDictionary.ContainsKey(myEventName))
        {
            _senderDictionary.Add(myEventName, _serviceBusPersisterConnection.TopicClient.CreateSender(myEventName));
        }
        var sender = _senderDictionary[myEventName];

        var eventName = @event.GetType().Name;//.Replace(INTEGRATION_EVENT_SUFFIX, "");
        var jsonMessage = JsonSerializer.Serialize(@event, @event.GetType());
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var message = new ServiceBusMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = new BinaryData(body),
            Subject = eventName,
        };

        try
        {
            sender.SendMessageAsync(message)
                            .GetAwaiter()
                            .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR Publishing integration event: {EventId} from {AppName}", @event.Id, @event.GetType());
            throw;
        }
    }

    public void SubscribeDynamic<TH>(string eventName)
        where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

        _subsManager.AddDynamicSubscription<TH>(eventName);
    }

    public void Subscribe<T, TH>(string customRoutingKey = "")
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        
        var eventName = String.IsNullOrEmpty(customRoutingKey) ? typeof(T).Name.Replace(INTEGRATION_EVENT_SUFFIX, "") : customRoutingKey;
        
        ServiceBusProcessorOptions options = new ServiceBusProcessorOptions { MaxConcurrentCalls = 10, AutoCompleteMessages = false };
        
        var containsKey = _subsManager.HasSubscriptionsForEvent<T>(customRoutingKey);
        if (!containsKey)
        {
            try
            {
                var _processor = _serviceBusPersisterConnection.TopicClient.CreateProcessor(customRoutingKey, _subscriptionName, options);
                RegisterSubscriptionClientMessageHandlerAsync(_processor).GetAwaiter().GetResult();
                _processorList.Add(_processor);

                _serviceBusPersisterConnection.AdministrationClient.CreateRuleAsync(customRoutingKey, _subscriptionName, new CreateRuleOptions
                {
                    Filter = new CorrelationRuleFilter() { Subject = eventName },
                    Name = eventName
                }).GetAwaiter().GetResult();
            }
            catch (ServiceBusException)
            {
                _logger.LogWarning("The messaging entity {eventName} already exists.", eventName);
            }
        }

        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

        _subsManager.AddSubscription<T, TH>();
    }

    public void Unsubscribe<T, TH>(string customRoutingKey = "")
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        var eventName = String.IsNullOrEmpty(customRoutingKey) ? typeof(T).Name.Replace(INTEGRATION_EVENT_SUFFIX, "") : customRoutingKey;

        try
        {
            _serviceBusPersisterConnection
                .AdministrationClient
                .DeleteRuleAsync(customRoutingKey, _subscriptionName, eventName)
                .GetAwaiter()
                .GetResult();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            _logger.LogWarning("The messaging entity {eventName} Could not be found.", eventName);
        }

        _logger.LogInformation("Unsubscribing from event {EventName}", eventName);

        _subsManager.RemoveSubscription<T, TH>();
    }

    public void UnsubscribeDynamic<TH>(string eventName)
        where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Unsubscribing from dynamic event {EventName}", eventName);

        _subsManager.RemoveDynamicSubscription<TH>(eventName);
    }

    private async Task RegisterSubscriptionClientMessageHandlerAsync(ServiceBusProcessor processor)
    {
        processor.ProcessMessageAsync +=
            async (args) =>
            {
                var eventName = $"{args.Message.Subject}";
                string messageData = args.Message.Body.ToString();

                // Complete the message so that it is not received again.
                if (await ProcessEvent(eventName, messageData))
                {
                    await args.CompleteMessageAsync(args.Message);
                }
            };

        processor.ProcessErrorAsync += ErrorHandler;
        await processor.StartProcessingAsync();
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        var ex = args.Exception;
        var context = args.ErrorSource;

        _logger.LogError(ex, "Error handling message - Context: {@ExceptionContext}", context);

        return Task.CompletedTask;
    }

    private async Task<bool> ProcessEvent(string eventName, string message)
    {
        var processed = false;
        if (_subsManager.HasSubscriptionsForEvent(eventName))
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var subscriptions = _subsManager.GetHandlersForEvent(eventName);
            foreach (var subscription in subscriptions)
            {
                if (subscription.IsDynamic)
                {
                    if (scope.ServiceProvider.GetService(subscription.HandlerType) is not IDynamicIntegrationEventHandler handler) continue;

                    using dynamic eventData = JsonDocument.Parse(message);
                    await handler.Handle(eventData);
                }
                else
                {
                    var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                    if (handler == null) continue;
                    var eventType = _subsManager.GetEventTypeByName(eventName);
                    var integrationEvent = JsonSerializer.Deserialize(message, eventType);
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { integrationEvent });
                }
            }
        }
        processed = true;
        return processed;
    }

    //private void RemoveDefaultRule()
    //{
    //    try
    //    {
    //        _serviceBusPersisterConnection
    //            .AdministrationClient
    //            .DeleteRuleAsync(_topicName, _subscriptionName, RuleProperties.DefaultRuleName)
    //            .GetAwaiter()
    //            .GetResult();
    //    }
    //    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
    //    {
    //        _logger.LogWarning("The messaging entity {DefaultRuleName} Could not be found.", RuleProperties.DefaultRuleName);
    //    }
    //}

    public async ValueTask DisposeAsync()
    {
        _subsManager.Clear();
        //??? is this goign to work?
        foreach (var _processor in _processorList)
        {
            await _processor.CloseAsync();
        }
        
    }
}