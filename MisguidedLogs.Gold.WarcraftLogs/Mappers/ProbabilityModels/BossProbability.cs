using System.Text.Json.Serialization;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record BossProbability(int BossId, int AmountOfPlayers, List<ClassProbability> Tanks, List<ClassProbability> Dps, List<ClassProbability> Hps, List<ClassProbability> Hybrids)
{
    public int AmountOfPlayers { get; set; } = AmountOfPlayers;

    public int AvgLastDpsValue { get; set; } = 0;
    public int AvgLastHpsValue { get; set; } = 0;
    public int AvgLastDmgTakenValue { get; set; } = 0;
    public short AvgMeleeDmgTakenValue { get; set; } = 0;
    public short AvgTotalDmgTakenByBossValue { get; set; } = 0;
    public short AvgTotalDmgTakenValue { get; set; } = 0;

    [JsonIgnore]
    public BossProbabilityDto GetDto => new(BossId,
        [.. Tanks.Select(x => x.GetDto)],
        [.. Dps.Select(x => x.GetDto)],
        [.. Hps.Select(x => x.GetDto)],
        [.. Hybrids.Select(x => x.GetDto)]);
}
