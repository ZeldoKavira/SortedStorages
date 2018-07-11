using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SortedStorages
{
    public static class Utils
    {
        /// <summary>Get the inventory changes between two states.</summary>
        /// <param name="current">The current inventory.</param>
        /// <param name="previous">The previous inventory.</param>
        public static IEnumerable<ItemStackChange> GetInventoryChanges(IEnumerable<Item> current, IDictionary<Item, int> previous)
        {
            current = current.Where(n => n != null).ToArray();
            foreach (Item item in current)
            {
                // stack size changed
                if (previous != null && previous.ContainsKey(item))
                {
                    if (previous[item] != item.Stack)
                        yield return new ItemStackChange { Item = item, StackChange = item.Stack - previous[item], ChangeType = ChangeType.StackChange };
                }

                // new item
                else
                    yield return new ItemStackChange { Item = item, StackChange = item.Stack, ChangeType = ChangeType.Added };
            }

            // removed items
            if (previous != null)
            {
                foreach (var entry in previous)
                {
                    if (current.Any(i => i == entry.Key))
                        continue;

                    yield return new ItemStackChange { Item = entry.Key, StackChange = -entry.Key.Stack, ChangeType = ChangeType.Removed };
                }
            }
        }
    }
}
