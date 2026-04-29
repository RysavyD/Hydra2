namespace Hydra2.Downloaders;

public interface IDownloaderFactory
{
    ISpotInformationDownloader? GetDownloader(int downloadType);
}
