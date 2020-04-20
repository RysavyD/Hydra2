using System;
using System.Collections.Generic;
using Hydra2.Service.Data;

namespace Hydra2.Service
{
    public interface IDataService
    {
        IEnumerable<River> GetRivers();
        IEnumerable<Station> GetStations(int riverId);
        Station GetStation(int stationId);
        IEnumerable<Sample> GetSamples(int spot, DateTime startDate, DateTime stopDate);
        int AddSample(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp);
    }
}