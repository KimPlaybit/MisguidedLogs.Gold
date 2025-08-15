using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record ClassProbabilityDto(Class Class, List<SpecProbabilityDto> Specs, float Probability, float TotalProbability);
