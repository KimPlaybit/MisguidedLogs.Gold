using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;
using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers;

public class Probability(BunnyCdnStorageLoader loader, BunnyCdnServiceStorageUploader uploader, BunnyCdnClientStorageUploader clientStorageUploader, ILogger<Probability> log)
{
    private Dictionary<(string Id, string FightId), PlayedCombinations> combinations = new();
    public IReadOnlyDictionary<(string Id, string FightId), PlayedCombinations> Combinations => combinations;
    public async Task CalculateProbability(HashSet<Zone> zones, HashSet<Boss> bosses, HashSet<Player> players, HashSet<Fight> fights, HashSet<PlayerStats> stats, CancellationToken cancellationToken)
    {
        log.LogInformation("Retrieving Players From Latest Report");

        foreach (var zone in zones)
        {
            var assosiatedBosses = bosses.Where(x => x.ZoneId == zone.Id).ToList();
            var probability = await ProbabilityValues.GetProbabilityValues(loader, zone.Id);

            foreach (var assosiatedBoss in assosiatedBosses)
            {
                var bossProbability = probability.Bosses.FirstOrDefault(x => x.BossId == assosiatedBoss.Id);
                if (bossProbability is null)
                {
                    bossProbability = new BossProbability(assosiatedBoss.Id, 0, [], [], [], [])
                    {
                        AvgMeleeDmgTakenValue = 20,
                        AvgTotalDmgTakenByBossValue = 10,
                        AvgTotalDmgTakenValue = 10
                    };
                    probability.Bosses.Add(bossProbability);
                }

                var assosiatedFights = fights.Where(x => x.BossId == assosiatedBoss.Id).ToHashSet();
                var assiosiatedStats = stats.Where(x => assosiatedFights.Any(f => f.FightId == x.FightId && f.BossId == assosiatedBoss.Id)).ToHashSet();
                var assosiatedPlayers = players.Where(x => assiosiatedStats.Any(s => s.PlayerId == x.PlayerId)).ToHashSet();

                if (assosiatedPlayers.Count is 0)
                {
                    continue;
                }

                var tanks = assiosiatedStats.Where(x => IsTank(x, assiosiatedStats, bossProbability, assosiatedPlayers)).ToList();
                if (TryCalcAvgTank(tanks, assiosiatedStats, bossProbability, out var avgDmgTaken))
                {
                    bossProbability.AvgMeleeDmgTakenValue = avgDmgTaken!.AvgMeleeDmgTakenValue;
                    bossProbability.AvgTotalDmgTakenByBossValue = avgDmgTaken.AvgTotalDmgTakenByBossValue;
                    bossProbability.AvgTotalDmgTakenValue = avgDmgTaken.AvgTotalDmgTakenValue;
                }

                var damageDealers = assiosiatedStats.Where(x => IsDps(x, tanks)).ToList();
                if (TryCalcAvg(damageDealers.Count, damageDealers.Select(x => x.Dps).Average(), bossProbability.AvgLastDpsValue, bossProbability.Dps.Sum(x => x.AmountOfPlayers), out var avgDps))
                {
                    bossProbability.AvgLastDpsValue = avgDps;
                }

                var healers = assiosiatedStats.Where(x => IsHealer(x, damageDealers, tanks)).ToList();
                if (TryCalcAvg(healers.Count, healers.Select(x => x.Hps).Average(), bossProbability.AvgLastHpsValue, bossProbability.Hps.Sum(x => x.AmountOfPlayers), out var avgHps))
                {
                    bossProbability.AvgLastHpsValue = avgHps;
                }

                // Todo Tanks, Damage Taken ? how to determine how many tanks were used?
                DealWithEachStats(tanks, players, bossProbability, bossProbability.Tanks);
                AddRoles(tanks, assosiatedBoss.Id, Role.Tank);

                // Not a perfect way to determine what a hybrid is. 
                var hybridStats = assiosiatedStats.Where(x => IsHybrid(x, tanks, avgHps, avgDps)).ToList();
                DealWithEachStats(hybridStats, players, bossProbability, bossProbability.Hybrids);
                AddRoles(hybridStats, assosiatedBoss.Id, Role.Hybrid);

                // Todo Dps
                DealWithEachStats(damageDealers, players, bossProbability, bossProbability.Dps);
                AddRoles(damageDealers, assosiatedBoss.Id, Role.Dps);

                // Todo Hps
                DealWithEachStats(healers, players, bossProbability, bossProbability.Hps);
                AddRoles(healers, assosiatedBoss.Id, Role.Healer);

                foreach (var boss in probability.Bosses)
                {
                    UpdateProbability(boss.Tanks, boss.AmountOfPlayers);
                    UpdateProbability(boss.Dps, boss.AmountOfPlayers);
                    UpdateProbability(boss.Hps, boss.AmountOfPlayers);
                    UpdateProbability(boss.Hybrids, boss.AmountOfPlayers);
                }
            }
            await probability.UploadResults(uploader, clientStorageUploader);
        }
    }

