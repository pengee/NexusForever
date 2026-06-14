using System.Numerics;

namespace NexusForever.Script.Main.Tutorial
{
    public static class TutorialLocations
    {
        //Add P1
        public static readonly Vector3 P2StartCombat = new Vector3(-173.16f, -879.54f, 263.61f);
        public static readonly Vector3 P4StartHousing = new Vector3(0, 0, 0);
    }
    
    /// <summary>
    /// Quest Chain
    /// </summary>
    public enum TutorialReferences : uint
    {
        NavigatingNexusExile       = 10513,
        NavigatingNexusDominion    = 10521,
        NavigatingNexusPt2Exile    = 10527,
        NavigatingNexusPt2Dominion = 10532,
        TheFaceOfTheEnemyExile     = 10518,
        TheFaceOfTheEnemyDominion  = 10524
    }

    public enum QuestObjectives : uint
    {
        AreaPressurePlateOne = 8539,   // Ring 1
        AreaPressurePlateTwo = 8542,   // Ring 2
        AreaPressurePlateThree = 8543, // Ring 3
        HoverboardObjective = 8540,
        CombatTeleportObjective = 73735u,
        DominionExplosiveMine = 21313,
        ExileExplosiveMine = 21287,
    }
    
    /// <summary>
    /// Creatures referenced by objectives
    /// </summary>
    public enum ObjectiveEntities : uint
    {
        // shared //
        Holoring = 73416,
        HoverboardDispenserEntity = 73419,
        HoverboardDispenserEntityOther = 74780,
        RaceArrow = 72051, // Blue Arrows
        ExtraBooster = 73461, // Visible Arrows on ground
        CombatHologramProjector = 73735,  // Tutorial P1 - > p2
        ExplosiveMineSmall = 73463, // Explosive Mine - Easy - NPEU - Part 2 - Wilderrun - JBN (0.75x)
        ExplosiveMineMed = 73667, // Explosive Mine - Medium - NPEU - Part 2 - Wilderrun - JBN (1.0x)
        ExplosiveMineBig = 73668, // Explosive Mine - Hard - NPEU - Part 2 - Wilderrun - JBN  (1.25x)
        BeaconArrow = 73665,
        ExileEliteF = 73473,
        ExileEliteM = 73566,
        // exile
        DominionBattleBeast = 73464,
        DominionFactionTurret = 73494,
        DorianWalker = 73491,
        BoboKloos = 5968,
        // dominion
        ExileDefenseDagun = 73465,
        ExileFactionTurret = 74862,
        ArtemisZin = 74861,
        ConscriptedLaborer = 14402,
        HousingHologramProjector = 73741, // Tutorial P2 - > p3
    }
    
    public enum QuestSpells : uint
    {
        // P1
        ClothesSpawnIn = 86913,
        HoverboardEquip = 85562,
        ExtraGas = 85424, //power buff
        PowerBoost = 85483, //speed + stat buff
        PowerBoostProxy0 = 84387, // mount speed / fall damage change (use this over Power Boost)
        // P2
        MineDismantle = 85452,
        MineDetonateEasy = 85430, // target = 1 (caster)
        MineDetonateMed = 85629,
        MineDetonateHard = 85630,
        CombatBurningCircleFireHazard = 87010,
        TurretImmune = 85474,
        DominionTurretChargeAttack = 85469,
        ExileTurretChargeAttack = 86978,
        TurretMortarAttack = 87011, // 16226 : target -> 0 = (none) , 16227 : target -> 0 = (none) Has Time
        Housingleport = 87061
    }
}
