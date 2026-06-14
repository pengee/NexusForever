using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Abstract.Entity;
// ReSharper disable InconsistentNaming

namespace NexusForever.Game;

public static class CCHelper
{
    public static readonly CCState[] MovementCCStates =
    [
        CCState.Stun,
        CCState.Sleep,
        CCState.Fear,
        CCState.Hold,
        CCState.Polymorph,
        CCState.Disorient,
        CCState.Knockdown,
        CCState.Knockback
    ];
        
    public static readonly CCState[] SpellCCStates =
    [
        CCState.Stun,
        CCState.Sleep,
        CCState.Fear,
        CCState.Hold,
        CCState.Polymorph,
        CCState.Disorient,
        CCState.Knockdown,
        CCState.Disable,
        CCState.Knockback,
        CCState.Silence,
        CCState.Disarm, //eh?
        CCState.Blind,
        CCState.AbilityRestriction
    ];
    
    public static bool EntityHasCC(ICreatureEntity entity, CCState state) => entity.HasCCState(state);

    public static bool EntityHasMovementCC(ICreatureEntity entity) => MovementCCStates.Any(entity.HasCCState);
    public static bool EntityHasSpellCC(ICreatureEntity entity) => SpellCCStates.Any(entity.HasCCState);
}
