using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NexusForever.Database.Character.Model;
using NexusForever.Database.Configuration.Model;
using NLog;

namespace NexusForever.Database.Character
{
    [Database(DatabaseType.Character)]
    public class CharacterDatabase : IDatabase
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private IConnectionString config;

        public void Initialise(IConnectionString connectionString)
        {
            config = connectionString;
        }

        public async Task Save(Action<CharacterContext> action)
        {
            await using var context = new CharacterContext(config);
            action.Invoke(context);
            await context.SaveChangesAsync();
        }

        public async Task Save(IDatabaseCharacter entity)
        {
            await using var context = new CharacterContext(config);
            entity.Save(context);
            await context.SaveChangesAsync();
        }

        public async Task Save(IEnumerable<IDatabaseCharacter> entities)
        {
            await using var context = new CharacterContext(config);
            foreach (IDatabaseCharacter entity in entities)
                entity.Save(context);
            await context.SaveChangesAsync();
        }

        public void Migrate()
        {
            using var context = new CharacterContext(config);

            List<string> migrations = context.Database.GetPendingMigrations().ToList();
            if (migrations.Count > 0)
            {
                log.Info($"Applying {migrations.Count} authentication database migration(s)...");
                foreach (string migration in migrations)
                    log.Info(migration);

                context.Database.Migrate();
            }
        }

        public List<CharacterModel> GetAllCharacters()
        {
            using var context = new CharacterContext(config);
            return context.Character.Where(c => c.DeleteTime == null).ToList();
        }

        public ulong GetNextCharacterId()
        {
            using var context = new CharacterContext(config);
            return context.Character
                .Select(r => r.Id)
                .DefaultIfEmpty()
                .Max();
        }

        public async Task<CharacterModel> GetCharacterById(ulong characterId)
        {
            await using var context = new CharacterContext(config);
            return await context.Character.FirstOrDefaultAsync(e => e.Id == characterId);
        }

        public async Task<CharacterModel> GetCharacterByName(string name)
        {
            await using var context = new CharacterContext(config);
            return await context.Character.FirstOrDefaultAsync(e => e.Name == name);
        }

        public ulong GetNextItemId()
        {
            using var context = new CharacterContext(config);
            return context.Item
                .Select(r => r.Id)
                .DefaultIfEmpty()
                .Max();
        }

        public ulong GetNextResidenceId()
        {
            using var context = new CharacterContext(config);
            return context.Residence
                .Select(r => r.Id)
                .DefaultIfEmpty()
                .Max();
        }

        public ulong GetNextDecorId()
        {
            using var context = new CharacterContext(config);
            return context.ResidenceDecor
                .Select(r => r.DecorId)
                .DefaultIfEmpty()
                .Max();
        }

        public async Task<List<CharacterModel>> GetCharacters(uint accountId)
        {
            using var context = new CharacterContext(config);
            return await context.Character.Where(c => c.AccountId == accountId)
                .AsSplitQuery()
                .Include(c => c.Appearance)
                .Include(c => c.Customisation)
                .Include(c => c.Item)
                .Include(c => c.Bone)
                .Include(c => c.Currency)
                .Include(c => c.Path)
                .Include(c => c.CharacterTitle)
                .Include(c => c.Stat)
                .Include(c => c.Costume)
                    .ThenInclude(c => c.CostumeItem)
                .Include(c => c.PetCustomisation)
                .Include(c => c.PetFlair)
                .Include(c => c.Keybinding)
                .Include(c => c.Spell)
                .Include(c => c.ActionSetShortcut)
                .Include(c => c.ActionSetAmp)
                .Include(c => c.Datacube)
                .Include(c => c.Mail)
                    .ThenInclude(c => c.Attachment)
                        .ThenInclude(c => c.Item)
                .Include(c => c.ZonemapHexgroup)
                .Include(c => c.Quest)
                    .ThenInclude(c => c.QuestObjective)
                .Include(c => c.Entitlement)
                .Include(c => c.Achievement)
                .Include(c => c.TradeskillMaterials)
                .Include(c => c.Reputation)
                .ToListAsync();
        }

