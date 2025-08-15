using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Text.Json.Serialization;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record SpecProbability(TalentSpec Spec, float Probability, List<string> Players)
{
    public float Probability { get; set; } = Probability;
    public float TotalProbability { get; set; } = 0;

    [JsonIgnore]
    public SpecProbabilityDto GetDto => new(Spec, Probability, TotalProbability);
}
