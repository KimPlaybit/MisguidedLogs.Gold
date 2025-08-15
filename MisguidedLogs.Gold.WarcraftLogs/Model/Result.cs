using System.Globalization;
using System.Security.Claims;

namespace MisguidedLogs.Gold.WarcraftLogs.Model;


public record Zone(int Id)
{
    public string Name => Id is 2018 ? "Scarlet Enclave" : ""; // <--- Get name somehow ?
}
public record Boss(int Id, string Name, int ZoneId);
public record Fight(string FightId, int BossId, DateTime StartTime, DateTime EndTime)
{
    public string ReportCode => FightId.Split("_")[1];
    public int BossId => int.Parse(FightId.Split("_")[0], CultureInfo.InvariantCulture);
    public int FightVersion => int.Parse(FightId.Split("_")[2], CultureInfo.InvariantCulture);
}

//The Id for the players stats is BossId, Code and FightId
public record PlayerStats(string PlayerId, string FightId, float Hps, float Dps, long TotalDamageTaken, long TotalDamageTakenByBoss, long MeleeDmgTaken, Specialization Spec);
public record Specialization(TalentSpec Spec, int FirstTree, int SecondTree, int ThirdTree);
public record Player(string PlayerId, int Guid, string Name, string Server, string Region, Class Class);
public enum TalentSpec
{
    Discipline,
    Holy,
    Shadow,
    Affliction,
    Demonlogy,
    Destruction,
    Arcane,
    Fire,
    Frost,
    Assasination,
    Combat,
    Subtlety,
    Balance,
    Feral,
    Restoration,
    BeastMastery,
    Marksmanship,
    Survival,
    Elemental,
    Enhancement,
    Protection,
    Retribution,
    Arms,
    Fury,
    Blood,
    Unholy,
    Hybrid
}

public enum Class
{
    Priest,
    Warlock,
    Mage,
    Rogue,
    Druid,
    Shaman,
    Hunter,
    Warrior,
    Paladin,
    Monk,
    DemonHunter,
    DeathKnight,
    Evoker
}