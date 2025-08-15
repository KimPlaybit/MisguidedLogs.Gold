using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers;

public class SearchIndexPlayers(BunnyCdnStorageLoader loader, BunnyCdnServiceStorageUploader uploader, AchivementsMapper achivementsMapper, ILogger<SearchIndexPlayers> log)
{
    public async Task CreatePlayerSearchInfo(HashSet<Player> players, HashSet<Fight> fights, HashSet<PlayerStats> stats, IReadOnlyDictionary<(string Id, string FightId), PlayedCombinations> combinations, CancellationToken cancellationToken)
    {
        var allExistingPlayers = (await loader.TryGetStorageObject<HashSet<PlayerSearchIndex>>("misguided-logs-warcraftlogs/gold/players.json.gz"))?.ToDictionary(x => x.Id, x => x) ?? [];
        foreach (var player in players)
        {
            var playerConnectedStats= stats.Where(s => s.PlayerId == player.PlayerId).ToList();
            if (!allExistingPlayers.TryGetValue(player.PlayerId, out var existingPlayer))
            {
                existingPlayer = new PlayerSearchIndex(player.PlayerId, player.Guid, $"{player.Name}-{player.Server}-{player.Region}", player.Class, [], []);
                allExistingPlayers.Add(existingPlayer.Id, existingPlayer);
            }
            foreach (var playerConnectedStat in playerConnectedStats)
            {
                var bossId = fights.FirstOrDefault(x => x.FightId == playerConnectedStat.FightId)?.BossId;
                if (bossId is null)
                {
                    continue; // Skip if no boss found for the fight
                }
                if (combinations.TryGetValue((playerConnectedStat.PlayerId, playerConnectedStat.FightId), out var combination))
                {
                    existingPlayer.Combinations.Add(combination);
                }
            }
        }


        log.LogInformation("Adding Achivements");
        await achivementsMapper.Mapper(combinations, fights, players, allExistingPlayers, cancellationToken);

        await uploader.Upload(allExistingPlayers.Values.ToHashSet(), "misguided-logs-warcraftlogs/gold/players.json.gz", cancellationToken);
        await uploader.Upload(allExistingPlayers.Values.Select(x => x.GetDto).ToHashSet(), "misguided-logs-warcraftlogs/gold/searchValues.json.gz", cancellationToken);
    }
}

public record PlayerSearchIndex(string Id, long Guid, string Name, Class Class, HashSet<PlayedCombinations> Combinations, HashSet<AchivedAchivementDto> AchivedAchivements)
{
    public PlayerSearchIndexDto GetDto => new PlayerSearchIndexDto(Id, Name);
}
public record PlayedCombinations(short BossId, Role Role, TalentSpec Spec);
public record PlayerSearchIndexDto(string Id, string Name);
public enum Role
{
    Tank,
    Healer,
    Dps,
    Hybrid
}