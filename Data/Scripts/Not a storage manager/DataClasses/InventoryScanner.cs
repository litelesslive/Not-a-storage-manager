﻿using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using NotAStorageManager.Data.Scripts.Not_a_storage_manager.AbstractClass;
using NotAStorageManager.Data.Scripts.Not_a_storage_manager.NoIdeaHowToNameFiles;
using NotAStorageManager.Data.Scripts.Not_a_storage_manager.StaticClasses;
using NotAStorageManager.Data.Scripts.Not_a_storage_manager.StorageSubclasses;
using Sandbox.Game;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace NotAStorageManager.Data.Scripts.Not_a_storage_manager.DataClasses
{
    public class InventoryScanner : ModBase
    {
        public HashSet<MyInventory> AllInventories = new HashSet<MyInventory>();

        public Dictionary<MyInventory, List<MyPhysicalInventoryItem>> Snapshot =
            new Dictionary<MyInventory, List<MyPhysicalInventoryItem>>();

        private readonly ItemDefinitionStorage _itemDefinitionStorage;

        public InventoryScanner(ItemDefinitionStorage itemDefinitionStorage)
        {
            _itemDefinitionStorage = itemDefinitionStorage;
        }

        public void ScanAllInventories()
        {
            try
            {
                if (AllInventories == null)
                {
                    ModLogger.Instance.LogWarning(ClassName, $"All inventories is somehow null");
                    return;
                }


                ModLogger.Instance.Log(ClassName, $"Inventories count {AllInventories.Count}");
                foreach (var items in AllInventories.Select(inventory => inventory.GetItems()))
                {
                    if (items == null)return;
                    foreach (var item in items)
                    {
                        var definitionId = item.GetDefinitionId();
                        if (!CountedTypes.Contains(definitionId.TypeId.ToString())) continue;
                        _itemDefinitionStorage.TryUpdateValue(definitionId, item.Amount);
                    }

                    // Clear the list to prepare for the next inventory
                    items.Clear();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Instance.LogError(ClassName,$"Congrats, all inventories scan fucked up {ex}");
            }
        }


        public void AddInventory(IMyInventory inventory)
        {
            try
            {
                var myInventory = (MyInventory)inventory;
                AllInventories.Add(myInventory);
                if (!inventory.Empty())
                {
                    var items = myInventory.GetItems(); // Reuse the same list for each inventory
                    foreach (var item in items)
                    {
                        var definitionId = item.GetDefinitionId();
                        if (!CountedTypes.Contains(definitionId.TypeId.ToString())) continue;
                        _itemDefinitionStorage.TryUpdateValue(definitionId, item.Amount);
                    }

                    // Clear the list to prepare for the next inventory
                    items.Clear();
                }

                inventory.OnVolumeChanged += Inventory_OnVolumeChanged;
            }
            catch (Exception ex)
            {
                ModLogger.Instance.LogError(ClassName, $"On add inventory error {ex}");
            }
        }
        public void RemoveInventory(MyInventory inventory)
        {
            try
            {
                Snapshot.Remove(inventory);
                AllInventories.Remove(inventory);
                inventory.OnVolumeChanged -= Inventory_OnVolumeChanged;
            }
            catch (Exception ex)
            {
                ModLogger.Instance.LogError(ClassName, $"On remove inventory error {ex}");
            }
        }

        private void Inventory_OnVolumeChanged(IMyInventory arg1, float arg2, float arg3)
        {
            try
            {
                var inventory = (MyInventory)arg1;

                // Retrieve the current items in the inventory
                var newValue = inventory.GetItems();
                List<MyPhysicalInventoryItem> oldValue;
                if (!Snapshot.TryGetValue(inventory, out oldValue))
                {
                    // If the snapshot doesn't exist, initialize it and return
                    Snapshot[inventory] = newValue;
                    return;
                }

                // Group old and new items by MyDefinitionId and sum their amounts
                var oldGrouped = oldValue
                    .GroupBy(item => item.GetDefinitionId())
                    .ToDictionary(group => group.Key,
                        group => group.Aggregate(MyFixedPoint.Zero, (total, next) => total + next.Amount));

                var newGrouped = newValue
                    .GroupBy(item => item.GetDefinitionId())
                    .ToDictionary(group => group.Key,
                        group => group.Aggregate(MyFixedPoint.Zero, (total, next) => total + next.Amount));

                // HashSet to ensure unique MyDefinitionIds
                var uniqueIds = new HashSet<MyDefinitionId>(oldGrouped.Keys);
                uniqueIds.UnionWith(newGrouped.Keys);

                foreach (var id in uniqueIds.Where(id => _itemDefinitionStorage.ContainsKey(id)))
                {
                    // Calculate the difference between old and new values
                    MyFixedPoint oldValueSum;
                    var oldAmount = oldGrouped.TryGetValue(id, out oldValueSum) ? oldValueSum : MyFixedPoint.Zero;
                    MyFixedPoint newValueSum;
                    var newAmount = newGrouped.TryGetValue(id, out newValueSum) ? newValueSum : MyFixedPoint.Zero;

                    var result = newAmount - oldAmount;

                    // Update the dictionary with the difference
                    _itemDefinitionStorage.TryUpdateValue(id, result);
                }

                // Updating the snapshot with the new inventory state
                Snapshot[inventory] = newValue;
            }
            catch (Exception ex)
            {
                MyAPIGateway.Utilities.ShowMessage(ClassName, $"Error in Inventory_OnVolumeChanged: {ex.Message}");
            }
        }


        public override void Dispose()
        {
            try
            {
                ModLogger.Instance.LogWarning(ClassName, "OnDispose was called");
                foreach (var inventory in AllInventories)
                {
                    inventory.OnVolumeChanged -= Inventory_OnVolumeChanged;
                }

                AllInventories.Clear();
                Snapshot.Clear();
            }
            catch (Exception ex)
            {
                ModLogger.Instance.LogError(ClassName, $"On dispose error {ex}");
            }
        }
    }
}