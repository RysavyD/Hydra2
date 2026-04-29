using Hydra2.Service.Data;

namespace Hydra2.Service;

public class FakeDataService : IDataService
{
    private readonly List<River> _rivers = new()
    {
        new River { Id = 1, Name = "Berounka" },
        new River { Id = 2, Name = "Vltava" },
        new River { Id = 3, Name = "Sázava" },
        new River { Id = 4, Name = "Klabava" },
    };

    private readonly List<Station> _stations = new()
    {
        new Station { Id = 1, Id_River = 1, Spot = "Bílá hora" },
        new Station { Id = 2, Id_River = 1, Spot = "Srbsko" },
        new Station { Id = 3, Id_River = 2, Spot = "Soumarský most" },
        new Station { Id = 4, Id_River = 2, Spot = "Vyšší Brod" },
        new Station { Id = 5, Id_River = 2, Spot = "Praha" },
        new Station { Id = 6, Id_River = 3, Spot = "Havlíčkův Brod" },
        new Station { Id = 7, Id_River = 3, Spot = "Nespeky" },
        new Station { Id = 8, Id_River = 4, Spot = "Hrádek" },
    };

    public Task<IEnumerable<River>> GetRiversAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<River>>(_rivers);

    public Task<IEnumerable<Station>> GetStationsAsync(int riverId, CancellationToken cancellationToken = default)
        => Task.FromResult(_stations.Where(s => s.Id_River == riverId));

    public Task<Station?> GetStationAsync(int stationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_stations.FirstOrDefault(s => s.Id == stationId));

    public Task<IEnumerable<Sample>> GetSamplesAsync(int spot, DateTime startDate, DateTime stopDate, CancellationToken cancellationToken = default)
    {
        var hours = (int)(stopDate - startDate).TotalHours;
        var samples = new Sample[Math.Max(hours, 0)];
        var random = new Random(spot);

        var lastLevel = 100f;
        var lastFlow = 8.5f;
        var lastTemperature = 3.5f;

        for (var i = 0; i < samples.Length; i++)
        {
            lastLevel = (float)(lastLevel + lastLevel * ((random.NextDouble() - 0.5) / 5));
            lastFlow = (float)(lastFlow + lastFlow * ((random.NextDouble() - 0.5) / 5));
            lastTemperature = (float)(lastTemperature + lastTemperature * ((random.NextDouble() - 0.5) / 5));

            samples[i] = new Sample
            {
                TimeStamp = startDate.AddHours(i),
                Level = lastLevel,
                Flow = lastFlow,
                Temperature = lastTemperature,
            };
        }

        return Task.FromResult<IEnumerable<Sample>>(samples);
    }

    public Task<int> AddSampleAsync(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
