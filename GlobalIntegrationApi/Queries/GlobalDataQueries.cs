using Dapper;
using GlobalIntegrationApi.Models;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;

namespace GlobalIntegrationApi.Queries;

public class GlobalDataQueries : IGlobalDataQueries
{
    private string _connectionString = string.Empty;
    public GlobalDataQueries(string constr)
    {
        _connectionString = !string.IsNullOrWhiteSpace(constr) ? constr : throw new ArgumentNullException(nameof(constr));
    }

    public async Task<List<string>> GetAllIdentifiers()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            if (connection.State == ConnectionState.Closed)
            {
                try
                {
                    connection.Open();
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
            string query = @"
                SELECT JSON_VALUE(Content, '$.RsiMessage.Identifier') AS FieldValue
                FROM Global_Integration.dbo.IntegrationEventLog
                WHERE JSON_VALUE(Content, '$.RsiMessage.Identifier') IS NOT NULL
                ORDER BY CreationTime";
            return (await connection.QueryAsync<string>(query)).ToList();
        }
    }

    public async Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            if (connection.State == ConnectionState.Closed){
                try{
                    connection.Open();
                }
                catch (Exception ex){
                    throw new Exception(ex.Message);
                }
            }

            string query = @"
                SELECT * FROM Global_Integration.dbo.IntegrationEventLog
                WHERE JSON_VALUE(Content, '$.RsiMessageId') LIKE @SearchTerm
                OR JSON_VALUE(Content, '$.RsiMessage.Identifier') LIKE @SearchTerm
                ORDER BY CreationTime DESC";

            var parameters = new { SearchTerm = $"%{msgIdentifier}%" };

            IEnumerable<IntegrationEventLog> logs = await connection.QueryAsync<IntegrationEventLog>(query, parameters);

            return logs.Select(log =>
            {
                var content = JObject.Parse(log.Content);
                var eventName = content["EventName"]?.ToString();
                var creationTime = log.CreationTime.ToString();
                var appName = content["AppName"]?.ToString() ?? string.Empty;
                var identifier = content["RsiMessage"]?["Identifier"]?.ToString() ?? content["RsiMessageId"]?.ToString();
                var collectionCode = content["RsiMessage"]?["CollectionCode"]?.ToString() ?? string.Empty;
                return new Content
                {
                    EventName = eventName,
                    AppName = appName,
                    Identifier = identifier,
                    CollectionCode = collectionCode,
                    CreationDate = creationTime,
                    EventId = log.EventId.ToString(),
                    TransactionId = log.TransactionId
                };
            });
        }
    }
}
