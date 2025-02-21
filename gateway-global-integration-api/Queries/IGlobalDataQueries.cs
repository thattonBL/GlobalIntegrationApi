namespace GlobalIntegrationApi.Queries;

public interface IGlobalDataQueries
{
    Task<IEnumerable<Content>> GetAllAudits(string sortColumn, string sortDirection, int pageNumber, int pageSize);
    Task<int> GetTotalRecords();
    Task<bool> DeleteEvent(Guid eventId);
    Task<List<string>> GetAllIdentifiers();
    Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier);
}
