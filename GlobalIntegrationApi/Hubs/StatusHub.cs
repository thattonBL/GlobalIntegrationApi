using GlobalIntegrationApi.Queries;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace GlobalIntegrationApi.Hubs;

public class StatusHub : Hub<INotificationClient>
{
    private readonly IGlobalDataQueries _globalDataQueries;
    public StatusHub(IGlobalDataQueries globalDataQueries)
    {
        _globalDataQueries = globalDataQueries ?? throw new ArgumentNullException(nameof(globalDataQueries));
    }
    
    public override async Task OnConnectedAsync()
    {
        var identifierList = await _globalDataQueries.GetAllIdentifiers();
        foreach (var identifier in identifierList)
        {
            var newAuditForId = await _globalDataQueries.GetAuditForIdentifier(identifier);
            await Clients.Client(Context.ConnectionId).SendStatusUpdate(identifier, JsonConvert.SerializeObject(newAuditForId));
        }

        await base.OnConnectedAsync();
    }
}

public interface INotificationClient
{
    Task SendStatusUpdate(string messageIdentifier, string statusData);
}