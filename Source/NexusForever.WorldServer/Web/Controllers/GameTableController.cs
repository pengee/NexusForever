using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NexusForever.GameTable;

namespace NexusForever.WorldServer.Web.Controllers
{
    [ApiController]
    [Route("api")]
    public class GameTableController : ControllerBase
    {
        private static readonly Regex foreignKeyPattern = new(@"^(.+?)Id(.*)$", RegexOptions.Compiled);

        [HttpGet("tables")]
        public IActionResult GetTables()
        {
            string[] names = GameTableManager.Instance.TableNames?.ToArray();
            if (names == null)
                return Ok(Array.Empty<object>());

            var tables = new List<object>();
            foreach (string name in names)
            {
                IGameTable table = GameTableManager.Instance.GetTable(name);
                if (table != null)
                    tables.Add(new { name, entryType = table.EntryType.Name, count = table.Count });
            }

            return Ok(tables);
        }

        [HttpGet("tables/{name}")]
        public IActionResult GetTable(string name)
        {
            IGameTable table = GameTableManager.Instance.GetTable(name);
            if (table == null)
                return NotFound(new { error = $"Table '{name}' not found." });

            var fields = new List<Dictionary<string, object>>();
            foreach (FieldInfo field in table.EntryType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var entry = new Dictionary<string, object>
                {
                    ["name"] = field.Name,
                    ["type"] = field.FieldType.Name
                };

                if (field.FieldType.IsArray)
                    entry["isArray"] = true;

                Dictionary<string, string> reference = DetectForeignKey(field);
                if (reference != null)
                    entry["references"] = reference;

                fields.Add(entry);
            }

            return Ok(new
            {
                name,
                entryType = table.EntryType.Name,
                count = table.Count,
                fields
            });
        }

        [HttpGet("tables/{name}/entries/{id}")]
        public IActionResult GetEntry(string name, ulong id)
        {
            IGameTable table = GameTableManager.Instance.GetTable(name);
            if (table == null)
                return NotFound(new { error = $"Table '{name}' not found." });

            object entry = table.GetEntry(id);
            if (entry == null)
                return NotFound(new { error = $"Entry {id} not found in '{name}'." });

            return Ok(EntryToDictionary(entry));
        }

        [HttpGet("tables/{name}/entries")]
        public IActionResult GetEntries(string name, [FromQuery] int offset = 0, [FromQuery] int limit = 100,
            [FromQuery] string sortBy = null, [FromQuery] bool sortAsc = true)
        {
            IGameTable table = GameTableManager.Instance.GetTable(name);
            if (table == null)
                return NotFound(new { error = $"Table '{name}' not found." });

            if (limit > 1000) limit = 1000;
            if (offset < 0) offset = 0;

            var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in HttpContext.Request.Query.Keys)
            {
                if (key.StartsWith("filter_", StringComparison.OrdinalIgnoreCase))
                    filters[key[7..]] = HttpContext.Request.Query[key];
            }

            IEnumerable<object> all = table.AllEntries.Cast<object>();

            if (filters.Count > 0)
            {
                Type entryType = table.EntryType;
                all = all.Where(e =>
                {
                    foreach (var filter in filters)
                    {
                        FieldInfo field = entryType.GetField(filter.Key, BindingFlags.Public | BindingFlags.Instance);
                        if (field == null) return false;
                        object val = field.GetValue(e);
                        string str = val?.ToString();
                        if (str == null || str.IndexOf(filter.Value, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    return true;
                });
            }

            int total = filters.Count > 0 ? all.Count() : table.Count;

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                FieldInfo sortField = table.EntryType.GetField(sortBy, BindingFlags.Public | BindingFlags.Instance);
                if (sortField != null && !sortField.FieldType.IsArray)
                {
                    all = sortAsc
                        ? all.OrderBy(e => sortField.GetValue(e) ?? "")
                        : all.OrderByDescending(e => sortField.GetValue(e) ?? "");
                }
            }

            List<object> entries = all
                .Skip(offset)
                .Take(limit)
                .Select(e => (object)EntryToDictionary(e))
                .ToList();

            return Ok(new { total, offset, limit, entries });
        }

        [HttpGet("tables/{name}/search")]
        public IActionResult SearchEntries(string name, [FromQuery] string q, [FromQuery] int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { total = 0, entries = Array.Empty<object>() });

            IGameTable table = GameTableManager.Instance.GetTable(name);
            if (table == null)
                return NotFound(new { error = $"Table '{name}' not found." });

            if (limit > 1000) limit = 1000;

            var results = new List<object>();

            foreach (object entry in table.AllEntries)
            {
                var dict = EntryToDictionary(entry);
                foreach (var kv in dict)
                {
                    if (kv.Value != null && kv.Value.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(dict);
                        break;
                    }
                }

                if (results.Count >= limit)
                    break;
            }

            return Ok(new { total = results.Count, entries = results });
        }

        private static Dictionary<string, object> EntryToDictionary(object entry)
        {
            var dict = new Dictionary<string, object>();
            foreach (FieldInfo field in entry.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(entry);
                if (value != null && field.FieldType.IsEnum)
                    value = (int)value;
                dict[field.Name] = value;
            }
            return dict;
        }

        private static Dictionary<string, string> DetectForeignKey(FieldInfo field)
        {
            if (field.FieldType != typeof(uint) && field.FieldType != typeof(ulong))
                return null;

            if (field.Name.StartsWith("LocalizedTextId"))
            {
                if (GameTableManager.Instance.GetTable("LocalizedText") != null)
                    return new Dictionary<string, string> { ["table"] = "LocalizedText", ["field"] = "Id" };
                return null;
            }

            Match match = foreignKeyPattern.Match(field.Name);
            if (!match.Success)
                return null;

            string candidate = match.Groups[1].Value;
            if (string.IsNullOrEmpty(candidate))
                return null;

            if (GameTableManager.Instance.GetTable(candidate) != null)
                return new Dictionary<string, string> { ["table"] = candidate, ["field"] = "Id" };

            return null;
        }
    }
}
