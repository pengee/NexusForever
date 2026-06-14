using System.Text;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static;
using NexusForever.Game.Static.RBAC;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.WorldServer.Command.Context;
using NexusForever.WorldServer.Command.Static;

namespace NexusForever.WorldServer.Command.Handler
{
    [Command(Permission.None, "Display your current location.", "loc", "location", "whereami")]
    public class LocationCommandCategory : CommandCategory
    {
        public override CommandResult Invoke(ICommandContext context, ParameterQueue queue)
        {
            IPlayer player = context.GetTargetOrInvoker<IPlayer>();
            TextTable tt = GameTableManager.Instance.GetTextTable(context.Language);

            WorldEntry worldEntry = player.Map.Entry;
            string worldName = tt?.GetEntry(worldEntry.LocalizedTextIdName) ?? $"World {worldEntry.Id}";

            var builder = new StringBuilder();
            builder.AppendLine($"World: ({worldEntry.Id}){worldName}");

            if (player.Zone != null)
            {
                string zoneName = tt?.GetEntry(player.Zone.LocalizedTextIdName) ?? $"Zone {player.Zone.Id}";
                builder.AppendLine($"Zone: ({player.Zone.Id}){zoneName}");
            }

            builder.AppendLine($"XYZ: {player.Position.X:F2}, {player.Position.Y:F2}, {player.Position.Z:F2}");
            context.SendMessage(builder.ToString());

            return CommandResult.Ok;
        }
    }
}
