using Dapper;
using Hydra2.Service.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Hydra2.Service;

public class ConfigService : IConfigService
{
    private readonly string _connectionString;

    public ConfigService(IOptions<Hydra2Options> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public ConfigService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Config> GetFirstConfigAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<Config>(new CommandDefinition(
            "SELECT TOP 1 [Id], [Key], [Value] FROM [Hydra].[Config]",
            cancellationToken: cancellationToken));
    }

    public async Task UpdateConfigAsync(int id, int value, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Hydra].[Config] SET [Value] = @value WHERE [Id] = @id",
            new { id, value },
            cancellationToken: cancellationToken));
    }
}
