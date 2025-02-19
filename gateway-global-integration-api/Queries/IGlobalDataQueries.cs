namespace GlobalIntegrationApi.Queries;

public interface IGlobalDataQueries
{
    Task<IEnumerable<Content>> GetAllAudits(string sortColumn, string sortDirection);
    Task<bool> DeleteEvent(Guid eventId);
    Task<List<string>> GetAllIdentifiers();
    Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier);
}
