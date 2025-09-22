using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers.Achivements;
using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Collections.Concurrent;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers;

public class AchivementsMapper(AllAchivements allAchivements, BunnyCdnStorageLoader loader, BunnyCdnServiceStorageUploader uploader, BunnyCdnClientStorageUploader clientStorageUploader)
{
    private Player GetPlayer (IEnumerable<Player> players)
    {
        return players.FirstOrDefault(x => x.Guid is not 0) ?? players.First();
    }

    public async Task Mapper(IReadOnlyDictionary<(string Id, string FightId), PlayedCombinations> combinations, HashSet<Fight> fights, HashSet<Player> players, Dictionary<string, PlayerSearchIndex> playersInfo, CancellationToken cancellationToken)
    {
        var achivements = await loader.TryGetStorageObject<AchivementValues>("misguided-logs-warcraftlogs/achivements/achivementvalues.json.gz") ?? CreateAchivementValues();
        var mostRecentAchivements = await loader.TryGetStorageObject<HashSet<AchivedAchivementDto>>("misguided-logs-warcraftlogs/achivements/achivementrecent.json.gz") ?? [];
        var worldFirstAchivements = await loader.TryGetStorageObject<HashSet<AchivedAchivementDto>>("misguided-logs-warcraftlogs/achivements/achivementworldfirst.json.gz") ?? [];
        var playersDic =  players.GroupBy(x => x.PlayerId).ToDictionary(x => x.Key, GetPlayer);

        await Task.WhenAll(fights.Select(f => AddAchivements(f, combinations, fights, new(playersDic), playersInfo, achivements, worldFirstAchivements, mostRecentAchivements, cancellationToken)));

        var dto = achivements.GetDto();
        await uploader.Upload(achivements, "misguided-logs-warcraftlogs/achivements/achivementvalues.json.gz", cancellationToken);
        await uploader.Upload(mostRecentAchivements.OrderByDescending(x => x.AchivedAt).Take(10), "misguided-logs-warcraftlogs/achivements/achivementrecent.json.gz", cancellationToken);
        await clientStorageUploader.Upload(dto, "misguidedlogs-client-info/achivements/archivementvalues-stripped.json.gz", cancellationToken);
        await clientStorageUploader.Upload(dto.Achivements.OrderBy(x => x.Probability).Take(10), "misguidedlogs-client-info/achivements/achivementvalues-top-10.json.gz", cancellationToken);
        await clientStorageUploader.Upload(mostRecentAchivements.OrderByDescending(x => x.AchivedAt).Take(10), "misguidedlogs-client-info/achivements/achivementrecent.json.gz", cancellationToken);
    }

    private static AchivementValues CreateAchivementValues()
    {
        return new AchivementValues([]);
    }

    private async Task AddAchivements(Fight fight, IReadOnlyDictionary<(string Id, string FightId), PlayedCombinations> combinations, HashSet<Fight> fights, ConcurrentDictionary<string, Player> players, Dictionary<string, PlayerSearchIndex> playersInfo, AchivementValues achivements, HashSet<AchivedAchivementDto> worldFirstAchivements, HashSet<AchivedAchivementDto> mostRecentAchivements, CancellationToken cancellationToken)
    {
        var combinationsInvolved = combinations.Where(x => x.Key.FightId == fight.FightId);
        var playersInvolved = combinationsInvolved.Select(x =>
        {
            if (players.TryGetValue(x.Key.Id, out var player))
            {
                return player;
            }
            else
            {
                return null;
            }
        });
        var tanksInfight = playersInvolved.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Tank)).ToHashSet();
        var dpsInfight = playersInvolved.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Dps)).ToHashSet();
        var healerInfight = playersInvolved.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Healer)).ToHashSet();
        var hybridInfight = playersInvolved.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Hybrid)).ToHashSet();

        var report = new ReportWithPlayersRoles(tanksInfight.ToList(), dpsInfight.ToList(), healerInfight.ToList(), hybridInfight.ToList());

        if (report.Players.Count == 0)
        {
            return;
        }

        var fulfilledAchivements = allAchivements.GetFulfilledAchivements(report);

        foreach (var fulfilled in fulfilledAchivements)
        {
            var newAchivement = new AchivedAchivementDto(fulfilled, fight.BossId, fight.ReportCode, fight.EndTime);
            var achivement = achivements.Achivements.FirstOrDefault(x => x.Boss == fight.BossId && fulfilled == x.Name) ??
                new Achivement(fulfilled, fight.BossId, [.. playersInvolved.Select(x => x.PlayerId)], [.. playersInvolved.Select(x => x.PlayerId)]);
            if (!achivements.Achivements.Contains(achivement))
            {
                achivements.Achivements.Add(achivement);
            }
            else
            {
                foreach (var id in playersInvolved.Select(x => x.PlayerId))
                {
                    achivement.AllPlayersCapable.Add(id);
                    achivement.PlayersFulfilled.Add(id);
                }
            }
            foreach (var player in report.Players)
            {
                if (playersInfo.TryGetValue(player.PlayerId, out var playerIndex) && !playerIndex.AchivedAchivements.Any(x => x.Name != fulfilled))
                {
                    playerIndex.AchivedAchivements.Add(newAchivement);
                }

            }
            lock (worldFirstAchivements)
            {
                var worldFirst = worldFirstAchivements.FirstOrDefault(x => x.Name == fulfilled && x.Boss == fight.BossId);
                if (worldFirst is null || worldFirst.AchivedAt > fight.EndTime)
                {
                    worldFirstAchivements.Add(newAchivement);
                }
            }


            mostRecentAchivements.Add(newAchivement);
            
        }

        foreach (var player in report.Players)
        {
            var specificAchivements = AllAchivements.ClassToAchivement.Where(x => x._Class == player.Class).ToList();

            foreach (var specific in specificAchivements)
            {
                var achivement = achivements.Achivements.FirstOrDefault(x => x.Boss == fight.BossId && x.Name == specific.Achivement) ??
                   new Achivement(specific.Achivement, fight.BossId, [], []);

                achivement.AllPlayersCapable.Add(player.PlayerId);
                if (!achivements.Achivements.Contains(achivement))
                {
                    achivements.Achivements.Add(achivement);
                }
            }

            var noTauntAchive = achivements.Achivements.FirstOrDefault(x => x.Name == Achivements.Achivement.HeyNoTaunt) ?? new Achivement(Achivements.Achivement.HeyNoTaunt, fight.BossId, [], []);

            noTauntAchive.AllPlayersCapable.Add(player.PlayerId);
            if (!achivements.Achivements.Contains(noTauntAchive))
            {
                achivements.Achivements.Add(noTauntAchive);
            }
        }
    }

}

public record AchivementValues(List<Achivement> Achivements)
{
    public AchivementValuesDto GetDto()
    {
        return new AchivementValuesDto([.. Achivements.Select(x => x.GetDto())]);
    }
}
public record Achivement(Achivements.Achivement Name, int Boss, HashSet<string> PlayersFulfilled, HashSet<string> AllPlayersCapable)
{
    public AchivementDto GetDto()
    {
        return new AchivementDto(Name, Boss, AllPlayersCapable.Count == 0 ? PlayersFulfilled.Count / AllPlayersCapable.Count : 0);
    }
}

public record AchivementValuesDto(AchivementDto[] Achivements);
public record AchivementDto(Achivements.Achivement Name, int Boss, float Probability);

public record AchivedAchivementDto(Achivements.Achivement Name, int Boss, string ReportCode, DateTime AchivedAt);