using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record SpecProbabilityDto(TalentSpec Spec, float Probability, float TotalProbability);