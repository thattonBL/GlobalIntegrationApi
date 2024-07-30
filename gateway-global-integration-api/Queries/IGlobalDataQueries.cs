namespace GlobalIntegrationApi.Queries;

public interface IGlobalDataQueries
{
    Task<List<string>> GetAllIdentifiers();
    Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier);
}
