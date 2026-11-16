using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Fraga.Tests.Editor
{
    public class FragaCoreEditModeTests
    {
        private GameObject tempPlayerObj;

        [TearDown]
        public void TearDown()
        {
            if (tempPlayerObj != null)
            {
                Object.DestroyImmediate(tempPlayerObj);
                tempPlayerObj = null;
            }
        }

        [Test]
        public void ItemDatabase_InitializesAndResolvesCoreItems()
        {
            ItemDatabase.Initialize();

            int[] requiredIds = { 100, 101, 115, 209, 210, 211, 300 };
            foreach (int id in requiredIds)
            {
                ItemData item = ItemDatabase.GetItem(id);
                Assert.IsNotNull(item, $"Item ID {id} deveria existir no ItemDatabase.");
                Assert.IsFalse(string.IsNullOrEmpty(item.itemName), $"Item ID {id} precisa de itemName.");
            }
        }

        [Test]
        public void InventoryAdvanced_AddStackRemoveAndEquipFlow()
        {
            tempPlayerObj = new GameObject("TestInventoryHolder");
            InventoryAdvanced inv = tempPlayerObj.AddComponent<InventoryAdvanced>();

            MethodInfo awakeMethod = typeof(InventoryAdvanced).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awakeMethod);
            awakeMethod.Invoke(inv, null);

            inv.AddItem(100, 10);
            Assert.AreEqual(10, inv.GetItemCount(100));

            bool removed = inv.RemoveItem(100, 4);
            Assert.IsTrue(removed);
            Assert.AreEqual(6, inv.GetItemCount(100));

            inv.AddItem(300, 1);
            Assert.IsTrue(inv.HasItem(300));
            Assert.AreEqual(300, inv.equippedMainHandId);

            Assert.IsTrue(inv.RemoveItem(300, 1));
            Assert.AreEqual(-1, inv.equippedMainHandId);
        }

        [Test]
        public void WorldBlueprint_IslandBoundsAndTerrainHeightAreDeterministic()
        {
            Vector2 camp = FragaWorldBlueprint.CrashCampPos;
            Assert.IsTrue(FragaWorldBlueprint.IsInsideIsland(camp.x, camp.y));
            Assert.IsFalse(FragaWorldBlueprint.IsInsideIsland(-50f, -50f));

            FragaWorldBlueprint.WorldRegion regionA = FragaWorldBlueprint.GetRegion(camp.x, camp.y);
            FragaWorldBlueprint.WorldRegion regionB = FragaWorldBlueprint.GetRegion(camp.x, camp.y);
            Assert.AreEqual(regionA, regionB);

            float h1 = TerrainMeshGenerator.GetTerrainHeight(camp.x, camp.y);
            float h2 = TerrainMeshGenerator.GetTerrainHeight(camp.x, camp.y);
            Assert.AreEqual(h1, h2, 0.0001f);
            Assert.Greater(h1, 0f);
        }

        [Test]
        public void SaveData_V3JsonRoundTripPreservesStatusAndInventory()
        {
            SaveData original = new SaveData
            {
                mapSeed = 4242,
                currentDay = 4,
                currentTime = 14.5f,
                isSick = true,
                sicknessTimer = 75.5f,
                isFractured = true,
                isBleeding = false
            };
            original.inventoryItemIDs.Add(100);
            original.inventoryItemAmounts.Add(12);
            original.inventorySlotDurabilities.Add(-1f);

            string json = JsonUtility.ToJson(original);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(3, loaded.saveVersion);
            Assert.AreEqual(4242, loaded.mapSeed);
            Assert.AreEqual(4, loaded.currentDay);
            Assert.IsTrue(loaded.isSick);
            Assert.AreEqual(75.5f, loaded.sicknessTimer, 0.01f);
            Assert.IsTrue(loaded.isFractured);
            Assert.IsFalse(loaded.isBleeding);
            Assert.AreEqual(1, loaded.inventoryItemIDs.Count);
            Assert.AreEqual(100, loaded.inventoryItemIDs[0]);
            Assert.AreEqual(12, loaded.inventoryItemAmounts[0]);
        }
    }
}
