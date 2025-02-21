namespace GlobalIntegrationApi.Queries;

public record Content
{
    public string EventName { get; init; }
    public string AppName { get; init; }
    public string Identifier { get; init; }
    public string CollectionCode { get; init; }
    public string CreationDate { get; init; }
    public string EventId { get; set; }
    public string TransactionId { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string CreationTime { get; set; }
}