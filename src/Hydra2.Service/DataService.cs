using System.Data;
using Dapper;
using Hydra2.Service.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Hydra2.Service;

public class DataService : IDataService
{
    private readonly string _connectionString;

    public DataService(IOptions<Hydra2Options> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public DataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<River>> GetRiversAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<River>(new CommandDefinition(
            "SELECT [Id], [Name], [RaftLink] FROM [Hydra].[River]",
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Station>> GetStationsAsync(int riverId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Station>(new CommandDefinition(
            @"SELECT [Id], [Spot], [Spa_val], [Spa0], [Spa1], [Spa2], [Spa3], [Spa3e], [Type], [Link], [Id_River], [DownLoadType]
              FROM [Hydra].[Station]
              WHERE Id_River = @riverId",
            new { riverId },
            cancellationToken: cancellationToken));
    }

    public async Task<Station?> GetStationAsync(int stationId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Station>(new CommandDefinition(
            @"SELECT st.[Id], [Spot], [Spa_val], [Spa0], [Spa1], [Spa2], [Spa3], [Spa3e], [Type], [Link], [DownLoadType], r.RaftLink
              FROM [Hydra].[Station] st
              JOIN [Hydra].[River] r ON st.Id_River = r.Id
              WHERE st.Id = @stationId",
            new { stationId },
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Sample>> GetSamplesAsync(int spot, DateTime startDate, DateTime stopDate, CancellationToken cancellationToken = default)
    {
        var tableName = SampleTableName.ForStation(spot);
        var parameters = new DynamicParameters();
        parameters.Add("startDate", startDate, DbType.DateTime);
        parameters.Add("stopDate", stopDate, DbType.DateTime);

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Sample>(new CommandDefinition(
            $@"SELECT [TimeStamp], [Level], [Flow], [Temperature]
               FROM [Hydra].[{tableName}]
               WHERE [TimeStamp] >= @startDate AND [TimeStamp] <= @stopDate
               ORDER BY [TimeStamp]",
            parameters,
            cancellationToken: cancellationToken));
    }

    public async Task<int> AddSampleAsync(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp, CancellationToken cancellationToken = default)
    {
        var tableName = SampleTableName.ForStation(stationId);

        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(new CommandDefinition(
            $@"IF NOT EXISTS (SELECT 1 FROM [Hydra].[{tableName}] WHERE [TimeStamp] = @sampleTimeStamp)
                   INSERT INTO [Hydra].[{tableName}] ([TimeStamp], [Level], [Flow], [Temperature])
                   VALUES (@sampleTimeStamp, @sampleLevel, @sampleFlow, @sampleTemperature)",
            new
            {
                sampleTimeStamp,
                sampleLevel,
                sampleFlow,
                sampleTemperature
            },
            cancellationToken: cancellationToken));
    }
}
