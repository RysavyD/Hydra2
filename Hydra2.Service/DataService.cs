using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Dapper;
using Hydra2.Service.Data;

namespace Hydra2.Service
{
    public class DataService : IDataService
    {
        private readonly string _connectionString;

        public DataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IEnumerable<River> GetRivers()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var rivers = sqlConnection.Query<River>("SELECT [Id] ,[Name],[RaftLink] FROM[Hydra].[River]");
                sqlConnection.Close();
                return rivers;
            }
        }

        public IEnumerable<Station> GetStations(int riverId)
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var stations = sqlConnection.Query<Station>(
                    @"SELECT [Id] ,[Spot] ,[Spa_val] ,[Spa0] ,[Spa1] ,[Spa2] ,[Spa3] ,[Spa3e] ,[Type] ,[Link] ,[Id_River] ,[DownLoadType]
                FROM [Hydra].[Station] WHERE Id_River = @riverId", new {riverId});
                sqlConnection.Close();
                return stations;
            }
        }

        public Station GetStation(int stationId)
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var station = sqlConnection.QuerySingleOrDefault<Station>(
                    @"SELECT st.[Id],[Spot],[Spa_val],[Spa0],[Spa1],[Spa2],[Spa3],[Spa3e],[Type],[Link],[DownLoadType], r.RaftLink from[Hydra].[Station] st JOIN[Hydra].[River] r ON st.Id_River = r.Id
                        WHERE st.Id = @stationId", new {stationId});
                sqlConnection.Close();
                return station;
            }
        }

        public IEnumerable<Sample> GetSamples(int spot, DateTime startDate, DateTime stopDate)
        {
            var spotString = spot.ToString("000");
            var p = new DynamicParameters();
            p.Add("startDate", startDate, DbType.DateTime);
            p.Add("stopDate", stopDate, DbType.DateTime);

            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var samples = sqlConnection.Query<Sample>(
                    $@"SELECT [TimeStamp] ,[Level] ,[Flow] ,[Temperature]
                        FROM [Hydra].[Sample-{spotString}]
                        WHERE [TimeStamp] >= @startDate 
                        AND [TimeStamp] <= @stopDate 
                        ORDER BY [TimeStamp]", p);
                sqlConnection.Close();
                return samples;
            }
        }

        public int AddSample(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp)
        {
            var spotString = stationId.ToString("000");
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                var command = sqlConnection.CreateCommand();
                command.CommandText= 
                    $@"IF NOT EXISTS (SELECT * FROM [Hydra].[Sample-{spotString}] WHERE [TimeStamp] = @sampleTimeStamp)
                            INSERT INTO [Hydra].[Sample-{spotString}]([TimeStamp], [Level], [Flow], [Temperature]) 
                            VALUES(@sampleTimeStamp, @sampleLevel, @sampleFlow, @sampleTemperature)";

                command.Parameters.AddWithValue("sampleTimeStamp", sampleTimeStamp);
                command.Parameters.AddWithValue("sampleLevel", sampleLevel ?? SqlSingle.Null);
                command.Parameters.AddWithValue("sampleFlow", sampleFlow ?? SqlSingle.Null);
                command.Parameters.AddWithValue("sampleTemperature", sampleTemperature ?? SqlSingle.Null);

                sqlConnection.Open();

                var result = command.ExecuteNonQuery();

                sqlConnection.Close();

                return result;
            }
        }
    }
}
