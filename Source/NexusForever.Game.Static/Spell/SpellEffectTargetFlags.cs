namespace NexusForever.Game.Static.Spell
{
    [Flags]
    public enum SpellEffectTargetFlags
    {
        // world location (non-entity-attached)
        None      = 0x00,
        // caster entity attached
        Caster    = 0x01,
        // PrimaryTargetId attached
        Target    = 0x02,
        // used in many "Heat Wave"
        // Egg Lob - Missile - BEAX
        // Defiling Dash
        Telegraph = 0x04,
        //
        Unknown05 = 0x05,
        // used in Spellslinger & Medic - Shield Surge
        Unknown06 = 0x06, //cone ?
        //
        Unknown07 = 0x07,
        // used in Esper - Dual Favor & Medic - Annihilation
        // & Spellslinger - Wild Barrage & Engineer - Anomaly Launcher
        Unknown08 = 0x08, // caster forward direction ?
        // used in AstrovoidAdv - Telekinetic Throw
        Unknown09 = 0x09
    }
}
