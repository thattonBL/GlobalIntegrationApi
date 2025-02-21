using Dapper;
using GlobalIntegrationApi.Models;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using System.Data;
using System.Data.SqlClient;

namespace GlobalIntegrationApi.Queries;

public class GlobalDataQueries : IGlobalDataQueries
{
    private string _connectionString = string.Empty;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<GlobalDataQueries> _logger;

    public GlobalDataQueries(string constr, ILogger<GlobalDataQueries> logger)
    {
        _connectionString = !string.IsNullOrWhiteSpace(constr) ? constr : throw new ArgumentNullException(nameof(constr));
        _logger = logger;
        _retryPolicy = Policy.Handle<SqlException>(ex => IsTransient(ex))
                                .Or<TimeoutException>()
                                .WaitAndRetryAsync(
                                    retryCount: 3,
                                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                    onRetry: (exception, timeSpan, context) =>
                                    {
                                        // Log or handle the retry attempt
                                        _logger.LogInformation($"Retrying due to: {exception.Message}");
                                    });
    }

    public async Task<List<string>> GetAllIdentifiers()
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                if (connection.State == ConnectionState.Closed)
                {
                    try
                    {
                        await connection.OpenAsync();
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
        });
    }

    public async Task<IEnumerable<Content>> GetAuditForIdentifier(string msgIdentifier)
    {

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                if (connection.State == ConnectionState.Closed)
                {
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                string query = @"
                        SELECT * FROM Global_Integration.dbo.IntegrationEventLog
                        WHERE JSON_VALUE(Content, '$.RsiMessageId') LIKE @SearchTerm
                        OR JSON_VALUE(Content, '$.RsiMessage.Identifier') LIKE @SearchTerm
                        OR JSON_VALUE(Content, '$.Identifier') LIKE @SearchTerm
                        ORDER BY CreationTime DESC";

                var parameters = new { SearchTerm = $"%{msgIdentifier}%" };

                IEnumerable<IntegrationEventLog> logs = await connection.QueryAsync<IntegrationEventLog>(query, parameters);

                return logs.Select(log =>
                {
                    var content = JObject.Parse(log.Content);
                    var eventName = content["EventName"]?.ToString();
                    var creationTime = log.CreationTime.ToString();
                    var appName = content["AppName"]?.ToString() ?? string.Empty;
                    var identifier = content["RsiMessage"]?["Identifier"]?.ToString() ?? content["RsiMessageId"]?.ToString() ?? content["Identifier"]?.ToString();
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
        });
    }

    public async Task<IEnumerable<Content>> GetAllAudits(string sortColumn, string sortDirection, int pageNumber, int pageSize)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                if (connection.State == ConnectionState.Closed)
                {
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                // Construct the ORDER BY clause based on the sortColumn
                string orderByClause;
                switch (sortColumn.ToLower())
                {
                    case "title":
                        orderByClause = "JSON_VALUE(Content, '$.Title')";
                        break;
                    case "author":
                        orderByClause = "JSON_VALUE(Content, '$.Author')";
                        break;
                    case "creationdate":
                        orderByClause = "JSON_VALUE(Content, '$.CreationDate')";
                        break;
                    case "collectioncode":
                        orderByClause = "JSON_VALUE(Content, '$.CollectionCode')";
                        break;
                    default:
                        // Default to sorting by CreationTime if sortColumn is not recognized
                        orderByClause = "CreationTime";
                        break;
                }

                // Add the sort direction
                orderByClause += $" {sortDirection}";

                // Calculate the offset for pagination
                int offset = (pageNumber - 1) * pageSize;

                // Construct the final query with pagination    
                string query = $@"
                    SELECT *, JSON_VALUE(Content, '$.RsiMessageId') 
                    FROM Global_Integration.dbo.IntegrationEventLog
                    ORDER BY {orderByClause}, EventId
                    OFFSET {offset} ROWS
                    FETCH NEXT {pageSize} ROWS ONLY";

                IEnumerable<IntegrationEventLog> logs = await connection.QueryAsync<IntegrationEventLog>(query);

                return logs.Select(log =>
                {
                    var content = JObject.Parse(log.Content);
                    var eventName = content["EventName"]?.ToString();
                    var creationTime = log.CreationTime.ToString();
                    var appName = content["AppName"]?.ToString() ?? string.Empty;
                    var identifier = content["RsiMessage"]?["Identifier"]?.ToString() ?? content["RsiMessageId"]?.ToString() ?? content["Identifier"]?.ToString();
                    var collectionCode = content["RsiMessage"]?["CollectionCode"]?.ToString() ?? string.Empty;
                    var title = content["RsiMessage"]?["Title"]?.ToString() ?? string.Empty;
                    var author = content["RsiMessage"]?["Author"]?.ToString() ?? string.Empty;
                    return new Content
                    {
                        EventName = eventName,
                        AppName = appName,
                        Identifier = identifier,
                        CollectionCode = collectionCode,
                        CreationDate = creationTime,
                        EventId = log.EventId.ToString(),
                        TransactionId = log.TransactionId,
                        Title = title,
                        Author = author,
                        CreationTime = creationTime,
                    };
                });
            }
        });
    }

    public async Task<int> GetTotalRecords()
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                if (connection.State == ConnectionState.Closed)
                {
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }

                string query = @"
                SELECT COUNT(EventId) FROM Global_Integration.dbo.IntegrationEventLog";

                return await connection.ExecuteScalarAsync<int>(query);
            }
        });
    }

    public async Task<bool> DeleteEvent(Guid eventId)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                if (connection.State == ConnectionState.Closed)
                {
                    try
                    {
                        await connection.OpenAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to open database connection: {ex.Message}", ex);
                    }
                }

                string query = @"DELETE FROM Global_Integration.dbo.IntegrationEventLog
                                 WHERE JSON_VALUE(Content, '$.RsiMessage.Identifier') =
                                 (SELECT JSON_VALUE(Content, '$.RsiMessage.Identifier')
                                 FROM Global_Integration.dbo.IntegrationEventLog
                                 WHERE EventId = @eventId)";

                try
                {
                    var rowsAffected = await connection.ExecuteAsync(query, new { EventId = eventId.ToString() });
                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing delete query: {ex.Message}", ex);
                }
            }
        });
    }

    private bool IsTransient(SqlException ex)
    {
        // Check the exception code and return true if it is transient
        // List of transient error numbers can be found here:
        // https://docs.microsoft.com/en-us/azure/sql-database/sql-database-develop-error-messages
        var transientErrorNumbers = new[] { 4060, 10928, 10929, 40197, 40501, 40613 };
        return Array.Exists(transientErrorNumbers, e => e == ex.Number);
    }
}
