using Events.Common;
using GlobalIntegrationApi.Queries;
using GlobalIntegrationApi.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace GlobalIntegrationApi.Hubs;

public class StatusHub : Hub<INotificationClient>
{
    private readonly IGlobalDataQueries _globalDataQueries;
    private readonly IGlobalIntegrationServices _globalIntegrationServices;
    public StatusHub(IGlobalDataQueries globalDataQueries, IGlobalIntegrationServices globalIntegrationServices)
    {
        _globalDataQueries = globalDataQueries ?? throw new ArgumentNullException(nameof(globalDataQueries));
        _globalIntegrationServices = globalIntegrationServices ?? throw new ArgumentNullException(nameof(globalIntegrationServices));
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
    public async Task<bool> StopNamedConsumer(string identifier)
    {
        return await _globalIntegrationServices.StopNamedCosumer(identifier);
    }
    public async Task<bool> RestartNamedConsumer(string identifier)
    {
        return await _globalIntegrationServices.RestartNamedCosumer(identifier);
    }
    public async Task<bool> PostRsiMessage(RsiPostItem message)
    {
        return await _globalIntegrationServices.PostRsiMessage(message);
    }
    public async Task<IEnumerable<Content>> GetAllAudits(string sortColumn, string sortDirection, int pageNumber, int pageSize)
    {
        return await _globalDataQueries.GetAllAudits(sortColumn, sortDirection, pageNumber, pageSize);
    }
    public async Task<int> GetTotalRecords()
    {
        return await _globalDataQueries.GetTotalRecords();
    }
    public async Task<bool> DeleteEvent(Guid eventId)
    {
        return await _globalDataQueries.DeleteEvent(eventId);
    }
}

public interface INotificationClient
{
    Task SendStatusUpdate(string messageIdentifier, string statusData);
    Task<bool> StopNamedConsumer(string identifier);
    Task<bool> RestartNamedConsumer(string identifier);
}