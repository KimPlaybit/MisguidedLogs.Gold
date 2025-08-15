using BunnyCDN.Net.Storage.Models;
using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers;
using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Globalization;

namespace MisguidedLogs.Gold.WarcraftLogs;

public class Runner(BunnyCdnStorageLoader loader, Probability probability, SearchIndexPlayers playerMapper, ILogger<Runner> log) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            log.LogInformation("Retrieving storageObjects");
            var storageObjects = await loader.GetListOfStorageObjects("misguided-logs-warcraftlogs/silver/");
            var latestFullpath = Newest([.. storageObjects]).FullPath;

            var zones = await loader.GetStorageObject<HashSet<Zone>>(Path.Combine(latestFullpath, "zones.json.gz"));
            var players = await loader.GetStorageObject<HashSet<Player>>(Path.Combine(latestFullpath, "players.json.gz"));
            var fights = await loader.GetStorageObject<HashSet<Fight>>(Path.Combine(latestFullpath, "fights.json.gz"));
            var bosses = await loader.GetStorageObject<HashSet<Boss>>(Path.Combine(latestFullpath, "bosses.json.gz"));
            var stats = await loader.GetStorageObject<HashSet<PlayerStats>>(Path.Combine(latestFullpath, "stats.json.gz"));

            ArgumentNullException.ThrowIfNull(zones);
            ArgumentNullException.ThrowIfNull(players);
            ArgumentNullException.ThrowIfNull(fights);
            ArgumentNullException.ThrowIfNull(bosses);
            ArgumentNullException.ThrowIfNull(stats);

            log.LogInformation("Calculating new Probability Info");
            await probability.CalculateProbability(zones, bosses, players, fights, stats, cancellationToken);

            log.LogInformation("Creating Player Search Info");
            await playerMapper.CreatePlayerSearchInfo(players, fights, stats, probability.Combinations, cancellationToken);
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

        return storageObjects.Where(x => !x.ObjectName.Contains("details")).OrderBy(x => DateTime.ParseExact(x.ObjectName.Split("__")[0], "yyyy-MM-dd_HH-MM", CultureInfo.InvariantCulture)).Last();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
