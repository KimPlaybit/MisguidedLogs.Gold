using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using System.Text.Json.Serialization;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record ProbabilityValues(int Zone, List<BossProbability> Bosses)
{
    public static async Task<ProbabilityValues> GetProbabilityValues(BunnyCdnStorageLoader loader, int zone)
    {
        var storageObjects = await loader.GetListOfStorageObjects("misguided-logs-warcraftlogs/zones/");
        var strObject = storageObjects.FirstOrDefault(x => x.ObjectName.Contains(zone.ToString()));

        if (strObject != null)
        {
            return await loader.GetStorageObject<ProbabilityValues>($"misguided-logs-warcraftlogs/zones/{zone}.json.gz")
                   ?? throw new ArgumentNullException();
        }

        var result = new ProbabilityValues(zone, []);
        return result;
    }

    public async Task UploadResults(BunnyCdnClientStorageUploader bunnyCdnStorageUploader, BunnyCdnClientStorageUploader bunnyCdnClientStorageUploader)
    {
        await bunnyCdnStorageUploader.Upload(this, $"misguided-logs-warcraftlogs/zones/{Zone}.json.gz", CancellationToken.None);
        await bunnyCdnStorageUploader.Upload(GetDto, $"misguided-logs-warcraftlogs/zones/{Zone}_stripped.json.gz", CancellationToken.None);
        await bunnyCdnClientStorageUploader.Upload(GetDto, $"misguided-logs-warcraftlogs/zones/{Zone}_stripped.json.gz", CancellationToken.None);
    }

    [JsonIgnore]
    private ProbabilityValuesDto GetDto => new(Zone, [.. Bosses.Select(x => x.GetDto)]);
}
