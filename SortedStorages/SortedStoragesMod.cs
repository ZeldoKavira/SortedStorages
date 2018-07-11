using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SortedStorages
{
    public class SortedStoragesMod : Mod
    {
        public static Config config;
        public override void Entry(IModHelper helper)
        {
            GameEvents.HalfSecondTick += this.GameEvents_HalfSecondTick;
            config = helper.ReadConfig<Config>();
        }

        public Dictionary<Tuple<string, Vector2>, Dictionary<Item, int>> ItemPools = new Dictionary<Tuple<string, Vector2>, Dictionary<Item, int>>();
        private void GameEvents_HalfSecondTick(object sender, EventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player.currentLocation == null)
            {
                return;
            }

            foreach (GameLocation loc in Game1.locations)
            {
                Dictionary<Vector2, Chest> chests = new Dictionary<Vector2, Chest>();
                Dictionary<Vector2, Tuple<Chest, ItemStackChange[]>> changedChests = new Dictionary<Vector2, Tuple<Chest, ItemStackChange[]>>();
                foreach (KeyValuePair<Vector2, StardewValley.Object> obj in loc.Objects)
                {
                    if (obj.Value is Chest)
                    {
                        Chest chest = (Chest)obj.Value;
                        if (chest.playerChest)
                        {
                            chests.Add(obj.Key, chest);
                        }
                    }
                }

                foreach (KeyValuePair<Vector2, Chest> chest in chests)
                {
                    Dictionary<Item, int> OldItems;

                    ItemPools.TryGetValue(new Tuple<string, Vector2>(loc.uniqueName, chest.Key), out OldItems);

                    if (OldItems != null)
                    {
                        // Pull item changes. We only care about additions since a stack change means the item was there previously and has been sorted.
                        // We only pull items that can stack unless category sorting is on, this way we are not doing logic that does not matter unless we need to move tools/ect to a different chest
                        ItemStackChange[] changedItems = Utils.GetInventoryChanges(chest.Value.items, OldItems).Where(ci => ci.ChangeType == ChangeType.Added && (ci.Item.maximumStackSize() > 1 || config.EnableCategorySorting)).ToArray();
                        if (changedItems.Any())
                        {
                            changedChests.Add(chest.Key, new Tuple<Chest, ItemStackChange[]>(chest.Value, changedItems));
                        }
                    }
                }

                if (changedChests.Count > 0)
                {
                    HandleLocationChestChanges(loc, chests, changedChests);
                }

                //This has to be updated after we reconcile changes otherwise our changes will trigger us to run again
                foreach (KeyValuePair<Vector2, Chest> chest in chests)
                {
                    ItemPools[new Tuple<string, Vector2>(loc.uniqueName, chest.Key)] = chest.Value.items.Where(n => n != null).ToDictionary(n => n, n => n.Stack);
                }

                if (config.EnableDebugOutput && chests.Count > 0)
                {
                    Monitor.Log($"Chest Count {chests.Count} in {loc.Name}. {changedChests.Count} changed.");
                }
            }
        }

        private void HandleLocationChestChanges(GameLocation location, Dictionary<Vector2, Chest> chests, Dictionary<Vector2, Tuple<Chest, ItemStackChange[]>> changedChests)
        {
            foreach (KeyValuePair<Vector2, Tuple<Chest, ItemStackChange[]>> changedChest in changedChests)
            {
                ItemStackChange[] changes = changedChest.Value.Item2;
                Vector2 pos = changedChest.Key;
                Chest thisChest = changedChest.Value.Item1;

                foreach (ItemStackChange change in changes)
                {
                    List<Chest> validOutputs = new List<Chest>();
                    List<Chest> validCategoryOutputs = new List<Chest>();
                    foreach (var chest in chests)
                    {
                        if (chest.Key != pos)
                        {
                            if (change.Item.maximumStackSize() > 1)
                            {
                                IEnumerable<Item> stackableItems = chest.Value.items.Where(i => i.canStackWith(change.Item));
                                if (stackableItems.Any())
                                {
                                    //If there is empty space in this chest save it for later incase we cannot stack them all.
                                    if (chest.Value.items.Count < 36)
                                    {
                                        validOutputs.Add(chest.Value);
                                    }

                                    //Attempt to add it to stacks in this chest that can take at least a portion of our stack
                                    foreach (var stackableItem in stackableItems)
                                    {
                                        change.Item.Stack = stackableItem.addToStack(change.Item.Stack);

                                        if (change.Item.Stack <= 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            if (chest.Value.items.Any(i => i.category == change.Item.category) && chest.Value.items.Count < 36)
                            {
                                validCategoryOutputs.Add(chest.Value);
                            }
                        }

                        if (change.Item.Stack <= 0)
                        {
                            break;
                        }
                    }

                    if (change.Item.Stack <= 0)
                    {
                        thisChest.items.Remove(change.Item);
                    }
                    else if (validOutputs.Any())
                    {
                        validOutputs.First().addItem(change.Item);
                        thisChest.items.Remove(change.Item);
                    }
                    else if (validCategoryOutputs.Any())
                    {
                        validCategoryOutputs.First().addItem(change.Item);
                        thisChest.items.Remove(change.Item);
                    }

                    thisChest.clearNulls();
                }
            }
        }
    }
}
