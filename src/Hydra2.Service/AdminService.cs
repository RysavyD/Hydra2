using Dapper;
using Hydra2.Service.Data.Admin;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Hydra2.Service;

public class AdminService : IAdminService
{
    private readonly string _connectionString;

    public AdminService(IOptions<Hydra2Options> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public AdminService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> GetSamplesCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        var result = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            @"SELECT SUM([Rows]) FROM
              ( SELECT sysindexes.Rows AS [Rows]
                FROM sysobjects
                INNER JOIN sysindexes ON sysobjects.id = sysindexes.id
                WHERE type = 'U' AND sysindexes.IndId < 2 AND sysobjects.Name LIKE 'Sample%'
              ) a",
            cancellationToken: cancellationToken));
        return (int)(result ?? 0);
    }

    public async Task<IEnumerable<SpotOverviewModel>> GetSpotOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<SpotOverviewModel>(new CommandDefinition(
            @"SELECT st.[Id], r.[Name], st.[Spot], st.[Type], st.[Link]
              FROM [Hydra].[Station] st
              JOIN [Hydra].[River] r ON st.[Id_River] = r.[Id]
              ORDER BY r.[Name], st.[Spot]",
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<SpotOverviewModel>> GetSpotOverviewWithSamplesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        var spots = (await connection.QueryAsync<SpotOverviewModel>(new CommandDefinition(
            @"SELECT st.[Id], r.[Name], st.[Spot], st.[Type], st.[Link]
              FROM [Hydra].[Station] st
              JOIN [Hydra].[River] r ON st.[Id_River] = r.[Id]
              ORDER BY r.[Name], st.[Spot]",
            cancellationToken: cancellationToken))).ToList();

        foreach (var spot in spots)
        {
            var tableName = SampleTableName.ForStation(spot.Id);
            spot.LastSample = await connection.QueryFirstOrDefaultAsync<DateTime?>(new CommandDefinition(
                $"SELECT TOP 1 [TimeStamp] FROM [Hydra].[{tableName}] ORDER BY [TimeStamp] DESC",
                cancellationToken: cancellationToken));
        }

        return spots;
    }

    public async Task<DateTime?> GetLastSampleAsync(int spotId, CancellationToken cancellationToken = default)
    {
        var tableName = SampleTableName.ForStation(spotId);
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<DateTime?>(new CommandDefinition(
            $"SELECT TOP 1 [TimeStamp] FROM [Hydra].[{tableName}] ORDER BY [TimeStamp] DESC",
            cancellationToken: cancellationToken));
    }
}
