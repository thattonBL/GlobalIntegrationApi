using EventBus.Events;

namespace GlobalIntegrationApi.IntegrationEvents.Events;

public record RequestStatusChangedToCancelledIntegrationEvent : IntegrationEvent
{
    public int CommonId { get; init; }
    public string NewStatus { get; init; }
    public string Identifier { get; init; }
    public string EventName { get; init; }

    public static string EVENT_NAME = "RequestStatusChangedToCancelled.IntegrationEvent";

    public RequestStatusChangedToCancelledIntegrationEvent(int commonId, string identifier, string newStatus, string eventName)
    {
        CommonId = commonId;
        NewStatus = newStatus;
        EventName = eventName;
        Identifier = identifier;
    }
}
