using MisguidedLogs.Gold.WarcraftLogs.Model;

namespace MisguidedLogs.Gold.WarcraftLogs.Mappers.Achivements;

public class AllAchivements
{
    private readonly static Class[] ClassesWithTaunt = [Class.Warrior, Class.Druid];
    public readonly static (Class _Class, Achivement Achivement)[] ClassToAchivement =
    [
            (Class.Paladin, Achivement.ForTheLight),
            (Class.Warrior, Achivement.BruteForce),
            (Class.Hunter, Achivement.TheBigHunt),
            (Class.Shaman, Achivement.StormEarthFire),
            (Class.Druid, Achivement.EarthMotherIsWatching),
            (Class.Rogue, Achivement.Assassination),
            (Class.Priest, Achivement.ArcanePower),
            (Class.Mage, Achivement.LightWillGuideUs),
            (Class.Warrior, Achivement.EmbraceTheShadows),
    ];
    public Achivement[] GetFulfilledAchivements(ReportWithPlayersRoles report)
    {
        var achivements = new List<Achivement>();
        foreach (var _class in ClassToAchivement)
        {
            if (report.Players.All(x => x.Class == _class._Class))
            {
                achivements.Add(_class.Achivement);
            }
        }

        if (!report.Players.Any(x => ClassesWithTaunt.Contains(x.Class)))
        {
            achivements.Add(Achivement.HeyNoTaunt);
        }

        return [.. achivements];
    }

}


public record ReportWithPlayersRoles(List<Player> Tanks, List<Player> Dps, List<Player> Healers, List<Player> Hybrid)
{
    public List<Player> Players { get; init; } = Tanks.Concat(Dps).Concat(Healers).Concat(Hybrid).ToList();
}

public enum Achivement
{
    ForTheLight,
    BruteForce,
    TheBigHunt,
    StormEarthFire,
    EarthMotherIsWatching,
    Assassination,
    ArcanePower,
    LightWillGuideUs,
    EmbraceTheShadows,
    HeyNoTaunt,
    TheAntiMeta
}