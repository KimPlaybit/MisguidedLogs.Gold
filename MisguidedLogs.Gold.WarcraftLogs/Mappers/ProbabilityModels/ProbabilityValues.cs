using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Linq;
using System.Text.Json.Nodes;
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

    public async Task UploadResults(BunnyCdnServiceStorageUploader bunnyCdnStorageUploader, BunnyCdnClientStorageUploader bunnyCdnClientStorageUploader)
    {
        await bunnyCdnStorageUploader.Upload(this, $"misguided-logs-warcraftlogs/zones/{Zone}.json.gz", CancellationToken.None);
        await bunnyCdnStorageUploader.Upload(GetDto, $"misguided-logs-warcraftlogs/zones/{Zone}_stripped.json.gz", CancellationToken.None);
        await bunnyCdnClientStorageUploader.Upload(GetDto, $"misguidedlogs-client-info/zones/{Zone}_stripped.json.gz", CancellationToken.None);
        //await bunnyCdnClientStorageUploader.Upload(rarest, $"misguidedlogs-client-info/zones/{Zone}_rarestplays.json.gz", CancellationToken.None);
    }


    //private record RarestPick(Class Class, TalentSpec Spec, float Probability, string PlayerId, string Name, int Guid, int BossId, string Code, Role Role)
    //{
    //    public string Name { get; set; } = "";
    //    public int Guid { get; set; }
    //}
    [JsonIgnore]
    private ProbabilityValuesDto GetDto => new(Zone, [.. Bosses.Select(x => x.GetDto)]);
}
