using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using NexusForever.Game.Static.RBAC;
using NexusForever.GameTable;
using NexusForever.WorldServer.Command.Context;
using NexusForever.WorldServer.Command.Static;

namespace NexusForever.WorldServer.Command.Handler
{
    [Command(Permission.GameTable, "Query game table data.", "gametable", "gt")]
    public class GameTableCommandCategory : CommandCategory
    {
        [Command(Permission.GameTable, "List all loaded game tables.", "list")]
        public void HandleList(ICommandContext context)
        {
            string[] names = GameTableManager.Instance.TableNames?.ToArray();
            if (names == null || names.Length == 0)
            {
                context.SendMessage("No game tables loaded.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Loaded GameTables ({names.Length}) ===");
            foreach (string name in names)
                sb.AppendLine(name);

            context.SendMessage(sb.ToString());
        }

        [Command(Permission.GameTable, "Lookup an entry by ID.", "lookup")]
        public void HandleLookup(ICommandContext context,
            [Parameter("Table name")]
            string tableName,
            [Parameter("Entry ID")]
            uint id)
        {
            IGameTable table = GameTableManager.Instance.GetTable(tableName);
            if (table == null)
            {
                context.SendError($"Unknown table: {tableName}");
                return;
            }

            object entry = table.GetEntry(id);
            if (entry == null)
            {
                context.SendError($"Entry {id} not found in {tableName}.");
                return;
            }

            context.SendMessage(FormatEntry(tableName, entry));
        }

        [Command(Permission.GameTable, "Show entry count for a table.", "count")]
        public void HandleCount(ICommandContext context,
            [Parameter("Table name")]
            string tableName)
        {
            IGameTable table = GameTableManager.Instance.GetTable(tableName);
            if (table == null)
            {
                context.SendError($"Unknown table: {tableName}");
                return;
            }

            context.SendMessage($"{tableName}: {table.Count} entries ({table.EntryType.Name})");
        }

        private static string FormatEntry(string tableName, object entry)
        {
            var sb = new StringBuilder();
            Type type = entry.GetType();
            sb.AppendLine($"{tableName} ({type.Name}):");

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(entry);
                sb.AppendLine($"  {field.Name}: {FormatValue(value)}");
            }

            return sb.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string str)
                return $"\"{str}\"";

            if (value.GetType().IsArray)
            {
                var array = (Array)value;
                var elements = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                    elements[i] = FormatValue(array.GetValue(i));
                return $"[{string.Join(", ", elements)}]";
            }

            if (value.GetType().IsEnum)
                return $"{value} ({(int)value})";

            return value.ToString();
        }
    }
}
