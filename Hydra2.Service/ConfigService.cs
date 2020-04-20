using System.Data.SqlClient;
using Dapper;
using Hydra2.Service.Data;

namespace Hydra2.Service
{
    public class ConfigService
    {
        private readonly string _connectionString;

        public ConfigService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Config GetFirstConfig()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var result = sqlConnection.QuerySingle<Config>("SELECT TOP 1 [Id] ,[Key] ,[Value] FROM [Hydra].[Config]");
                sqlConnection.Close();
                return result;
            }
        }

        public void UpdateConfig(int id, int value)
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();

                var command = sqlConnection.CreateCommand();
                command.CommandText = "UPDATE [Hydra].[Config] SET [Value] = @Value WHERE [Id]=@Id";
                command.Parameters.Add(new SqlParameter("Value", value));
                command.Parameters.Add(new SqlParameter("Id", id));

                command.ExecuteNonQuery();
                
                sqlConnection.Close();
            }
        }
    }
}