    private void AddRoles(List<PlayerStats> stats, int bossId, Role role)//
    {
        stats.ForEach(x => AddPlayerCombination(role, x.Spec.Spec, bossId, x.PlayerId, x.FightId));
    }

    private void AddPlayerCombination(Role role, TalentSpec spec, int bossId, string playerId, string fightId)
    {
        try
        {
            combinations.Add((playerId, fightId), new PlayedCombinations(bossId, role, spec));

        }
        catch (Exception)
        {

            throw;
        }
    }

    private static bool TryCalcAvg(int amountNewPlayers, double newAvg, int lastAvg, int lastAmountOfPlayers, out int newAvgValue)
    {
        newAvgValue = 0;
        if (amountNewPlayers is not 0)
        {
            var lastAvgSum = lastAmountOfPlayers * lastAvg;
            var newAvgSum = newAvg * amountNewPlayers;
            newAvgValue = (int)(lastAvgSum + newAvgSum) / (amountNewPlayers + lastAmountOfPlayers);
            return true;
        }

        return false;
    }
    private static bool TryCalcAvgTank(List<PlayerStats> playerStats, HashSet<PlayerStats> allStats, BossProbability bossProbability, out TankAvgValues? newAvgValue)
    {
        if (playerStats.Count is not 0)
        {
            var avgMelee = playerStats.Select(x => (float)x.MeleeDmgTaken / GetMeleeDmgTaken(x, allStats)).Average() * 100;
            var avgDamageTaken = playerStats.Select(x => (float)x.TotalDamageTaken / GetTotalDmgTaken(x, allStats)).Average() * 100;
            var avgTotalByBoss = playerStats.Select(x => (float)x.TotalDamageTakenByBoss / GetTotalDmgTakenByBoss(x, allStats)).Average() * 100;
            if (bossProbability.Tanks.Sum(x => x.AmountOfPlayers) is 0)
            {
                newAvgValue = new TankAvgValues(
                    (short)avgMelee,
                    (short)avgTotalByBoss,
                    (short)avgDamageTaken
                    );
                return true;
            }
            var lastAvgDmgTakenSum = (float)bossProbability.AvgLastDmgTakenValue * bossProbability.Tanks.Sum(x => x.AmountOfPlayers);
            var lastAvgTotalByBoss = (float)bossProbability.AvgTotalDmgTakenByBossValue * bossProbability.Tanks.Sum(x => x.AmountOfPlayers);
            var lastAvgMelee = (float)bossProbability.AvgMeleeDmgTakenValue * bossProbability.Tanks.Sum(x => x.AmountOfPlayers);

            newAvgValue = new TankAvgValues(
                (short)((lastAvgMelee + avgMelee) / (playerStats.Count + bossProbability.Tanks.Sum(x => x.AmountOfPlayers))),
                (short)((lastAvgTotalByBoss + avgTotalByBoss) / (playerStats.Count + bossProbability.Tanks.Sum(x => x.AmountOfPlayers))),
                (short)((lastAvgDmgTakenSum + avgDamageTaken) / (playerStats.Count + bossProbability.Tanks.Sum(x => x.AmountOfPlayers)))
                );
            return true;
        }
        newAvgValue = null;
        return false;
    }

    private static long GetMeleeDmgTaken(PlayerStats stats, HashSet<PlayerStats> allStats)
    {
        var allAssiosiatedPlayers = allStats.Where(x => x.FightId == stats.FightId);
        return allAssiosiatedPlayers.Sum(x => x.MeleeDmgTaken);
    }
    private static long GetTotalDmgTaken(PlayerStats stats, HashSet<PlayerStats> allStats)
    {
        var allAssiosiatedPlayers = allStats.Where(x => x.FightId == stats.FightId);
        return allAssiosiatedPlayers.Sum(x => x.TotalDamageTaken);
    }
    private static long GetTotalDmgTakenByBoss(PlayerStats stats, HashSet<PlayerStats> allStats)
    {
        var allAssiosiatedPlayers = allStats.Where(x => x.FightId == stats.FightId);
        return allAssiosiatedPlayers.Sum(x => x.TotalDamageTakenByBoss);
    }