        public bool CharacterNameExists(string characterName)
        {
            using var context = new CharacterContext(config);
            return context.Character.Any(c => c.Name == characterName);
        }

        public List<ResidenceModel> GetResidences()
        {
            using var context = new CharacterContext(config);
            return context.Residence
                .Include(r => r.Plot)
                .Include(r => r.Decor)
                .Include(r => r.Character)
                .Include(r => r.Guild)
                // only load residences where the owner character or guild hasn't been deleted
                .Where(r => (r.OwnerId.HasValue && !r.Character.DeleteTime.HasValue) || (r.GuildOwnerId.HasValue && !r.Guild.DeleteTime.HasValue))
                .ToList();
        }

        public ulong GetNextMailId()
        {
            using var context = new CharacterContext(config);
            return context.CharacterMail
                .Select(r => r.Id)
                .DefaultIfEmpty()
                .Max();
        }

        public ulong GetNextGuildId()
        {
            using var context = new CharacterContext(config);
            return context.Guild
                .Select(r => r.Id)
                .DefaultIfEmpty()
                .Max();
        }

        public List<GuildModel> GetGuilds()
        {
            using var context = new CharacterContext(config);
            return context.Guild
                .Where(g => g.DeleteTime == null)
                .Include(g => g.GuildRank)
                .Include(g => g.GuildMember)
                .Include(g => g.GuildData)
                .Include(g => g.Achievement)
                .ToList();
        }

        public List<ChatChannelModel> GetChatChannels()
        {
            using var context = new CharacterContext(config);
            return context.ChatChannel
                .Include(c => c.Members)
                .ToList();
        }

        public List<CharacterCreateModel> GetCharacterCreationData()
        {
            using var context = new CharacterContext(config);

            return context.CharacterCreate.ToList();
        }

        public List<PropertyBaseModel> GetProperties(uint type)
        {
            using var context = new CharacterContext(config);
                
            return context.PropertyBase.Where(p => p.Type == type).ToList();
        }

        /// <summary>
        /// List characters with optional filters and pagination.
        /// </summary>
        public (List<CharacterModel> Characters, int Total) GetCharacters(string? nameFilter = null, uint? accountId = null, bool? isOnline = null, int offset = 0, int limit = 50)
        {
            using var context = new CharacterContext(config);

            IQueryable<CharacterModel> query = context.Character.AsNoTracking().Where(c => c.DeleteTime == null);

            if (!string.IsNullOrEmpty(nameFilter))
                query = query.Where(c => c.Name.Contains(nameFilter));
            if (accountId.HasValue)
                query = query.Where(c => c.AccountId == accountId.Value);
            if (isOnline.HasValue)
                query = query.Where(c => c.IsOnline == isOnline.Value);

            int total = query.Count();
            var characters = query
                .OrderByDescending(c => c.LastOnline ?? DateTime.MinValue)
                .Skip(offset).Take(limit)
                .ToList();

            return (characters, total);
        }

        /// <summary>
        /// Get character with full includes for admin detail view.
        /// </summary>
        public async Task<CharacterModel?> GetCharacterDetailAsync(ulong id)
        {
            await using var context = new CharacterContext(config);
            return await context.Character.AsNoTracking()
                .Include(c => c.Item)
                .Include(c => c.Quest).ThenInclude(q => q.QuestObjective)
                .Include(c => c.Spell)
                .Include(c => c.Stat)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>
        /// Set character level directly in database (for offline characters).
        /// </summary>
        public bool SetCharacterLevel(ulong characterId, byte level)
        {
            using var context = new CharacterContext(config);
            var character = context.Character.FirstOrDefault(c => c.Id == characterId);
            if (character == null)
                return false;

            if (level < 1 || level > 50)
                return false;

            character.Level = level;
            context.SaveChanges();
            return true;
        }
    }
}
