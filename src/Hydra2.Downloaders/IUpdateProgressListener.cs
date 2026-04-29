namespace Hydra2.Downloaders;

public interface IUpdateProgressListener
{
    void OnIterationStarted(int stationId);
    void OnIterationCompleted(int stationId);
}

public class NullUpdateProgressListener : IUpdateProgressListener
{
    public void OnIterationStarted(int stationId) { }
    public void OnIterationCompleted(int stationId) { }
}
