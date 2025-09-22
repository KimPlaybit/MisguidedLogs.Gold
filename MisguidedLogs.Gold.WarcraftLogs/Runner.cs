using BunnyCDN.Net.Storage.Models;
using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers;
using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Globalization;

namespace MisguidedLogs.Gold.WarcraftLogs;

public class Runner(BunnyCdnStorageLoader loader, BunnyCdnServiceStorageUploader uploader, Probability probability, SearchIndexPlayers playerMapper, ClientConfig config, ILogger<Runner> log) : IHostedService
{
    private record LastRun(string Name);
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var zonesFolders = await loader.GetListOfStorageObjects(Path.Combine("misguided-logs-warcraftlogs/silver"));

            foreach (var zoneFold in zonesFolders)
            {
                var zone = zoneFold.ObjectName;
                log.LogInformation("Checking last file ran");
                var last = await loader.GetStorageObject<LastRun>(Path.Combine($"misguided-logs-warcraftlogs/goldrunner/{zone}", "lastrun.json.gz"));

                log.LogInformation("Retrieving storageObjects");
                var storageObjects = await loader.GetListOfStorageObjects($"misguided-logs-warcraftlogs/silver/{zone}");
                var newest = Newest(storageObjects);
                if (!storageObjects.Any() || last is not null && newest.ObjectName == last.Name)
                {
                    log.LogInformation("No new file to process for {Zone}, shutting down", zone);
                    continue;
                }

                var zones = await loader.GetStorageObject<HashSet<Zone>>(Path.Combine(newest.FullPath, "zones.json.gz"));
                var players = await loader.GetStorageObject<HashSet<Player>>(Path.Combine(newest.FullPath, "players.json.gz"));
                var fights = await loader.GetStorageObject<HashSet<Fight>>(Path.Combine(newest.FullPath, "fights.json.gz"));
                var bosses = await loader.GetStorageObject<HashSet<Boss>>(Path.Combine(newest.FullPath, "bosses.json.gz"));
                var stats = await loader.GetStorageObject<HashSet<PlayerStats>>(Path.Combine(newest.FullPath, "stats.json.gz"));

                ArgumentNullException.ThrowIfNull(zones);
                ArgumentNullException.ThrowIfNull(players);
                ArgumentNullException.ThrowIfNull(fights);
                ArgumentNullException.ThrowIfNull(bosses);
                ArgumentNullException.ThrowIfNull(stats);

                log.LogInformation("Calculating new Probability Info");
                await probability.CalculateProbability(zones, bosses, players, fights, stats, cancellationToken);

                log.LogInformation("Creating Player Search Info");
                await playerMapper.CreatePlayerSearchInfo(players, fights, stats, probability.Combinations, cancellationToken);

                await uploader.Upload(new LastRun(newest.ObjectName), Path.Combine($"misguided-logs-warcraftlogs/goldrunner/{zone}", "lastrun.json.gz"), cancellationToken);
            }
            
        }
        catch (Exception e)
        {

            log.LogInformation("Something Went Wrong, Shutting down, Error {Exception}", e);
            Environment.Exit(1);
        }
        log.LogInformation("Finishing Job");
        Environment.Exit(0);
    }

    private static StorageObject Newest(StorageObject[] storageObjects)
    {
        if (storageObjects.Length == 0)
        {   
            throw new ArgumentException("Empty Folder");
        }

        if (storageObjects.Length == 1)
        {
            return storageObjects[0];
        }

        return storageObjects.Where(x => !x.ObjectName.Contains("details")).OrderBy(x => DateTime.ParseExact(x.ObjectName.Split("__")[0], "yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture)).Last();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
