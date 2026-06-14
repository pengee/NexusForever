using System;
using System.Collections;

namespace NexusForever.GameTable
{
    public interface IGameTable
    {
        Type EntryType { get; }
        object GetEntry(ulong id);
        int Count { get; }
        IEnumerable AllEntries { get; }
    }
}
