using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers.Achivements;
using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers;

public class AchivementsMapper(AllAchivements allAchivements, BunnyCdnStorageLoader loader, BunnyCdnServiceStorageUploader uploader, BunnyCdnClientStorageUploader clientStorageUploader)
{
    public async Task Mapper(IReadOnlyDictionary<(string Id, string FightId), PlayedCombinations> combinations, HashSet<Fight> fights, HashSet<Player> players, Dictionary<string, PlayerSearchIndex> playersInfo, CancellationToken cancellationToken)
    {
        var achivements = await loader.TryGetStorageObject<AchivementValues>("misguided-logs-warcraftlogs/achivements/achivementvalues.json.gz") ?? CreateAchivementValues();
        var mostRecentAchivements = await loader.TryGetStorageObject<HashSet<AchivedAchivementDto>>("misguided-logs-warcraftlogs/achivements/achivementrecent.json.gz") ?? [];
        var worldFirstAchivements = await loader.TryGetStorageObject<HashSet<AchivedAchivementDto>>("misguided-logs-warcraftlogs/achivements/achivementworldfirst.json.gz") ?? [];

        foreach (var fight in fights)
        {
            var combinationsInvolved = combinations.Where(x => x.Key.FightId == fight.FightId);
            var tanksInfight = players.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Tank)).ToList();
            var dpsInfight = players.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Dps)).ToList();
            var healerInfight = players.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Healer)).ToList();
            var hybridInfight = players.Where(y => combinationsInvolved.Any(x => x.Key.Id == y.PlayerId && x.Value.Role is Role.Hybrid)).ToList();

            var report = new ReportWithPlayersRoles(tanksInfight, dpsInfight, healerInfight, hybridInfight);

            if (report.Players.Count == 0)
            {
                continue;
            }

            var fulfilledAchivements = allAchivements.GetFulfilledAchivements(report);

            foreach (var fulfilled in fulfilledAchivements)
            {
                var newAchivement = new AchivedAchivementDto(fulfilled, fight.BossId, fight.ReportCode, fight.EndTime);
                var achivement = achivements.Achivements.FirstOrDefault(x => x.Boss == fight.BossId && fulfilled == x.Name) ??
                    new Achivement(fulfilled, fight.BossId, [.. players.Select(x => x.PlayerId)], [.. players.Select(x => x.PlayerId)]);
                if (!achivements.Achivements.Contains(achivement))
                {
                    achivements.Achivements.Add(achivement);
                }
                else
                {
                    foreach (var id in players.Select(x => x.PlayerId))
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

                var worldFirst = worldFirstAchivements.FirstOrDefault(x => x.Name == fulfilled && x.Boss == fight.BossId);
                if (worldFirst is null || worldFirst.AchivedAt > fight.EndTime)
                {
                    worldFirstAchivements.Add(newAchivement);
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
        var dto = achivements.GetDto();
        await uploader.Upload(achivements, "misguided-logs-warcraftlogs/achivements/achivementvalues.json.gz", cancellationToken);
        await uploader.Upload(mostRecentAchivements.OrderByDescending(x => x.AchivedAt).Take(10), "misguided-logs-warcraftlogs/achivements/achivementrecent.json.gz", cancellationToken);
        await clientStorageUploader.Upload(dto, "misguidedlogs-client-info/achivements/achivementrecent.json.gz", cancellationToken);
        await clientStorageUploader.Upload(dto.Achivements.OrderBy(x => x.Probability).Take(10), "misguidedlogs-client-info/achivements/achivementvalues-top-10.json.gz", cancellationToken);
        await clientStorageUploader.Upload(mostRecentAchivements.OrderByDescending(x => x.AchivedAt).Take(10), "misguidedlogs-client-info/achivements/achivementrecent.json.gz", cancellationToken);
    }

    private static AchivementValues CreateAchivementValues()
    {
        return new AchivementValues([]);
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