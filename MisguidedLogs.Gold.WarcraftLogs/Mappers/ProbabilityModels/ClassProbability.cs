using MisguidedLogs.Gold.WarcraftLogs.Model;
using System.Text.Json.Serialization;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.ProbabilityModels;

public record ClassProbability(Class Class, List<SpecProbability> Specs, float Probability)
{
    public int AmountOfPlayers => Specs.Sum(x => x.Players.Count);
    public float Probability { get; set; } = Probability;
    public float TotalProbability { get; set; }

    [JsonIgnore]
    public ClassProbabilityDto GetDto => new(Class, [.. Specs.Select(x => x.GetDto)], Probability, TotalProbability);
}
