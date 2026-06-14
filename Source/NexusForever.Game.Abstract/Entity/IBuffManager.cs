using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IBuffManager
    {
        /// <summary>
        /// Update all active <see cref="IBuff"/> instances, removing any that have expired.
        /// </summary>
        void Update(double lastTick);

        /// <summary>
        /// Add a new <see cref="IBuff"/> to the manager, or stack with an existing buff from the same spell and caster.
        /// </summary>
        IBuff AddBuff(IUnitEntity caster, IUnitEntity target, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint castingId, uint effectId, Action<IBuff> tickCallback = null);

        /// <summary>
        /// Remove the supplied <see cref="IBuff"/> from the manager.
        /// </summary>
        void RemoveBuff(IBuff buff);

        /// <summary>
        /// Remove an <see cref="IBuff"/> by its casting id.
        /// </summary>
        void RemoveBuff(uint castingId);

        /// <summary>
        /// Remove all <see cref="IBuff"/> instances matching the supplied spell4 id.
        /// </summary>
        void RemoveBuffsBySpell(uint spell4Id);
        
        /// <summary>
        /// Remove all buff instances matching the supplied SpellEffectType.
        /// </summary>
        /// <param name="effectType"></param>
        void RemoveBuffsByEffectType(SpellEffectType effectType);

        /// <summary>
        /// Remove all <see cref="IBuff"/> instances cast by the supplied caster guid.
        /// </summary>
        void RemoveBuffsByCaster(uint casterGuid);

        /// <summary>
        /// Remove all active <see cref="IBuff"/> instances.
        /// </summary>
        void RemoveAllBuffs();

        /// <summary>
        /// Return all active <see cref="IBuff"/> instances.
        /// </summary>
        IEnumerable<IBuff> GetBuffs();

        /// <summary>
        /// Return all active <see cref="IBuff"/> instances matching the supplied predicate.
        /// </summary>
        IEnumerable<IBuff> GetBuffs(Func<IBuff, bool> predicate);

        /// <summary>
        /// Return an <see cref="IBuff"/> by its casting id.
        /// </summary>
        IBuff GetBuff(uint castingId);

        /// <summary>
        /// Returns whether an <see cref="IBuff"/> with the supplied casting id exists.
        /// </summary>
        bool HasBuff(uint castingId);

        /// <summary>
        /// Return an <see cref="IBuff"/> matching the supplied spell4 id and caster guid.
        /// </summary>
        IBuff GetBuffBySpell(uint spell4Id, uint casterGuid);

        /// <summary>
        /// Return an <see cref="IBuff"/> with the supplied effect id, or null.
        /// </summary>
        IBuff GetBuffByEffectId(uint effectId);

        /// <summary>
        /// Remove up to <paramref name="count"/> dispellable buffs, preferring debuffs if <paramref name="dispelDebuff"/> is true.
        /// </summary>
        IEnumerable<IBuff> RemoveDispellableBuffs(uint count, bool dispelDebuff);

        /// <summary>
        /// Remove all active channeled <see cref="IBuff"/> instances.
        /// </summary>
        void RemoveChanneledBuffs();

        void Save(CharacterContext context);

        void Load(IPlayer player, IEnumerable<CharacterBuffModel> models);
    }
}
