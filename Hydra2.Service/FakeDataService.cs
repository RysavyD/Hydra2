using System;
using System.Collections.Generic;
using System.Linq;
using Hydra2.Service.Data;

namespace Hydra2.Service
{
    public class FakeDataService : IDataService
    {
        readonly List<River> rivers = new List<River>()
        {
            new River {Id = 1, Name = "Berounka"},
            new River {Id = 2, Name = "Vltava"},
            new River {Id = 3, Name = "Sázava"},
            new River {Id = 4, Name = "Klabava"},
        };

        List<Station> stations = new List<Station>()
        {
            new Station() {Id = 1, Id_River = 1, Spot = "Bílá hora"},
            new Station() {Id = 2, Id_River = 1, Spot = "Srbsko"},
            new Station() {Id = 3, Id_River = 2, Spot = "Soumarský most"},
            new Station() {Id = 4, Id_River = 2, Spot = "Vyšší Brod"},
            new Station() {Id = 5, Id_River = 2, Spot = "Praha"},
            new Station() {Id = 6, Id_River = 3, Spot = "Havlíčkův Brod"},
            new Station() {Id = 7, Id_River = 3, Spot = "Nespeky"},
            new Station() {Id = 8, Id_River = 4, Spot = "Hrádek"},
        };

        public IEnumerable<River> GetRivers()
        {
            return rivers;
        }

        public IEnumerable<Station> GetStations(int riverId)
        {
            return stations.Where(x => x.Id_River == riverId);
        }

        public Station GetStation(int stationId)
        {
            return stations.First(x => x.Id == stationId);
        }

        public IEnumerable<Sample> GetSamples(int spot, DateTime startDate, DateTime stopDate)
        {
            var hours = (int) (stopDate - startDate).TotalHours;
            var result = new Sample[hours];
            var random1 = new Random();
            var random2 = new Random(random1.Next(0, 25000));
            var random3 = new Random(random1.Next(0, 55000));

            var lastLevel = 100f;
            var lastFlow = 8.5f;
            var lastTemperature = 3.5f;

            for (int i = 0; i < hours; i++)
            {
                var rand1 = random1.NextDouble();
                var rand2 = random2.NextDouble();
                var rand3 = random3.NextDouble();
                var nextLevel = Convert.ToSingle(lastLevel + lastLevel * ((rand1 - 0.5) / 5));
                var nextFlow = Convert.ToSingle(lastFlow + lastFlow * ((rand2 - 0.5) / 5));
                var nextTemperature = Convert.ToSingle(lastTemperature + lastTemperature * ((rand3 - 0.5) / 5));
                result[i] = new Sample
                {
                    TimeStamp = startDate.AddHours(i),
                    Level = nextLevel,
                    Flow = nextFlow,
                    Temperature = nextTemperature
                };

                lastLevel = nextLevel;
                lastFlow = nextFlow;
                lastTemperature = nextTemperature;
            }

            return result;
        }

        public int AddSample(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp)
        {
            throw new NotImplementedException();
        }
    }
}
