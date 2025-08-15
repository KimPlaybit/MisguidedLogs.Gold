namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record BossProbabilityDto(int BossId, List<ClassProbabilityDto> Tanks, List<ClassProbabilityDto> Dps, List<ClassProbabilityDto> Hps, List<ClassProbabilityDto> Hybrids);
