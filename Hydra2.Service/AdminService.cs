using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using Hydra2.Service.Data;
using Hydra2.Service.Data.Admin;

namespace Hydra2.Service
{
    public class AdminService
    {
        private readonly string _connectionString;

        public AdminService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int GetSamplesCount()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                var command = sqlConnection.CreateCommand();
                command.CommandText =
                    @"SELECT SUM([Rows]) FROM
	                    ( SELECT sysindexes.Rows as [Rows]
                            FROM
                                sysobjects
                                INNER JOIN sysindexes
                                ON sysobjects.id = sysindexes.id
                            WHERE
                                type = 'U'
                                AND sysindexes.IndId < 2
	                            AND sysobjects.Name LIKE 'Sample%') a";

                sqlConnection.Open();

                var result = (int) command.ExecuteScalar();

                sqlConnection.Close();

                return result;
            }
        }

        public IEnumerable<SpotOverviewModel> GetSpotOverview()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var result = sqlConnection.Query<SpotOverviewModel>(
                    @"SELECT st.[Id], r.[Name],st.[Spot], st.[Type], st.[Link] FROM [Hydra].[Station] st JOIN [Hydra].[River] r ON st.[Id_River]=r.[Id]
                        ORDER BY r.[Name], st.[Spot]");
                sqlConnection.Close();
                return result;
            }
        }

        public IEnumerable<SpotOverviewModel> GetSpotOverviewWitSamples()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var result = sqlConnection.Query<SpotOverviewModel>(
                    @"SELECT st.[Id],r.[Name],st.[Spot], st.[Type], st.[Link] FROM [Hydra].[Station] st JOIN [Hydra].[River] r ON st.[Id_River]=r.[Id]
                        ORDER BY r.[Name], st.[Spot]");

                foreach (var spot in result)
                {
                    var spotString = spot.Id.ToString("000");
                    spot.LastSample = sqlConnection.QueryFirstOrDefault<DateTime>($"SELECT TOP 1 [TimeStamp] FROM [Hydra].[Sample-{spotString}] ORDER BY [TimeStamp] DESC");
                }

                sqlConnection.Close();
                return result;
            }
        }

        public DateTime GetLastSample(int spotId)
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                var spotString = spotId.ToString("000");
                var result = sqlConnection.QueryFirstOrDefault<DateTime>(
                    $@"SELECT TOP 1 [TimeStamp] FROM [Hydra].[Sample-{spotString}] ORDER BY [TimeStamp] DESC");

                sqlConnection.Close();
                return result;
            }
        }
    }
}