    private record TankAvgValues(short AvgMeleeDmgTakenValue, short AvgTotalDmgTakenByBossValue, short AvgTotalDmgTakenValue);

    private static bool IsDps(PlayerStats toEvaluate, List<PlayerStats> tankList)
    {
        if (tankList.Contains(toEvaluate))
        {
            return false;
        }
        return toEvaluate.Hps < 100 && toEvaluate.Dps > 100;
    }

    private static bool IsHybrid(PlayerStats stats, List<PlayerStats> tankList, float avgHeal, float avgDPS)
    {
        if (tankList.Contains(stats) || stats.Dps < 100 || stats.Hps < 100)
        {
            return false;
        }

        var percentageOfHeal = stats.Hps / avgHeal;
        var percentageOfDps = stats.Dps / avgDPS;
        var isHybrid = Math.Abs(percentageOfHeal - percentageOfDps);
        return isHybrid < 0.05f; // 5% difference
    }

    private static bool IsHealer(PlayerStats toEvaluate, List<PlayerStats> dpsList, List<PlayerStats> tankList)
    {
        if (tankList.Contains(toEvaluate))
        {
            return false;
        }

        if (dpsList.Contains(toEvaluate))
        {
            return false;
        }
        return toEvaluate.Dps < 100 && toEvaluate.Hps > 100;
    }

    private static bool IsTank(PlayerStats stats, HashSet<PlayerStats> allStats, BossProbability bossProbability, HashSet<Player> players)
    {
        if (stats.Spec.Spec is TalentSpec.Protection or TalentSpec.Feral or TalentSpec.Hybrid or TalentSpec.Fury)
        {
            if (players.FirstOrDefault(x => x.PlayerId == stats.PlayerId)?.Class is 
                Class.Mage or 
                Class.Warlock or
                Class.Rogue or
                Class.Shaman or
                Class.Priest or
                Class.Hunter or null)
            {
                return false;
            }
            var allAssiosiatedPlayers = allStats.Where(x => x.FightId == stats.FightId);
            var totalMeleeDmgTaken = allAssiosiatedPlayers.Sum(x => x.MeleeDmgTaken);
            var totalDmgTaken = allAssiosiatedPlayers.Sum(x => x.TotalDamageTaken);
            var totalDmgTakenByBoss = allAssiosiatedPlayers.Sum(x => x.TotalDamageTakenByBoss);
            if ((float)stats.MeleeDmgTaken / totalMeleeDmgTaken > (float)bossProbability.AvgMeleeDmgTakenValue / 100)
            {
                return true;
            }

            if ((float)stats.TotalDamageTakenByBoss / totalDmgTakenByBoss > (float)bossProbability.AvgTotalDmgTakenByBossValue / 100)
            {
                return true;
            }

            if ((float)stats.TotalDamageTaken / totalDmgTaken > (float)bossProbability.AvgTotalDmgTakenValue / 100)
            {
                return true;
            }
        }


        return false;
    }

    public void DealWithEachStats(List<PlayerStats> stats, HashSet<Player> players, BossProbability bossProbability, List<ClassProbability> classProbabilities)
    {
        foreach (var stat in stats)
        {
            var player = players.First(x => x.PlayerId == stat.PlayerId);
            var classProbability = classProbabilities.FirstOrDefault(x => x.Class == player.Class);
            if (classProbability is null)
            {
                classProbabilities.Add(new ClassProbability(player.Class, [new SpecProbability(stat.Spec.Spec, 0, [player.PlayerId])], 0));
                continue;
            }
            var spec = classProbability.Specs.FirstOrDefault(x => x.Spec == stat.Spec.Spec);
            if (spec is null)
            {
                classProbability.Specs.Add(new SpecProbability(stat.Spec.Spec, 0, [player.PlayerId]));
            }
            else if (!spec.Players.Contains(player.PlayerId))
            {
                spec.Players.Add(player.PlayerId);
                bossProbability.AmountOfPlayers++;
            }
        }
    }

    public void UpdateProbability(List<ClassProbability> classProbabilities, int totalPlayerAmount)
    {
        foreach (var classProbability in classProbabilities)
        {
            foreach (var specProbability in classProbability.Specs)
            {
                specProbability.Probability = specProbability.Players.Count / (float)classProbabilities.Sum(x => x.AmountOfPlayers);
                specProbability.TotalProbability = specProbability.Players.Count / (float)totalPlayerAmount;
            }
            classProbability.Probability = classProbability.AmountOfPlayers / (float)classProbabilities.Sum(x => x.AmountOfPlayers);
            classProbability.TotalProbability = classProbability.AmountOfPlayers / (float)totalPlayerAmount;
        }
    }

}
