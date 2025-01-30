namespace GlobalIntegrationApi.Queries;

public interface IGlobalDataQueries
{
    Task<IEnumerable<Content>> GetAllAudits();
    Task<bool> DeleteEvent(Guid eventId);
    Task<List<string>> GetAllIdentifiers();
    Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier);
}
