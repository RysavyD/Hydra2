using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace SampleTableSeparator
{
    class Program
    {
        private const string ConnectionString =
            @"data source=LAPTOP-SCPOKV5J\SQL2014;initial catalog=Hydra2;integrated security=True;MultipleActiveResultSets=True;";

        private static List<Tuple<DateTime, int>> Duplicities = new List<Tuple<DateTime, int>>();
        private static List<int> IdsToRemove = new List<int>();
        private static List<int> StationIds = new List<int>();

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            TestConnection();

            FindDuplicities();

            LoadDuplicitiesIds();

            RemoveDuplicities();

            LoadStation();

            CreateStationSampleTables();

            FillStationSampleTables();

            RemoveOldSampleTable();

            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        private static void TestConnection()
        {
            WriteMethodName();
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT @@VERSION";

                var res = command.ExecuteScalar();
                Console.WriteLine($"Version: {res}");

                connection.Close();
            }
        }

        private static void FindDuplicities()
        {
            WriteMethodName();
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT [TimeStamp],[Id_Station] FROM [Hydra].[Sample] GROUP BY[TimeStamp], [Id_Station] HAVING COUNT(*) > 1";

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var date = reader.GetDateTime(0);
                    var id = reader.GetInt32(1);

                    Duplicities.Add(new Tuple<DateTime, int>(date, id));
                }

                connection.Close();
            }

            Console.WriteLine($"Duplicities found: {Duplicities.Count}");
        }

        private static void LoadDuplicitiesIds()
        {
            WriteMethodName();
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var (dateTime, id) in Duplicities)
                {
                    var command = connection.CreateCommand();
                    command.CommandText =
                        "SELECT [Id] FROM [Hydra].[Sample] WHERE [TimeStamp] = @Date AND [Id_Station]=@Id";
                    command.Parameters.Add(new SqlParameter("Date", dateTime));
                    command.Parameters.Add(new SqlParameter("Id", id));

                    var reader = command.ExecuteReader();
                    reader.Read();
                    while (reader.Read())
                    {
                        IdsToRemove.Add(reader.GetInt32(0));
                    }
                }

                connection.Close();
            }

            Console.WriteLine($"Ids to remove found: {IdsToRemove.Count}");
        }

        private static void RemoveDuplicities()
        {
            WriteMethodName();

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var id in IdsToRemove)
                {
                    var command = connection.CreateCommand();
                    command.CommandText =
                        "DELETE FROM [Hydra].[Sample] WHERE [Id] = @id";
                    command.Parameters.Add(new SqlParameter("Id", id));

                    command.ExecuteScalar();
                }

                connection.Close();
            }

            Console.WriteLine($"Ids removed: {IdsToRemove.Count}");
        }

        private static void LoadStation()
        {
            WriteMethodName();

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT [Id],[Spot] FROM [Hydra2].[Hydra].[Station]";
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    StationIds.Add(reader.GetInt32(0));
                }

                connection.Close();
            }

            Console.WriteLine($"Station founded: {StationIds.Count}");
        }

        private static void CreateStationSampleTables()
        {
            WriteMethodName();

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var stationId in StationIds)
                {
                    var idString = stationId.ToString("000");

                    var command = connection.CreateCommand();
                    command.CommandText =
                        @$"CREATE TABLE [Hydra].[Sample-{idString}]([TimeStamp][datetime] NOT NULL, [Level] [real] NULL,[Flow] [real] NULL,[Temperature] [real] NULL, PRIMARY KEY ([TimeStamp]))";

                    command.ExecuteScalar();
                }

                connection.Close();
            }

            Console.WriteLine($"Tables created: {StationIds.Count}");
        }

        private static void FillStationSampleTables()
        {
            WriteMethodName();

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                foreach (var stationId in StationIds)
                {
                    var idString = stationId.ToString("000");

                    var command = connection.CreateCommand();
                    command.CommandText =
                        @$"INSERT INTO [Hydra].[Sample-{idString}] 
                            SELECT [TimeStamp], [Level], [Flow], [Temperature]
                            FROM [Hydra].[Sample]
                            WHERE Id_Station=@id";
                    command.Parameters.Add(new SqlParameter("id", stationId));

                    command.ExecuteScalar();
                }

                connection.Close();
            }

            Console.WriteLine($"Tables filled: {StationIds.Count}");
        }

        private static void RemoveOldSampleTable()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = $"DROP TABLE [Hydra].[Sample]";

                command.ExecuteScalar();

                connection.Close();
            }

            Console.WriteLine($"Sample table deleted.");

            WriteMethodName();
        }

        private static void WriteMethodName([System.Runtime.CompilerServices.CallerMemberName]string name ="")
        {
            Console.WriteLine();
            Console.WriteLine(name);
        }
    }
}
