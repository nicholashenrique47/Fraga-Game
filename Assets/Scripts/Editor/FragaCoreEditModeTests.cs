using System.IO;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fraga.Tests.Editor
{
    public class FragaCoreEditModeTests
    {
        private GameObject tempPlayerObj;
        private readonly List<GameObject> tempObjects = new List<GameObject>();
        private readonly List<string> tempFiles = new List<string>();

        private GameObject Track(GameObject go)
        {
            if (go != null) tempObjects.Add(go);
            return go;
        }

        private static T InvokeLifecycle<T>(T comp, string methodName) where T : Component
        {
            MethodInfo m = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(m, $"Metodo {typeof(T).Name}.{methodName} nao encontrado.");
            m.Invoke(comp, null);
            return comp;
        }

        [TearDown]
        public void TearDown()
        {
            if (tempPlayerObj != null)
            {
                Object.DestroyImmediate(tempPlayerObj);
                tempPlayerObj = null;
            }

            for (int i = tempObjects.Count - 1; i >= 0; i--)
            {
                if (tempObjects[i] != null)
                {
                    Object.DestroyImmediate(tempObjects[i]);
                }
            }
            tempObjects.Clear();

            for (int i = tempFiles.Count - 1; i >= 0; i--)
            {
                string f = tempFiles[i];
                if (!string.IsNullOrEmpty(f))
                {
                    try
                    {
                        if (File.Exists(f)) File.Delete(f);
                        if (File.Exists(f + ".bak")) File.Delete(f + ".bak");
                    }
                    catch { }
                }
            }
            tempFiles.Clear();

            StructureHealth.AllStructures.RemoveAll(s => s == null);
            WorldBlueprintSite.AllSites.RemoveAll(s => s == null);
            DryingRackInteractable.AllRacks.RemoveAll(r => r == null);
            RainCollectorInteractable.AllCollectors.RemoveAll(c => c == null);
            TrapInteractable.AllTraps.RemoveAll(t => t == null);

            if (StorageChestInteractable.IsAnyChestOpen)
                StorageChestInteractable.CloseModal();
            if (FragaCoopSession.Instance != null)
                Object.DestroyImmediate(FragaCoopSession.Instance.gameObject);
        }

        [Test]
        public void ItemDatabase_InitializesAndResolvesCoreItems()
        {
            ItemDatabase.Initialize();

            int[] requiredIds = { 100, 101, 115, 209, 210, 211, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 400, 401, 402, 403, 404, 405, 406 };
            foreach (int id in requiredIds)
            {
                ItemData item = ItemDatabase.GetItem(id);
                Assert.IsNotNull(item, $"Item ID {id} deveria existir no ItemDatabase.");
                Assert.IsFalse(string.IsNullOrEmpty(item.itemName), $"Item ID {id} precisa de itemName.");
            }

            ItemData sling = ItemDatabase.GetItem(314);
            Assert.AreEqual(EquipSlotType.MainHand, sling.equipSlot);
            Assert.Greater(sling.maxDurability, 0f);
        }

        [Test]
        public void CraftingSystem_AllRecipesHaveValidIdsAndIngredients()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestCraftingHolder");
            CraftingSystem craft = InvokeLifecycle(tempPlayerObj.AddComponent<CraftingSystem>(), "Awake");

            List<CraftRecipe> recipes = craft.GetAllRecipes();
            Assert.Greater(recipes.Count, 15);
            foreach (CraftRecipe r in recipes)
            {
                Assert.IsNotNull(ItemDatabase.GetItem(r.outputItemId), $"Receita '{r.recipeName}' tem output invalido: {r.outputItemId}");
                Assert.Greater(r.outputAmount, 0);
                Assert.AreEqual(r.ingredientIds.Length, r.ingredientAmounts.Length, $"Receita '{r.recipeName}' tem mismatch de arrays.");
                for (int i = 0; i < r.ingredientIds.Length; i++)
                {
                    Assert.IsNotNull(ItemDatabase.GetItem(r.ingredientIds[i]), $"Receita '{r.recipeName}' tem ingrediente invalido: {r.ingredientIds[i]}");
                    Assert.Greater(r.ingredientAmounts[i], 0);
                }
            }
        }

        [Test]
        public void Deployables_TileIdsAreUniqueAndNonColliding()
        {
            int[] deployableIds = { 400, 401, 402, 403, 404, 405, 406 };
            HashSet<int> seenTiles = new HashSet<int>();
            foreach (int itemId in deployableIds)
            {
                int tileId = InventoryAdvanced.GetDeployableTileId(itemId);
                Assert.Greater(tileId, 0, $"Deployable {itemId} sem tile valido.");
                Assert.IsTrue(seenTiles.Add(tileId), $"Colisao de tileID {tileId} no deployable {itemId}!");
            }
            Assert.AreEqual(25, InventoryAdvanced.GetDeployableTileId(402));
            Assert.AreEqual(12, InventoryAdvanced.GetDeployableTileId(403));
            Assert.AreEqual(16, InventoryAdvanced.GetDeployableTileId(406));
        }

        [Test]
        public void InventoryAdvanced_ToolDurabilityBreakBlockAndRepairFlow()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestDurabilityHolder");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            inv.AddItem(314, 1); // Funda Celta (maxDurability = 75)
            inv.AddItem(105, 1); // Pele de Animal para reparo
            Assert.AreEqual(314, inv.equippedMainHandId);

            int slingSlot = inv.FindFirstSlotIndex(314);
            Assert.GreaterOrEqual(slingSlot, 0);

            bool broke = inv.DamageEquippedTool(EquipSlotType.MainHand, 75f);
            Assert.IsTrue(broke);
            Assert.AreEqual(-1, inv.equippedMainHandId);
            Assert.AreEqual(0f, inv.slots[slingSlot].durability, 0.001f);

            // Nao permite reequipar item quebrado
            Assert.IsFalse(inv.EquipItem(314));
            Assert.AreEqual(-1, inv.equippedMainHandId);

            // Valida reparo com Pele de Animal (105)
            int matId = inv.GetRepairIngredientId(314, out string _);
            Assert.AreEqual(105, matId);
            Assert.IsTrue(inv.RepairItemSlot(slingSlot, out _));
            Assert.AreEqual(75f, inv.slots[slingSlot].durability, 0.01f);
            Assert.IsTrue(inv.EquipItem(314));
            Assert.AreEqual(314, inv.equippedMainHandId);
        }

        [Test]
        public void MineableRockNode_PermanentEntersCooldownAfterMaxStrikes()
        {
            tempPlayerObj = new GameObject("TestRockNode");
            MineableRockNode node = tempPlayerObj.AddComponent<MineableRockNode>();
            node.ResetNode(new Vector2Int(10, 10), 2, permanent: true);

            for (int i = 0; i < node.maxHitsBeforeCooldown; i++)
            {
                Assert.IsTrue(node.TryConsumeStrike(out _, out int waitSec));
                Assert.AreEqual(0, waitSec);
            }

            Assert.IsFalse(node.TryConsumeStrike(out int left, out int cooldown));
            Assert.AreEqual(0, left);
            Assert.Greater(cooldown, 0);
        }

        [Test]
        public void ChoppingAndCombatSystem_EnforcesAxeAndRockToolRequirements()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestToolReqHolder");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            // Desarmado: nao pode cortar arvore nem quebrar rocha
            Assert.IsFalse(ChoppingAndCombatSystem.IsAxeEquipped(inv));
            Assert.IsFalse(ChoppingAndCombatSystem.IsRockToolEquipped(inv));
            Assert.IsFalse(ChoppingAndCombatSystem.EnsureAxeEquipped(inv, true));

            // Com Machado de Pedra (300): pode cortar arvore e quebrar rocha
            inv.AddItem(300, 1);
            Assert.AreEqual(300, inv.equippedMainHandId);
            Assert.IsTrue(ChoppingAndCombatSystem.IsAxeEquipped(inv));
            Assert.IsTrue(ChoppingAndCombatSystem.IsRockToolEquipped(inv));

            // Com Picareta de Pedra (301): quebra rocha, mas se bater em arvore e tiver Machado na mochila, auto-equipa o Machado!
            inv.AddItem(301, 1);
            inv.EquipItem(301);
            Assert.IsFalse(ChoppingAndCombatSystem.IsAxeEquipped(inv));
            Assert.IsTrue(ChoppingAndCombatSystem.IsRockToolEquipped(inv));
            Assert.IsTrue(ChoppingAndCombatSystem.EnsureAxeEquipped(inv, true));
            Assert.AreEqual(300, inv.equippedMainHandId);

            // ResourcePickup desativa auto-magnet por padrao para pedras/galhos do chao
            ResourcePickup pickup = tempPlayerObj.AddComponent<ResourcePickup>();
            pickup.ResetPickupState(101, 1, false, Vector2Int.zero);
            Assert.IsTrue(pickup.disableAutoMagnet);
            Assert.AreEqual(0f, pickup.magnetRadius, 0.001f);
        }

        [Test]
        public void WorldBlueprintSite_And_BuiltStructures_CompleteAndInteract()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestBlueprintBuilder");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            // Adiciona recursos para construir a Cabana de Toras (8 Madeiras, 6 Pedras)
            inv.AddItem(100, 10);
            inv.AddItem(101, 8);

            GameObject cabinSiteObj = new GameObject("Blueprint_Cabana de Toras");
            try
            {
                BoxCollider trig = cabinSiteObj.AddComponent<BoxCollider>();
                trig.isTrigger = true;
                trig.size = new Vector3(3.6f, 2.7f, 3.6f);

                GameObject bedMat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bedMat.name = "Bed_Mat";
                bedMat.transform.SetParent(cabinSiteObj.transform, false);
                Object.DestroyImmediate(bedMat.GetComponent<Collider>());

                WorldBlueprintSite site = cabinSiteObj.AddComponent<WorldBlueprintSite>();
                site.structureName = "Cabana de Toras";
                site.neededLogs = 8;
                site.neededStones = 6;

                // Deposita todos os recursos necessarios de uma vez com 1 interacao [E]
                Assert.IsTrue(site.TryDepositResource(tempPlayerObj));

                ShelterInteractable shelter = cabinSiteObj.GetComponent<ShelterInteractable>();
                Assert.IsNotNull(shelter, "Cabana de Toras concluida deve ter ShelterInteractable para Dormir e Salvar Jogo.");
                Assert.IsTrue(shelter.isCabin);
                Assert.IsNotNull(bedMat.GetComponent<Collider>(), "Bed_Mat dentro da Cabana de Toras deve receber BoxCollider para mirar na cama.");
                Assert.IsTrue(shelter.GetPromptLabel().Contains("Salvar Jogo"));
            }
            finally
            {
                if (cabinSiteObj != null) Object.DestroyImmediate(cabinSiteObj);
            }
        }

        [Test]
        public void WorldBlueprintSite_CancelAndRefund_ReturnsDepositedMaterials()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestBlueprintRefundPlayer");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            // Kit inicial tem 2 Madeiras (100) e 2 Pedras (101); adiciona +3 de cada => 5 Madeiras e 5 Pedras
            inv.AddItem(100, 3);
            inv.AddItem(101, 3);
            Assert.AreEqual(5, inv.GetItemCount(100));
            Assert.AreEqual(5, inv.GetItemCount(101));

            GameObject siteObj = Track(new GameObject("Blueprint_Cabana_Refund"));
            WorldBlueprintSite site = siteObj.AddComponent<WorldBlueprintSite>();
            site.structureName = "Cabana de Toras";
            site.neededLogs = 8;
            site.neededStones = 6;

            Assert.IsTrue(site.TryDepositResource(tempPlayerObj));
            Assert.AreEqual(5, site.currentLogs);
            Assert.AreEqual(5, site.currentStones);
            Assert.IsFalse(site.IsComplete);
            Assert.AreEqual(0, inv.GetItemCount(100));
            Assert.AreEqual(0, inv.GetItemCount(101));

            site.CancelAndRefund(tempPlayerObj);
            Assert.AreEqual(5, inv.GetItemCount(100), "Madeira depositada deve ser reembolsada ao cancelar obra.");
            Assert.AreEqual(5, inv.GetItemCount(101), "Pedra depositada deve ser reembolsada ao cancelar obra.");
        }

        [Test]
        public void InventoryAdvanced_OnItemAdded_FiresOnAddAndSuppressesOnReset()
        {
            ItemDatabase.Initialize();
            tempPlayerObj = new GameObject("TestOnItemAdded");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            int eventsCount = 0;
            int lastItemId = -1;
            int lastAdded = -1;
            int lastTotal = -1;
            inv.OnItemAdded += (id, added, total) =>
            {
                eventsCount++;
                lastItemId = id;
                lastAdded = added;
                lastTotal = total;
            };

            inv.ResetToStartingKit();
            Assert.AreEqual(0, eventsCount, "ResetToStartingKit nao deve disparar notificacoes de coleta.");

            inv.AddItem(101, 3); // Kit ja tem 2 pedras -> total 5
            Assert.AreEqual(1, eventsCount);
            Assert.AreEqual(101, lastItemId);
            Assert.AreEqual(3, lastAdded);
            Assert.AreEqual(5, lastTotal);
        }

        [Test]
        public void HUDSurvivalOverlay_GetGameplayCanvas_IgnoresMainMenuAndDebugCanvases()
        {
            GameObject menuObj = Track(new GameObject("MainMenu_Canvas"));
            menuObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject debugObj = Track(new GameObject("Debug_Canvas"));
            debugObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject gameplayObj = Track(new GameObject("UI_Gameplay_Canvas"));
            Canvas gameplayCanvas = gameplayObj.AddComponent<Canvas>();
            gameplayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            Canvas resolved = HUDSurvivalOverlay.GetGameplayCanvas();
            Assert.IsNotNull(resolved);
            Assert.AreNotEqual(menuObj, resolved.gameObject);
            Assert.AreNotEqual(debugObj, resolved.gameObject);
            Assert.AreEqual(gameplayCanvas, resolved);
        }

        [Test]
        public void HUDSurvivalOverlay_InitializesClockUpdatesWithDayNightAndShowsItemFeed()
        {
            ItemDatabase.Initialize();

            GameObject canvasObj = Track(new GameObject("UI_TestHUD_Canvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject dncObj = Track(new GameObject("TestDayNightCycle"));
            DayNightCycle dnc = InvokeLifecycle(dncObj.AddComponent<DayNightCycle>(), "Awake");

            GameObject invObj = Track(new GameObject("TestHUDInventory"));
            InventoryAdvanced inv = InvokeLifecycle(invObj.AddComponent<InventoryAdvanced>(), "Awake");

            GameObject hudObj = Track(new GameObject("TestHUDOverlay"));
            HUDSurvivalOverlay hud = InvokeLifecycle(hudObj.AddComponent<HUDSurvivalOverlay>(), "Awake");
            InvokeLifecycle(hud, "Start");

            Transform hudRoot = canvasObj.transform.Find("HUD_SurvivalElements");
            Assert.IsNotNull(hudRoot, "HUD_SurvivalElements deve ser criado no Canvas de gameplay.");

            Text clockText = hudRoot.Find("Clock/ClockText")?.GetComponent<Text>();
            Text dayText = hudRoot.Find("Clock/DayText")?.GetComponent<Text>();
            Assert.IsNotNull(clockText);
            Assert.IsNotNull(dayText);
            Assert.AreEqual("10:00  DIA", clockText.text);
            Assert.AreEqual("SOBREVIVENCIA - DIA 1", dayText.text);

            // Simula passagem para noite no Dia 3 e valida ForceRefreshNow()
            dnc.currentTime = 21.5f;
            dnc.currentDay = 3;
            hud.ForceRefreshNow();
            Assert.AreEqual("21:30  NOITE", clockText.text);
            Assert.AreEqual("SOBREVIVENCIA - DIA 3", dayText.text);

            // Coleta 4 Madeiras (kit inicial tinha 2 -> Total 6) e valida Feed Empilhado
            inv.AddItem(100, 4);
            Transform feedRow0 = hudRoot.Find("ItemCollectionFeed/FeedRow_0");
            Assert.IsNotNull(feedRow0);
            Assert.IsTrue(feedRow0.gameObject.activeSelf, "FeedRow_0 deve ficar visivel ao coletar item.");
            Text feedTxt = feedRow0.Find("Text")?.GetComponent<Text>();
            Assert.IsNotNull(feedTxt);
            Assert.IsTrue(feedTxt.text.Contains("+4"), $"FeedRow_0 deve exibir quantidade '+4': {feedTxt.text}");
            Assert.IsTrue(feedTxt.text.Contains("Total: 6"), $"FeedRow_0 deve exibir total '(Total: 6)': {feedTxt.text}");
        }

        private MobileInputController CreateTestMobileJoystick(out RectTransform bgRT, out RectTransform handleRT)
        {
            GameObject canvasObj = Track(new GameObject("UI_MobileTest_Canvas"));
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Container fixo 1920x1080 para que o teste nao dependa do tamanho da janela Game View no Editor
            GameObject mockScreen = new GameObject("MockScreen1080p");
            mockScreen.transform.SetParent(canvasObj.transform, false);
            RectTransform mockRT = mockScreen.AddComponent<RectTransform>();
            mockRT.anchorMin = Vector2.zero;
            mockRT.anchorMax = Vector2.zero;
            mockRT.pivot = new Vector2(0.5f, 0.5f);
            mockRT.sizeDelta = new Vector2(1920f, 1080f);
            mockRT.anchoredPosition = new Vector2(960f, 540f);

            GameObject zoneObj = new GameObject("Joystick_TouchZone");
            zoneObj.transform.SetParent(mockScreen.transform, false);
            zoneObj.AddComponent<Image>().color = Color.clear;

            GameObject bgObj = new GameObject("Joystick_BG");
            bgObj.transform.SetParent(mockScreen.transform, false);
            bgObj.AddComponent<Image>();
            bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.zero;
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(180f, 180f);
            bgRT.anchoredPosition = new Vector2(160f, 160f);

            GameObject handleObj = new GameObject("Joystick_Handle");
            handleObj.transform.SetParent(bgObj.transform, false);
            handleObj.AddComponent<Image>();
            handleRT = handleObj.GetComponent<RectTransform>();
            handleRT.anchorMin = new Vector2(0.5f, 0.5f);
            handleRT.anchorMax = new Vector2(0.5f, 0.5f);
            handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.sizeDelta = new Vector2(75f, 75f);

            MobileInputController mic = zoneObj.AddComponent<MobileInputController>();
            mic.joystickBackground = bgRT;
            mic.joystickHandle = handleRT;
            mic.joystickRadius = 80f;
            mic.deadZone = 6f;
            mic.joystickType = MobileInputController.JoystickType.DynamicFloatingFollow;

            InvokeLifecycle(mic, "Awake");
            InvokeLifecycle(mic, "Start");
            return mic;
        }

        [Test]
        public void MobileInputController_DynamicFloatingFollow_PositionsClampsAndFollowsThumb()
        {
            MobileInputController mic = CreateTestMobileJoystick(out RectTransform bgRT, out RectTransform handleRT);
            mic.SetJoystickMode(MobileInputController.JoystickType.DynamicFloatingFollow);

            // 1. Toque dentro da zona esquerda: ancora corretamente na posicao do dedo (sem jogar fora da tela)
            mic.BeginDragAt(0, new Vector2(180f, 190f));
            Assert.IsTrue(mic.IsDragging);
            Assert.Greater(bgRT.anchoredPosition.x, 100f);
            Assert.Greater(bgRT.anchoredPosition.y, 100f);
            Assert.AreEqual(Vector2.zero, mic.GetInputVector());

            // 2. Micro-movimento dentro da deadzone (< 6px): permanece zero
            Vector2 startAnchor = bgRT.anchoredPosition;
            mic.UpdateDragAt(new Vector2(183f, 190f), 0.016f);
            Assert.AreEqual(Vector2.zero, mic.GetInputVector());

            // 3. Arrasto completo para a direita (80px): valor analogico proximo de +1 no eixo X e handle no limite do raio
            mic.UpdateDragAt(new Vector2(260f, 190f), 0.016f);
            Assert.Greater(mic.GetInputVector().x, 0.90f);
            Assert.AreEqual(80f, handleRT.anchoredPosition.x, 2f);

            // 4. Arrasto alem do raio (follow thumb): a base acompanha para direita mantendo o handle preso no raio
            mic.UpdateDragAt(new Vector2(360f, 190f), 0.016f);
            Assert.Greater(bgRT.anchoredPosition.x, startAnchor.x + 20f);
            Assert.LessOrEqual(handleRT.anchoredPosition.magnitude, mic.joystickRadius + 1f);

            // 5. Toque muito perto da borda inferior esquerda: base e clampada dentro da margem segura
            mic.EndDrag();
            Assert.IsFalse(mic.IsDragging);
            Assert.AreEqual(Vector2.zero, mic.GetInputVector());

            mic.BeginDragAt(1, new Vector2(8f, 8f));
            Assert.GreaterOrEqual(bgRT.anchoredPosition.x, mic.joystickRadius + 14f - 1f);
            Assert.GreaterOrEqual(bgRT.anchoredPosition.y, mic.joystickRadius + 14f - 1f);
            mic.EndDrag();
        }

        [Test]
        public void MobileInputController_SprintLock_EngagesInFloatingAndFixedModes()
        {
            MobileInputController mic = CreateTestMobileJoystick(out RectTransform bgRT, out RectTransform _);

            // Modo DynamicFloatingFollow: segurar frente total ativa trava de corrida e puxar para tras cancela
            mic.SetJoystickMode(MobileInputController.JoystickType.DynamicFloatingFollow);
            mic.BeginDragAt(0, new Vector2(220f, 190f));
            mic.UpdateDragAt(new Vector2(220f, 270f), 0.25f);
            Assert.IsTrue(mic.IsSprinting, "Empurrar totalmente para cima deve ativar sprint analogico.");
            Assert.IsFalse(mic.IsSprintLocked);

            mic.UpdateDragAt(new Vector2(220f, 270f), 0.40f);
            Assert.IsTrue(mic.IsSprintLocked, "Segurar frente total por > 0.55s deve engatar Sprint Lock.");
            Assert.IsTrue(mic.IsSprinting);

            mic.UpdateDragAt(new Vector2(220f, 150f), 0.016f);
            Assert.IsFalse(mic.IsSprintLocked, "Puxar o joystick para tras deve cancelar Sprint Lock.");
            mic.EndDrag();

            // Modo Fixo: base permanece fixa e empurrar alem de 1.35x raio ativa Sprint Lock imediato
            mic.SetJoystickMode(MobileInputController.JoystickType.Fixed);
            Vector2 fixedPos = bgRT.anchoredPosition;
            mic.BeginDragAt(0, new Vector2(210f, 180f));
            Assert.AreEqual(fixedPos, bgRT.anchoredPosition);
            mic.UpdateDragAt(new Vector2(fixedPos.x, fixedPos.y + 125f), 0.016f);
            Assert.IsTrue(mic.IsSprintLocked);
            Assert.IsTrue(mic.IsSprinting);
            mic.EndDrag();
        }

        [Test]
        public void CameraTouchController_AccumulatesIgnoresSpikesAndResetsOnDisable()
        {
            GameObject camTouchObj = Track(new GameObject("CameraTouchZone"));
            CameraTouchController camTouch = InvokeLifecycle(camTouchObj.AddComponent<CameraTouchController>(), "Awake");
            EventSystem es = Track(new GameObject("TestEventSystem")).AddComponent<EventSystem>();

            PointerEventData down = new PointerEventData(es) { pointerId = 7, position = new Vector2(1400f, 500f) };
            camTouch.OnPointerDown(down);
            Assert.IsTrue(camTouch.IsDragging);
            Assert.AreEqual(7, camTouch.ActivePointerId);

            PointerEventData drag1 = new PointerEventData(es) { pointerId = 7, delta = new Vector2(14f, -6f) };
            PointerEventData drag2 = new PointerEventData(es) { pointerId = 7, delta = new Vector2(6f, 2f) };
            camTouch.OnDrag(drag1);
            camTouch.OnDrag(drag2);

            Vector2 consumed = camTouch.GetTouchDelta();
            Assert.AreEqual(20f, consumed.x, 0.01f);
            Assert.AreEqual(-4f, consumed.y, 0.01f);
            Assert.AreEqual(Vector2.zero, camTouch.GetTouchDelta(), "Delta deve zerar apos ser consumido.");

            // Spike > 240px em 1 frame e ignorado para evitar giro brusco multi-touch
            PointerEventData spike = new PointerEventData(es) { pointerId = 7, delta = new Vector2(480f, 0f) };
            camTouch.OnDrag(spike);
            Assert.AreEqual(Vector2.zero, camTouch.GetTouchDelta());

            // Desativar ao abrir modal reseta o estado de arrasto sem travar camera
            camTouchObj.SetActive(false);
            InvokeLifecycle(camTouch, "OnDisable");
            Assert.IsFalse(camTouch.IsDragging);
            Assert.AreEqual(-1, camTouch.ActivePointerId);
        }

        [Test]
        public void FragaInputRouter_And_UIManager_MobileLayoutTouchZonesAndSiblingOrder()
        {
            GameObject canvasObj = Track(new GameObject("UI_Mobile_Canvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Track(new GameObject("TestEventSystem_Router")).AddComponent<EventSystem>();

            GameObject routerObj = Track(new GameObject("TestInputRouter"));
            FragaInputRouter router = InvokeLifecycle(routerObj.AddComponent<FragaInputRouter>(), "Awake");

            UIManager uiMgr = InvokeLifecycle(canvasObj.AddComponent<UIManager>(), "Awake");
            InvokeLifecycle(router, "Start");
            InvokeLifecycle(uiMgr, "Start");

            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Mobile);
            Assert.IsTrue(router.IsTouchControlsActive);

            Transform camZone = canvasObj.transform.Find("CameraTouchZone");
            Transform joyZone = canvasObj.transform.Find("Joystick_TouchZone");
            Transform joyBg = canvasObj.transform.Find("Joystick_BG");
            Transform cluster = canvasObj.transform.Find("MobileControls_Cluster");
            Transform hotbar = canvasObj.transform.Find("HotbarHUD_Root");

            Assert.IsNotNull(camZone);
            Assert.IsNotNull(joyZone);
            Assert.IsNotNull(joyBg);
            Assert.IsNotNull(cluster);
            Assert.IsNotNull(hotbar);
            Assert.IsTrue(cluster.gameObject.activeSelf);
            Assert.IsTrue(joyZone.gameObject.activeSelf);

            // Touch zones devem ficar abaixo de Hotbar e Cluster Mobile na hierarquia de raycast
            Assert.Less(camZone.GetSiblingIndex(), hotbar.GetSiblingIndex());
            Assert.Less(joyZone.GetSiblingIndex(), hotbar.GetSiblingIndex());
            Assert.Less(joyZone.GetSiblingIndex(), cluster.GetSiblingIndex());

            // Todos os 10 botoes mobile vetoriais existem no cluster
            string[] expectedButtons =
            {
                "Btn_Mobile_Attack", "Btn_Mobile_Jump", "Btn_Mobile_Interact",
                "Btn_Mobile_Inventory", "Btn_Mobile_Crouch", "Btn_Mobile_Block",
                "Btn_Mobile_Lantern", "Btn_Mobile_Radial", "Btn_Mobile_Build",
                "Btn_Mobile_Dodge"
            };
            foreach (string btnName in expectedButtons)
            {
                Assert.IsNotNull(cluster.Find(btnName), $"Botao mobile '{btnName}' ausente no cluster.");
            }

            // Hooks mobile disparam eventos no FragaInputRouter durante Update
            MobileInputController mic = joyZone.GetComponent<MobileInputController>();
            Assert.IsNotNull(mic);
            mic.BeginDragAt(0, new Vector2(180f, 180f));
            mic.UpdateDragAt(new Vector2(260f, 180f), 0.016f);

            router.TriggerMobileInteract();
            router.TriggerMobileJump();
            router.TriggerMobilePrimaryDown();
            router.TriggerMobileDodge();
            router.TriggerMobileHotbarSlot(2);
            InvokeLifecycle(router, "Update");

            Assert.Greater(router.MoveInput.x, 0.85f);
            Assert.IsTrue(router.InteractPressedThisFrame);
            Assert.IsTrue(router.JumpPressedThisFrame);
            Assert.IsTrue(router.DodgePressedThisFrame);
            Assert.IsTrue(router.PrimaryDownThisFrame);
            Assert.IsTrue(router.PrimaryHeld);
            Assert.AreEqual(2, router.HotbarKeyPressedThisFrame);

            // Ao alternar para Desktop, controles touch ficam ocultos
            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Desktop);
            Assert.IsFalse(router.IsTouchControlsActive);
            Assert.IsFalse(cluster.gameObject.activeSelf);
            Assert.IsFalse(joyZone.gameObject.activeSelf);
        }

        [Test]
        public void InventoryAdvanced_StartingKitAndAddStackRemoveFlow()
        {
            tempPlayerObj = new GameObject("TestInventoryHolder");
            InventoryAdvanced inv = InvokeLifecycle(tempPlayerObj.AddComponent<InventoryAdvanced>(), "Awake");

            // Valida kit inicial e hotbar sem itens fantasmas ("0")
            Assert.AreEqual(2, inv.GetItemCount(100));
            Assert.AreEqual(2, inv.GetItemCount(101));
            Assert.AreEqual(3, inv.GetItemCount(205));
            Assert.AreEqual(5, inv.GetItemCount(202));
            for (int h = 0; h < inv.hotbarItemIds.Length; h++)
            {
                int hid = inv.hotbarItemIds[h];
                if (hid > 0)
                {
                    Assert.IsTrue(inv.HasItem(hid), $"Hotbar slot {h} aponta para item {hid} inexistente!");
                }
            }

            inv.AddItem(100, 10);
            Assert.AreEqual(12, inv.GetItemCount(100));

            bool removed = inv.RemoveItem(100, 4);
            Assert.IsTrue(removed);
            Assert.AreEqual(8, inv.GetItemCount(100));

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
                isBleeding = false,
                isLighterOn = true
            };
            original.inventoryItemIDs.Add(100);
            original.inventoryItemAmounts.Add(12);
            original.inventorySlotDurabilities.Add(-1f);
            original.blueprintStructures.Add(new SavedBlueprintStructureData
            {
                structureName = "Cabana de Toras",
                isComplete = true,
                currentHealth = 210f
            });

            string json = JsonUtility.ToJson(original);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(5, loaded.saveVersion);
            Assert.IsTrue(loaded.isLighterOn);
            Assert.AreEqual(4242, loaded.mapSeed);
            Assert.AreEqual(4, loaded.currentDay);
            Assert.IsTrue(loaded.isSick);
            Assert.AreEqual(75.5f, loaded.sicknessTimer, 0.01f);
            Assert.IsTrue(loaded.isFractured);
            Assert.IsFalse(loaded.isBleeding);
            Assert.AreEqual(1, loaded.inventoryItemIDs.Count);
            Assert.AreEqual(100, loaded.inventoryItemIDs[0]);
            Assert.AreEqual(12, loaded.inventoryItemAmounts[0]);
            Assert.AreEqual(1, loaded.blueprintStructures.Count);
            Assert.AreEqual("Cabana de Toras", loaded.blueprintStructures[0].structureName);
            Assert.IsTrue(loaded.blueprintStructures[0].isComplete);
        }

        [Test]
        public void BlueprintPlacement_And_SaveSystem_RoundTripsCompletedStructuresAndChestContents()
        {
            ItemDatabase.Initialize();

            GameObject host = Track(new GameObject("TestSaveHost"));
            DayNightCycle dnc = InvokeLifecycle(host.AddComponent<DayNightCycle>(), "Awake");
            dnc.currentDay = 3;
            dnc.currentTime = 16.25f;
            SanitySystem sanity = InvokeLifecycle(host.AddComponent<SanitySystem>(), "Awake");
            PlayerStatus status = InvokeLifecycle(host.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            BlueprintPlacementManager bpMgr = InvokeLifecycle(host.AddComponent<BlueprintPlacementManager>(), "Awake");
            SaveSystem saveSys = InvokeLifecycle(host.AddComponent<SaveSystem>(), "Awake");

            saveSys.playerTransform = host.transform;
            saveSys.dayNightCycle = dnc;
            saveSys.sanitySystem = sanity;
            saveSys.playerStatus = status;
            saveSys.inventory = inv;

            string testSavePath = Path.Combine(Path.GetTempPath(), $"fraga_editmode_save_{System.Guid.NewGuid():N}.dat");
            tempFiles.Add(testSavePath);
            saveSys.SetSavePathForTesting(testSavePath);

            bpMgr.ClearAllPlacedStructures();

            // 1. Cabana de Toras concluida
            GameObject cabin = bpMgr.SpawnStructureObject("Cabana de Toras", new Vector3(140f, 3f, 140f), 45f, 10, 6);
            cabin.GetComponent<WorldBlueprintSite>().ForceCompleteFromSave();

            // 2. Bau de Base concluido com itens dentro
            GameObject chestObj = bpMgr.SpawnStructureObject("Bau de Base", new Vector3(144f, 3f, 142f), 0f, 2, 1);
            chestObj.GetComponent<WorldBlueprintSite>().ForceCompleteFromSave();
            StorageChestInteractable chest = chestObj.GetComponent<StorageChestInteractable>();
            Assert.IsNotNull(chest);
            chest.ImportStacks(new List<SavedChestStackData>
            {
                new SavedChestStackData { itemId = 100, amount = 9 },
                new SavedChestStackData { itemId = 205, amount = 4 }
            });

            // 3. Cesto de Toras com 7 toras armazenadas
            GameObject holderObj = bpMgr.SpawnStructureObject("Cesto de Toras", new Vector3(146f, 3f, 142f), 90f, 2, 1);
            holderObj.GetComponent<WorldBlueprintSite>().ForceCompleteFromSave();
            LogHolderInteractable holder = holderObj.GetComponent<LogHolderInteractable>();
            Assert.IsNotNull(holder);
            holder.SetStoredLogs(7);

            // 4. Parede com Porta aberta
            GameObject doorObj = bpMgr.SpawnStructureObject("Parede com Porta", new Vector3(148f, 3f, 142f), 180f, 3, 0);
            doorObj.GetComponent<WorldBlueprintSite>().ForceCompleteFromSave();
            DoorInteractable door = doorObj.GetComponent<DoorInteractable>();
            Assert.IsNotNull(door);
            door.SetOpenState(true, silent: true);

            // 5. Obra em andamento (Palicada 2/3 madeiras)
            GameObject siteObj = bpMgr.SpawnStructureObject("Palicada Pontiaguda", new Vector3(152f, 3f, 142f), 30f, 3, 0);
            siteObj.GetComponent<WorldBlueprintSite>().currentLogs = 2;

            saveSys.SaveGame();
            Assert.IsTrue(File.Exists(testSavePath), "SaveGame deve gravar o ficheiro de teste.");

            bpMgr.ClearAllPlacedStructures();
            Assert.AreEqual(0, StructureHealth.AllStructures.Count);
            Assert.AreEqual(0, WorldBlueprintSite.AllSites.Count);

            saveSys.LoadGame();

            Assert.AreEqual(4, StructureHealth.AllStructures.Count, "Todas as 4 estruturas concluidas devem ser restauradas.");
            Assert.AreEqual(1, WorldBlueprintSite.AllSites.Count, "A obra parcial em andamento deve ser restaurada.");
            Assert.AreEqual(2, WorldBlueprintSite.AllSites[0].currentLogs);
            Assert.AreEqual(3, WorldBlueprintSite.AllSites[0].neededLogs);

            StorageChestInteractable loadedChest = Object.FindAnyObjectByType<StorageChestInteractable>();
            Assert.IsNotNull(loadedChest);
            Assert.AreEqual(2, loadedChest.stacks.Count);
            Assert.AreEqual(100, loadedChest.stacks[0].itemId);
            Assert.AreEqual(9, loadedChest.stacks[0].amount);
            Assert.AreEqual(205, loadedChest.stacks[1].itemId);
            Assert.AreEqual(4, loadedChest.stacks[1].amount);

            LogHolderInteractable loadedHolder = Object.FindAnyObjectByType<LogHolderInteractable>();
            Assert.IsNotNull(loadedHolder);
            Assert.AreEqual(7, loadedHolder.storedLogs);

            DoorInteractable loadedDoor = Object.FindAnyObjectByType<DoorInteractable>();
            Assert.IsNotNull(loadedDoor);
            Assert.IsTrue(loadedDoor.isOpen);

            bpMgr.ClearAllPlacedStructures();
        }

        [Test]
        public void ShelterInteractable_Interact_AdvancesTimeDryingRackAndRainCollector()
        {
            ItemDatabase.Initialize();

            GameObject worldObj = Track(new GameObject("TestShelterWorld"));
            DayNightCycle dnc = InvokeLifecycle(worldObj.AddComponent<DayNightCycle>(), "Awake");
            dnc.currentDay = 1;
            dnc.currentTime = 22.0f;

            PlayerStatus status = InvokeLifecycle(worldObj.AddComponent<PlayerStatus>(), "Awake");
            status.health = 40f;
            status.stamina = 20f;
            status.hunger = 80f;
            status.thirst = 80f;
            status.isBleeding = true;

            SanitySystem sanity = InvokeLifecycle(worldObj.AddComponent<SanitySystem>(), "Awake");
            sanity.sanity = 30f;

            InventoryAdvanced inv = InvokeLifecycle(worldObj.AddComponent<InventoryAdvanced>(), "Awake");
            inv.AddItem(104, 1); // Carne crua

            GameObject rackObj = Track(new GameObject("TestDryingRack"));
            DryingRackInteractable rack = rackObj.AddComponent<DryingRackInteractable>();
            rack.BuildHangingSlots();
            rack.Interact(inv);
            Assert.IsTrue(rack.GetPromptLabel(inv).Contains("curando"));

            GameObject collectorObj = Track(new GameObject("TestRainCollector"));
            RainCollectorInteractable collector = collectorObj.AddComponent<RainCollectorInteractable>();
            collector.waterCharges = 0;
            collector.BuildWaterVisual();

            GameObject cabinObj = Track(new GameObject("TestCabinShelter"));
            ShelterInteractable shelter = cabinObj.AddComponent<ShelterInteractable>();
            shelter.isCabin = true;
            shelter.Interact();

            Assert.AreEqual(2, dnc.currentDay);
            Assert.AreEqual(6.5f, dnc.currentTime, 0.01f);
            Assert.AreEqual(95f, status.health, 0.01f);
            Assert.AreEqual(100f, status.stamina, 0.01f);
            Assert.IsFalse(status.isBleeding);
            Assert.AreEqual(90f, sanity.sanity, 0.01f);
            Assert.Less(status.hunger, 80f);
            Assert.GreaterOrEqual(status.hunger, 18f);
            Assert.IsTrue(rack.GetPromptLabel(inv).Contains("pronto"), "Dormir a noite deve curar a carne no secador.");
            Assert.AreEqual(collector.maxCharges, collector.waterCharges, "Dormir a noite deve acumular agua no coletor.");
        }

        [Test]
        public void PlayerController_SetLighterState_SyncsWithSanitySystemAndRespectsEmptyOil()
        {
            GameObject playerObj = Track(new GameObject("TestLighterPlayer"));
            SanitySystem sanity = InvokeLifecycle(playerObj.AddComponent<SanitySystem>(), "Awake");
            PlayerController pc = InvokeLifecycle(playerObj.AddComponent<PlayerController>(), "Awake");

            sanity.oil = 50f;
            pc.SetLighterState(false);
            Assert.IsFalse(pc.IsLighterOn);
            Assert.IsFalse(sanity.isLampOn);

            pc.ToggleLighter();
            Assert.IsTrue(pc.IsLighterOn);
            Assert.IsTrue(sanity.isLampOn);

            sanity.oil = 0f;
            pc.SetLighterState(true);
            Assert.IsFalse(pc.IsLighterOn, "Sem oleo e sem tocha/lampiao na mao esquerda, o lampiao nao deve acender.");
            Assert.IsFalse(sanity.isLampOn);
        }

        [Test]
        public void HUDSurvivalOverlay_And_UIManager_AdaptMobileContextualLabelsAndTouchPrompts()
        {
            ItemDatabase.Initialize();

            GameObject canvasObj = Track(new GameObject("UI_AdaptiveLabels_Canvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Track(new GameObject("TestEventSystem_Adaptive")).AddComponent<EventSystem>();

            GameObject routerObj = Track(new GameObject("TestRouter_Adaptive"));
            FragaInputRouter router = InvokeLifecycle(routerObj.AddComponent<FragaInputRouter>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(routerObj.AddComponent<InventoryAdvanced>(), "Awake");
            DayNightCycle dnc = InvokeLifecycle(routerObj.AddComponent<DayNightCycle>(), "Awake");

            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");
            UIManager uiMgr = InvokeLifecycle(canvasObj.AddComponent<UIManager>(), "Awake");
            InvokeLifecycle(hud, "Start");
            InvokeLifecycle(router, "Start");
            InvokeLifecycle(uiMgr, "Start");

            TutorialManager tut = InvokeLifecycle(routerObj.AddComponent<TutorialManager>(), "Awake");
            tut.inventory = inv;

            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Mobile);

            // Tool icons + contextual labels no Mobile
            inv.AddItem(300, 1); // Machado de Pedra
            InvokeLifecycle(uiMgr, "SyncDynamicControlIcons");
            Text atkLbl = canvasObj.transform.Find("MobileControls_Cluster/Btn_Mobile_Attack/Label")?.GetComponent<Text>();
            Assert.IsNotNull(atkLbl);
            Assert.AreEqual("CORTAR", atkLbl.text);

            inv.AddItem(301, 1); // Picareta de Pedra
            inv.EquipItem(301);
            InvokeLifecycle(uiMgr, "SyncDynamicControlIcons");
            Assert.AreEqual("MINERAR", atkLbl.text);

            // Interaction prompt no Mobile: remove [E] e exibe TOQUE no badge
            hud.SetInteractionPrompt(true, "Abrir Bau de Base [E]", FragaProIconLibrary.ControlIcon.Interact);
            Text promptTxt = canvasObj.transform.Find("HUD_SurvivalElements/ContextualInteractionPrompt/PromptText")?.GetComponent<Text>();
            Text keyBadgeTxt = canvasObj.transform.Find("HUD_SurvivalElements/ContextualInteractionPrompt/KeyBadge/KeyText")?.GetComponent<Text>();
            Assert.IsNotNull(promptTxt);
            Assert.IsNotNull(keyBadgeTxt);
            Assert.AreEqual("Abrir Bau de Base", promptTxt.text);
            Assert.AreEqual("TOQUE", keyBadgeTxt.text);
            Assert.IsTrue(keyBadgeTxt.transform.parent.gameObject.activeSelf);

            // Tutorial no Mobile nao exibe atalhos [E]/[I] de teclado
            tut.RefreshUIForCurrentLayout();
            Text objTitle = canvasObj.transform.Find("HUD_SurvivalElements/ObjectiveTracker/Title")?.GetComponent<Text>();
            Assert.IsNotNull(objTitle);
            Assert.IsFalse(objTitle.text.Contains("[E]"));

            // Desktop restaura badge E e prompt original
            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Desktop);
            hud.SetInteractionPrompt(true, "Abrir Bau de Base [E]", FragaProIconLibrary.ControlIcon.Interact);
            Assert.AreEqual("Abrir Bau de Base [E]", promptTxt.text);
            Assert.AreEqual("E", keyBadgeTxt.text);
        }

        [Test]
        public void BodyTemperatureSystem_BonfireProximity_UnlatchesWhenMovingAway()
        {
            GameObject fireObj = Track(new GameObject("TestBonfire"));
            fireObj.transform.position = Vector3.zero;
            BonfireEntity fire = fireObj.AddComponent<BonfireEntity>();
            Assert.IsTrue(fire.isLit);

            GameObject playerObj = Track(new GameObject("TestBodyTempPlayer"));
            playerObj.transform.position = new Vector3(2f, 0f, 0f);
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            BodyTemperatureSystem bodyTemp = InvokeLifecycle(playerObj.AddComponent<BodyTemperatureSystem>(), "Awake");
            bodyTemp.playerStatus = status;

            InvokeLifecycle(bodyTemp, "Update");
            Assert.IsTrue(status.isNearBonfire, "Proximo da fogueira acesa deve ativar aquecimento.");

            // Afasta o jogador alem do raio (5m -> 30m) e forca nova varredura
            playerObj.transform.position = new Vector3(30f, 0f, 30f);
            FieldInfo nextScanField = typeof(BodyTemperatureSystem).GetField("nextFireScanTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(nextScanField);
            nextScanField.SetValue(bodyTemp, 0f);

            InvokeLifecycle(bodyTemp, "Update");
            Assert.IsFalse(status.isNearBonfire, "Ao se afastar da fogueira, isNearBonfire deve desativar sem ficar preso.");
        }

        [Test]
        public void PlayerStatus_Exhaustion_LocksBelowThresholdAndRecoversAt18()
        {
            GameObject playerObj = Track(new GameObject("TestExhaustionPlayer"));
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");

            status.stamina = 0.25f;
            status.isSprinting = false;
            InvokeLifecycle(status, "UpdateStamina");
            Assert.IsTrue(status.isExhausted, "Estamina zerada deve travar o jogador em estado exausto.");
            Assert.IsFalse(status.CanSpendStamina(4f));

            status.stamina = 12f;
            InvokeLifecycle(status, "UpdateStamina");
            Assert.IsTrue(status.isExhausted, "Exaustao deve permanecer ativa ate atingir >= 18 de estamina.");
            Assert.IsFalse(status.CanSpendStamina(4f));

            status.stamina = 18.5f;
            InvokeLifecycle(status, "UpdateStamina");
            Assert.IsFalse(status.isExhausted, "Exaustao deve liberar ao atingir >= 18 de estamina.");
            Assert.IsTrue(status.CanSpendStamina(4f));
        }

        [Test]
        public void PlayerController_TryDodge_GrantsIFramesSpendsStaminaAndRespectsFractureAndExhaustion()
        {
            GameObject playerObj = Track(new GameObject("TestDodgePlayer"));
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            PlayerController pc = InvokeLifecycle(playerObj.AddComponent<PlayerController>(), "Awake");

            status.stamina = 100f;
            bool dodged = pc.TryDodge();
            Assert.IsTrue(dodged, "TryDodge deve executar com estamina cheia e sem fratura.");
            Assert.IsTrue(pc.IsInvulnerable, "Esquiva deve conceder i-frames imediatos.");
            Assert.AreEqual(82f, status.stamina, 0.01f, "Esquiva deve consumir 18 de estamina.");

            // Reseta cooldowns para validar bloqueios de fratura e exaustao
            FieldInfo cdField = typeof(PlayerController).GetField("dodgeCooldownTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cdField);
            cdField.SetValue(pc, 0f);

            status.isFractured = true;
            Assert.IsFalse(pc.TryDodge(), "Fratura deve impedir esquiva.");

            status.isFractured = false;
            status.isExhausted = true;
            Assert.IsFalse(pc.TryDodge(), "Exaustao deve impedir esquiva.");
        }

        [Test]
        public void WildlifeAIEngine_TakeDamageAndCarcassDrops_UseValidPeltId105()
        {
            ItemDatabase.Initialize();
            GameObject wolfObj = Track(new GameObject("TestWolfAI"));
            WildlifeAIEngine ai = InvokeLifecycle(wolfObj.AddComponent<WildlifeAIEngine>(), "Awake");
            ai.creatureType = WildlifeAIEngine.CreatureType.IberianWolf;
            ai.maxHealth = 75;
            ai.currentHealth = 75;

            ai.TakeDamage(30f);
            Assert.AreEqual(45, ai.currentHealth);
            Assert.AreEqual(WildlifeAIEngine.AIState.Charging, ai.currentState);

            ai.TakeDamage(25f);
            Assert.AreEqual(20, ai.currentHealth);
            Assert.AreEqual(WildlifeAIEngine.AIState.FleeingFire, ai.currentState, "Lobo ferido (<35% HP) deve recuar.");

            MethodInfo dropMethod = typeof(WildlifeAIEngine).GetMethod("SpawnCarcassDrops", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(dropMethod);
            dropMethod.Invoke(ai, null);

            ResourcePickup[] pickups = Object.FindObjectsByType<ResourcePickup>(FindObjectsInactive.Include);
            bool foundMeat = false;
            bool foundPelt = false;
            bool foundBones = false;
            foreach (ResourcePickup p in pickups)
            {
                if (p == null) continue;
                Track(p.gameObject);
                if (p.itemID == 104) foundMeat = true;
                if (p.itemID == 105) foundPelt = true;
                if (p.itemID == 120) foundBones = true;
            }
            Assert.IsTrue(foundMeat, "Carcaca deve dropar Carne Crua (104).");
            Assert.IsTrue(foundPelt, "Carcaca deve dropar Pele de Animal (105).");
            Assert.IsTrue(foundBones, "Carcaca deve dropar Ossos (120).");
        }

        [Test]
        public void BossSystem_EnrageAndMaterialPropertyBlock_TriggersBelowHalfHpWithoutMaterialLeak()
        {
            GameObject bossHost = Track(new GameObject("TestBossHost"));
            BossSystem boss = InvokeLifecycle(bossHost.AddComponent<BossSystem>(), "Awake");

            GameObject bossModel = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            bossModel.transform.SetParent(bossHost.transform, false);
            bossModel.transform.position = new Vector3(10f, 0f, 10f);
            Renderer rend = bossModel.GetComponent<Renderer>();
            Material sharedMat = rend.sharedMaterial;

            typeof(BossSystem).GetField("bossInstance", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(boss, bossModel);
            typeof(BossSystem).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(boss, BossSystem.BossState.Chasing);
            typeof(BossSystem).GetField("bossRenderers", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(boss, new[] { rend });
            boss.currentHealth = boss.maxHealth;

            Assert.IsTrue(boss.IsActive);
            Assert.IsFalse(boss.IsEnraged);

            boss.TakeDamage(300f);
            Assert.AreEqual(200f, boss.currentHealth, 0.01f);
            Assert.IsTrue(boss.IsEnraged, "Boss com <= 50% HP deve entrar em Fase 2 Enfurecida.");

            InvokeLifecycle(boss, "UpdateBossVisualFeedback");
            Assert.IsTrue(rend.HasPropertyBlock(), "Hit flash do Boss deve usar MaterialPropertyBlock.");
            Assert.AreSame(sharedMat, rend.sharedMaterial, "Feedback visual do Boss nao pode instanciar novo material em runtime.");

            bool rayHit = boss.TryDamageInRay(new Ray(new Vector3(10f, 1.8f, 4f), Vector3.forward), 12f, 0.6f, 25f, out Vector3 _, out bool killed);
            Assert.IsTrue(rayHit, "TryDamageInRay deve acertar o Boss no raio do projetil.");
            Assert.IsFalse(killed);
            Assert.AreEqual(175f, boss.currentHealth, 0.01f);
        }

        [Test]
        public void HUDSurvivalOverlay_DirectionalDamageSicknessIconAndBossBar_WorkDeterministically()
        {
            GameObject canvasObj = Track(new GameObject("TestHUD_AAA_Canvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject playerObj = Track(new GameObject("TestHUDPlayer"));
            playerObj.transform.position = Vector3.zero;
            playerObj.transform.rotation = Quaternion.identity;
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            PlayerController pc = InvokeLifecycle(playerObj.AddComponent<PlayerController>(), "Awake");
            hud.playerStatus = status;
            hud.playerController = pc;

            // Status icon Sickness (DOENTE) separado de Veneno
            status.isPoisoned = false;
            status.isSick = true;
            InvokeLifecycle(hud, "UpdateStatusEffects");
            Transform sickIcon = canvasObj.transform.Find("HUD_SurvivalElements/StatusIcon_Sickness");
            Transform poisonIcon = canvasObj.transform.Find("HUD_SurvivalElements/StatusIcon_Poison");
            Assert.IsNotNull(sickIcon);
            Assert.IsTrue(sickIcon.gameObject.activeSelf);
            Assert.IsFalse(poisonIcon.gameObject.activeSelf);

            // Indicador direcional de dano (Direita = indice 3)
            float[] alphas = typeof(HUDSurvivalOverlay).GetField("damageAlphas", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hud) as float[];
            Assert.IsNotNull(alphas);
            Transform viewRef = Camera.main != null ? Camera.main.transform : pc.transform;
            hud.ShowDamageFromDirection(viewRef.position + viewRef.right * 8f);
            Assert.Greater(alphas[3], 0.7f, "Dano vindo da direita deve iluminar o indicador direito.");
            Assert.AreEqual(0f, alphas[2], 0.01f);

            // Barra de Chefe visivel apenas quando Boss ativo
            Transform bossBar = canvasObj.transform.Find("HUD_SurvivalElements/BossHealthBar");
            Assert.IsNotNull(bossBar);
            Assert.IsFalse(bossBar.gameObject.activeSelf);
        }

        [Test]
        public void FragaCoopSession_ModeSwitchJoinCodePermissionsAndSquadPreview_WorkDeterministically()
        {
            FragaCoopSession coop = FragaCoopSession.GetOrCreate();
            Track(coop.gameObject);

            Assert.AreEqual(CoopSessionMode.Solo, coop.sessionMode);
            Assert.IsFalse(coop.ShouldShowPartyHUD);
            Assert.IsTrue(coop.CanLocalPlayerInteract("P2-SCOUT", isSharedWithGroup: true));
            Assert.IsFalse(coop.CanLocalPlayerInteract("P2-SCOUT", isSharedWithGroup: false));
            Assert.IsTrue(coop.CanLocalPlayerInteract(coop.localPlayerId, isSharedWithGroup: false));

            coop.StartCoopHost("FRAGA-7777");
            Assert.IsTrue(coop.IsCoopSessionActive);
            Assert.AreEqual(CoopSessionMode.CoopHost, coop.sessionMode);
            Assert.AreEqual("FRAGA-7777", coop.sessionJoinCode);
            Assert.IsTrue(coop.ShouldShowPartyHUD);

            coop.ToggleSquadPreview();
            Assert.IsTrue(coop.IsSquadPreviewEnabled);
            Assert.AreEqual(3, coop.Members.Count, "Simulacao de esquadrao deve adicionar 2 aliados ao jogador local.");
            Assert.AreEqual(3, coop.CountConnectedMembers());

            bool pingReceived = false;
            string pingLabel = null;
            coop.OnPingBroadcast += (_, _, label) =>
            {
                pingReceived = true;
                pingLabel = label;
            };
            coop.SendQuickPing(new Vector3(12f, 0f, -8f), "Fogueira Norte");
            Assert.IsTrue(pingReceived);
            Assert.AreEqual("Fogueira Norte", pingLabel);

            bool buildEventReceived = false;
            coop.OnBuildCommandBroadcast += evt =>
            {
                if (evt.actionType == "Place" && evt.structureName == "Bau de Base")
                    buildEventReceived = true;
            };
            coop.BroadcastBuildEvent(new CoopBuildSyncEvent
            {
                structureNetId = "STR-01",
                structureName = "Bau de Base",
                actionType = "Place",
                isSharedWithGroup = true
            });
            Assert.IsTrue(buildEventReceived);

            coop.SetSoloMode();
            Assert.AreEqual(CoopSessionMode.Solo, coop.sessionMode);
            Assert.AreEqual(1, coop.Members.Count);
        }

        [Test]
        public void InventoryAdvanced_SortAndCompact_MergesStacksAndComputesWeightWithoutItemLoss()
        {
            ItemDatabase.Initialize();
            GameObject holder = Track(new GameObject("TestSortInventory"));
            InventoryAdvanced inv = InvokeLifecycle(holder.AddComponent<InventoryAdvanced>(), "Awake");

            int initialWood = inv.GetItemCount(100);
            int initialStone = inv.GetItemCount(101);

            // Espalha pilhas fragmentadas de Madeira (100) e Pedra (101) em slots distantes
            inv.slots[10].itemId = 100;
            inv.slots[10].quantity = 3;
            inv.slots[14].itemId = 100;
            inv.slots[14].quantity = 4;
            inv.slots[18].itemId = 101;
            inv.slots[18].quantity = 5;

            int expectedWood = initialWood + 7;
            int expectedStone = initialStone + 5;
            int occupiedBefore = inv.GetOccupiedSlotCount();

            inv.SortAndCompact();

            Assert.AreEqual(expectedWood, inv.GetItemCount(100), "Organizar mochila nao pode perder madeira.");
            Assert.AreEqual(expectedStone, inv.GetItemCount(101), "Organizar mochila nao pode perder pedra.");
            Assert.Less(inv.GetOccupiedSlotCount(), occupiedBefore, "Pilhas fragmentadas devem ser fundidas.");
            Assert.Greater(inv.GetEstimatedWeightKg(), 0f);

            // Slots vazios devem estar agrupados no final apos compactacao
            bool seenEmpty = false;
            for (int i = 0; i < inv.maxSlots; i++)
            {
                if (inv.slots[i].IsEmpty()) seenEmpty = true;
                else Assert.IsFalse(seenEmpty, "Nao pode existir slot ocupado depois de slot vazio apos SortAndCompact().");
            }
        }

        [Test]
        public void SurvivalInventoryCraftUI_FourTabsAndQuickTraumaTreatment_CuresConditions()
        {
            ItemDatabase.Initialize();
            GameObject canvasObj = Track(new GameObject("TestBackpackCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject playerObj = Track(new GameObject("TestBackpackPlayer"));
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            CraftingSystem craft = InvokeLifecycle(playerObj.AddComponent<CraftingSystem>(), "Awake");

            SurvivalInventoryCraftUI ui = canvasObj.AddComponent<SurvivalInventoryCraftUI>();
            ui.inventory = inv;
            ui.playerStatus = status;
            ui.craftingSystem = craft;
            InvokeLifecycle(ui, "Awake");

            ui.OpenMenu();
            Assert.IsTrue(ui.IsOpen);
            Transform win = canvasObj.transform.Find("SurvivalInventory_Root/Window_Mat");
            Assert.IsNotNull(win);
            Assert.IsNotNull(win.Find("Tab_Inventory"));
            Assert.IsNotNull(win.Find("Tab_Crafting"));
            Assert.IsNotNull(win.Find("Tab_CharacterStatus"));
            Assert.IsNotNull(win.Find("Tab_CoopGroup"));

            ui.OpenCharacterStatusTab();
            Assert.IsTrue(win.Find("Tab_CharacterStatus").gameObject.activeSelf);
            Assert.IsFalse(win.Find("Tab_Inventory").gameObject.activeSelf);
            Assert.IsNotNull(win.Find("Tab_CharacterStatus/TraumaColumn/AnatomyDiagramPanel"), "Aba Status deve conter diagrama anatomico.");
            Assert.IsNotNull(win.Find("Tab_CharacterStatus/TraumaColumn/AnatomyDiagramPanel/Btn_TreatAllTraumas"), "Aba Status deve conter botao Tratar Todos.");

            // Cura rapida de Sangramento (206) e Fratura (209) pela aba Status do Personagem
            inv.AddItem(206, 1);
            inv.AddItem(209, 1);
            status.isBleeding = true;
            status.isFractured = true;

            Assert.IsTrue(ui.TryQuickTreatCondition("bleeding"));
            Assert.IsFalse(status.isBleeding, "Sangramento deve ser curado ao aplicar ligadura.");

            Assert.IsTrue(ui.TryQuickTreatCondition("fracture"));
            Assert.IsFalse(status.isFractured, "Fratura deve ser curada ao aplicar tala.");

            // Valida cura em lote por TryTreatAllActiveConditions()
            inv.AddItem(206, 1);
            inv.AddItem(213, 1);
            status.isBleeding = true;
            status.isPoisoned = true;
            ui.TryTreatAllActiveConditions();
            Assert.IsFalse(status.isBleeding, "TryTreatAllActiveConditions deve tratar sangramento.");
            Assert.IsFalse(status.isPoisoned, "TryTreatAllActiveConditions deve tratar veneno.");

            ui.CloseMenu();
            Assert.IsFalse(ui.IsOpen);
        }

        [Test]
        public void StorageChest_And_BlueprintOwnership_PersistsCoopAccessAndDepositedResources()
        {
            ItemDatabase.Initialize();
            FragaCoopSession coop = FragaCoopSession.GetOrCreate();
            Track(coop.gameObject);

            GameObject canvasObj = Track(new GameObject("TestChestCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject host = Track(new GameObject("TestCoopSaveHost"));
            DayNightCycle dnc = InvokeLifecycle(host.AddComponent<DayNightCycle>(), "Awake");
            SanitySystem sanity = InvokeLifecycle(host.AddComponent<SanitySystem>(), "Awake");
            PlayerStatus status = InvokeLifecycle(host.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            BlueprintPlacementManager bpMgr = InvokeLifecycle(host.AddComponent<BlueprintPlacementManager>(), "Awake");
            SaveSystem saveSys = InvokeLifecycle(host.AddComponent<SaveSystem>(), "Awake");

            saveSys.playerTransform = host.transform;
            saveSys.dayNightCycle = dnc;
            saveSys.sanitySystem = sanity;
            saveSys.playerStatus = status;
            saveSys.inventory = inv;

            string testSavePath = Path.Combine(Path.GetTempPath(), $"fraga_coop_save_{System.Guid.NewGuid():N}.dat");
            tempFiles.Add(testSavePath);
            saveSys.SetSavePathForTesting(testSavePath);
            bpMgr.ClearAllPlacedStructures();

            GameObject chestObj = bpMgr.SpawnStructureObject("Bau de Base", new Vector3(145f, 3f, 145f), 0f, 2, 1);
            chestObj.GetComponent<WorldBlueprintSite>().ForceCompleteFromSave();
            StorageChestInteractable chest = chestObj.GetComponent<StorageChestInteractable>();
            StructureHealth sh = chestObj.GetComponent<StructureHealth>();
            Assert.IsNotNull(chest);
            Assert.IsNotNull(sh);

            // Carrega 12 pilhas distintas (acima do limite antigo de 9 linhas) e valida ScrollView completo
            var manyStacks = new List<SavedChestStackData>();
            int[] sampleIds = { 100, 101, 102, 103, 104, 105, 106, 107, 109, 118, 120, 205 };
            foreach (int id in sampleIds)
            {
                manyStacks.Add(new SavedChestStackData { itemId = id, amount = 2 });
            }
            chest.ImportStacks(manyStacks);
            Assert.AreEqual(12, chest.stacks.Count);

            chest.Interact();
            Assert.IsTrue(StorageChestInteractable.IsAnyChestOpen);
            Transform chestContent = canvasObj.transform.Find("BaseStorageChestModal/Window/ChestCol/Content");
            Assert.IsNotNull(chestContent);
            Assert.AreEqual(12, chestContent.childCount, "Modal scrollavel do Bau deve exibir todas as 12 pilhas sem corte em 9.");
            StorageChestInteractable.CloseModal();
            Assert.IsFalse(StorageChestInteractable.IsAnyChestOpen);

            // Alterna trava de acesso privado e testa bloqueio por dono diferente
            Assert.IsTrue(chest.ToggleCoopAccessLock());
            Assert.IsFalse(sh.isSharedWithGroup);
            sh.ownerPlayerId = "P2-REMOTE";
            sh.ownerPlayerName = "Iago";
            Assert.IsFalse(chest.CanLocalPlayerAccess(), "Bau privado de outro jogador deve bloquear acesso local.");
            chest.Interact();
            Assert.IsFalse(StorageChestInteractable.IsAnyChestOpen);

            // Restaura posse local privada, deposita recursos e valida persistencia no SaveSystem
            sh.ownerPlayerId = coop.localPlayerId;
            sh.ownerPlayerName = coop.localPlayerName;
            inv.AddItem(100, 4);
            int moved = chest.DepositAllResourcesFromBag(inv);
            Assert.Greater(moved, 0);

            saveSys.SaveGame();
            bpMgr.ClearAllPlacedStructures();
            saveSys.LoadGame();

            StorageChestInteractable reloadedChest = Object.FindAnyObjectByType<StorageChestInteractable>();
            Assert.IsNotNull(reloadedChest);
            StructureHealth reloadedSh = reloadedChest.GetComponent<StructureHealth>();
            Assert.IsNotNull(reloadedSh);
            Assert.IsFalse(reloadedSh.isSharedWithGroup, "Permissao privada do Bau deve persistir apos Save/Load.");
            Assert.AreEqual(coop.localPlayerId, reloadedSh.ownerPlayerId);
            bpMgr.ClearAllPlacedStructures();
            Assert.IsNotNull(hud);
        }

        [Test]
        public void HUDSurvivalOverlay_And_OptionsQuickBar_ShowCoopPartyHUDAndStatusButton()
        {
            FragaCoopSession coop = FragaCoopSession.GetOrCreate();
            Track(coop.gameObject);

            GameObject canvasObj = Track(new GameObject("TestCoopHudCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");
            FragaOptionsMenu opt = InvokeLifecycle(canvasObj.AddComponent<FragaOptionsMenu>(), "Awake");
            Assert.IsNotNull(opt);

            Transform quickStatusBtn = canvasObj.transform.Find("HUD_QuickActionBar/Btn_QuickStatus");
            Assert.IsNotNull(quickStatusBtn, "Barra rapida deve expor botao STAT [K].");

            Transform partyHud = canvasObj.transform.Find("HUD_SurvivalElements/CoopPartyHUD");
            Assert.IsNotNull(partyHud);
            hud.UpdateCoopPartyHUD();
            Assert.IsFalse(partyHud.gameObject.activeSelf, "Painel Co-op no HUD deve permanecer oculto no modo Solo.");

            coop.ToggleSquadPreview();
            hud.UpdateCoopPartyHUD();
            Assert.IsTrue(partyHud.gameObject.activeSelf, "Painel Co-op no HUD deve aparecer quando sessao Co-op/Simulacao estiver ativa.");
            Assert.IsTrue(partyHud.Find("CoopMemberSlot_0").gameObject.activeSelf);
            Assert.IsTrue(partyHud.Find("CoopMemberSlot_1").gameObject.activeSelf);
        }

        [Test]
        public void HUDSurvivalOverlay_BiometricStatusCard_HasLinearMetersIconsAndPinnedTracker()
        {
            GameObject canvasObj = Track(new GameObject("TestBiometricHudCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject playerObj = Track(new GameObject("TestBiometricPlayer"));
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");

            HUDSurvivalOverlay hud = canvasObj.AddComponent<HUDSurvivalOverlay>();
            hud.playerStatus = status;
            InvokeLifecycle(hud, "Awake");
            Assert.IsNotNull(inv);

            Transform vitals = canvasObj.transform.Find("HUD_SurvivalElements/DiegeticVitalsWidget");
            Assert.IsNotNull(vitals, "DiegeticVitalsWidget deve existir no HUD.");
            Assert.IsNotNull(vitals.Find("BiometricHeaderPill"), "Cartao de biometria deve ter cabecalho clicavel [K].");
            Assert.IsNotNull(vitals.Find("HealthBarRow/HeartBadge/HeartIcon"), "Barra de vida deve exibir icone vetorial de coracao.");

            Transform hpFillT = vitals.Find("HealthBarRow/HealthBar_BG/HealthBar_Fill");
            Transform stFillT = vitals.Find("StaminaBarRow/StaminaBar_BG/StaminaBar_Fill");
            Transform dialFillT = vitals.Find("MicroDialsRack/Dial_0/LinearBG/LinearFill");
            Assert.IsNotNull(hpFillT, "Barra linear de Vida deve existir.");
            Assert.IsNotNull(stFillT, "Barra linear de Estamina deve existir.");
            Assert.IsNotNull(dialFillT, "MicroDial deve incluir barra linear de progresso.");
            Assert.IsNotNull(hpFillT.GetComponent<Image>()?.sprite, "HealthBar_Fill precisa de sprite para Image.Type.Filled funcionar.");
            Assert.IsNotNull(stFillT.GetComponent<Image>()?.sprite, "StaminaBar_Fill precisa de sprite para Image.Type.Filled funcionar.");
            Assert.IsNotNull(dialFillT.GetComponent<Image>()?.sprite, "LinearFill do MicroDial precisa de sprite para Image.Type.Filled funcionar.");
            Assert.AreEqual(Image.Type.Filled, hpFillT.GetComponent<Image>().type);

            Assert.IsNotNull(canvasObj.transform.Find("HUD_SurvivalElements/StatusIcon_Poison"), "Badge de veneno deve existir.");

            Transform pinnedHud = canvasObj.transform.Find("HUD_SurvivalElements/PinnedBlueprintHUD");
            Assert.IsNotNull(pinnedHud, "Rastreador de projeto fixado deve existir no HUD.");
            Assert.IsFalse(pinnedHud.gameObject.activeSelf, "PinnedBlueprintHUD inicia oculto quando nenhum projeto esta fixado.");

            hud.SetPinnedBlueprint(1, "Cabana de Toras", 10, 6);
            Assert.IsTrue(pinnedHud.gameObject.activeSelf, "SetPinnedBlueprint deve ativar o rastreador no HUD.");

            hud.ClearPinnedBlueprint();
            Assert.IsFalse(pinnedHud.gameObject.activeSelf, "ClearPinnedBlueprint deve ocultar o rastreador no HUD.");
        }

        [Test]
        public void BlueprintPlacementManager_SurvivalBook_FilterReadyAndPinToHUD_Work()
        {
            GameObject canvasObj = Track(new GameObject("TestBookHudCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject host = Track(new GameObject("TestBookHost"));
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            Assert.IsNotNull(inv);
            Assert.IsNotNull(hud);
            BlueprintPlacementManager bpMgr = InvokeLifecycle(host.AddComponent<BlueprintPlacementManager>(), "Awake");

            bpMgr.ToggleBuildMenuOrPlacement();
            Assert.IsTrue(bpMgr.IsBookOpen, "Livro de construcao deve abrir ao alternar o menu.");

            Transform bookModal = canvasObj.transform.Find("SurvivalConstructionBookModal");
            Assert.IsNotNull(bookModal);
            Assert.IsNotNull(bookModal.Find("LeatherBookCover/RightPage_Parchment/DraftingGridOverlay"));
            Assert.IsNotNull(bookModal.Find("LeatherBookCover/RightPage_Parchment/BookmarkTab_1"));
            Assert.IsNotNull(bookModal.Find("LeatherBookCover/RightPage_Parchment/RightFilterToolbar/Btn_FilterReady"));

            bpMgr.ToggleFilterOnlyReadyBlueprints();
            Assert.IsTrue(bpMgr.FilterOnlyReadyBlueprints, "Filtro 'SO PRONTOS' deve alternar estado.");

            bpMgr.TogglePinBlueprint(0);
            Assert.AreEqual(0, bpMgr.PinnedProjectIndex, "Projeto 0 deve ficar fixado no HUD.");
            Transform pinnedHud = canvasObj.transform.Find("HUD_SurvivalElements/PinnedBlueprintHUD");
            Assert.IsNotNull(pinnedHud);
            Assert.IsTrue(pinnedHud.gameObject.activeSelf, "Fixar projeto no Livro deve exibir o rastreador no HUD.");

            bpMgr.TogglePinBlueprint(0);
            Assert.AreEqual(-1, bpMgr.PinnedProjectIndex);
            Assert.IsFalse(pinnedHud.gameObject.activeSelf);
            bpMgr.CloseSurvivalBook();
        }

        [Test]
        public void MainMenuManager_UpgradesLegacyCanvasToAAAControlsSettingsAndCoopPanels()
        {
            float prevTimeScale = Time.timeScale;
            float prevVol = AudioListener.volume;
            int prevFps = Application.targetFrameRate;

            try
            {
                GameObject camObj = Track(new GameObject("TestMenuCamera"));
                Camera cam = camObj.AddComponent<Camera>();

                GameObject menuCanvas = Track(new GameObject("MainMenu_Canvas"));
                menuCanvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Track(new GameObject("Menu_Background")).transform.SetParent(menuCanvas.transform, false);
                Track(new GameObject("Main_Panel")).transform.SetParent(menuCanvas.transform, false);
                Track(new GameObject("NewGame_Panel")).transform.SetParent(menuCanvas.transform, false);
                Track(new GameObject("Settings_Panel")).transform.SetParent(menuCanvas.transform, false);

                MainMenuManager mmm = menuCanvas.AddComponent<MainMenuManager>();
                mmm.mainCamera = cam;
                InvokeLifecycle(mmm, "Start");

                Assert.IsNotNull(mmm.mainPanel);
                Assert.IsNotNull(mmm.newGamePanel);
                Assert.IsNotNull(mmm.settingsPanel);
                Assert.IsNotNull(mmm.coopPanel, "Menu AAA deve criar painel de Sala Co-op.");
                Assert.IsNotNull(mmm.masterVolumeSlider);
                Assert.IsNotNull(mmm.masterVolumeSlider.fillRect, "Slider de volume deve possuir FillRect configurado.");
                Assert.IsNotNull(mmm.masterVolumeSlider.handleRect, "Slider de volume deve possuir HandleRect configurado.");

                Assert.IsNotNull(menuCanvas.transform.Find("Main_Panel/HeroHeader"));
                Assert.IsNotNull(menuCanvas.transform.Find("Main_Panel/NavRail/Btn_Continue"));
                Assert.IsNotNull(menuCanvas.transform.Find("Main_Panel/NavRail/Btn_OpenCoop"));
                Assert.IsNotNull(menuCanvas.transform.Find("Main_Panel/BriefingCard"));

                Assert.IsTrue(mmm.mainPanel.activeSelf);
                Assert.IsFalse(mmm.coopPanel.activeSelf);

                mmm.SwitchPanel(mmm.coopPanel);
                Assert.IsTrue(mmm.coopPanel.activeSelf);
                Assert.IsFalse(mmm.mainPanel.activeSelf);
                Assert.IsNotNull(menuCanvas.transform.Find("Coop_Panel/HostIp_Input"), "Painel Co-op deve ter campo para IP do amigo.");
                Assert.IsNotNull(menuCanvas.transform.Find("Coop_Panel/Btn_FindLanRoom"), "Painel Co-op deve ter botao BUSCAR LAN.");
                Assert.IsNotNull(menuCanvas.transform.Find("Coop_Panel/Btn_JoinByIp"), "Painel Co-op deve ter botao ENTRAR (IP).");

                mmm.SwitchPanel(mmm.settingsPanel);
                Assert.IsTrue(mmm.settingsPanel.activeSelf);
                Assert.IsNotNull(menuCanvas.transform.Find("Settings_Panel/Volume_Slider/Fill Area/Fill"));
            }
            finally
            {
                Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
                AudioListener.volume = prevVol;
                Application.targetFrameRate = prevFps;
            }
        }

        [Test]
        public void FragaLanCoopTransport_PacketProcessingDiscoveryAndAvatarSync_WorkWithoutSockets()
        {
            FragaCoopSession coop = FragaCoopSession.GetOrCreate();
            Track(coop.gameObject);
            FragaLanCoopTransport transport = FragaLanCoopTransport.GetOrCreate();

            GameObject host = Track(new GameObject("TestLanBuildHost"));
            BlueprintPlacementManager bpMgr = InvokeLifecycle(host.AddComponent<BlueprintPlacementManager>(), "Awake");
            bpMgr.ClearAllPlacedStructures();

            try
            {
                Assert.IsFalse(string.IsNullOrEmpty(FragaLanCoopTransport.GetLocalIPv4Address()));

                Assert.IsTrue(FragaLanCoopTransport.TryParseHostAndPort("192.168.1.25:7788", 7777, out string parsedHost, out int parsedPort));
                Assert.AreEqual("192.168.1.25", parsedHost);
                Assert.AreEqual(7788, parsedPort);
                Assert.IsTrue(FragaLanCoopTransport.TryParseHostAndPort("10.0.0.5", 7777, out parsedHost, out parsedPort));
                Assert.AreEqual("10.0.0.5", parsedHost);
                Assert.AreEqual(7777, parsedPort);

                transport.ProcessDiscoveryBeacon("192.168.1.44|FRAGA_LAN|FRAGA-8888|AmigoHost|7777|2");
                Assert.AreEqual(1, transport.DiscoveredRooms.Count);
                Assert.AreEqual("192.168.1.44", transport.DiscoveredRooms[0].hostIp);
                Assert.AreEqual("AmigoHost", transport.DiscoveredRooms[0].hostName);

                CoopMemberState remoteState = new CoopMemberState
                {
                    playerId = "P2-REMOTE-LAN",
                    displayName = "Amigo LAN",
                    colorHex = "#66FF99",
                    health = 77f,
                    maxHealth = 100f,
                    stamina = 80f,
                    worldPosition = new Vector3(120f, 3f, 130f),
                    isConnected = true
                };
                Assert.IsTrue(transport.ProcessIncomingPacket("STATE|" + JsonUtility.ToJson(remoteState)));
                Assert.AreEqual(2, coop.Members.Count);
                Assert.IsNotNull(GameObject.Find("CoopRemoteAvatar_P2-REMOTE-LAN"), "Avatar 3D remoto deve ser criado ao receber STATE.");

                CoopBuildSyncEvent buildEvt = new CoopBuildSyncEvent
                {
                    structureNetId = "NET-STR-901",
                    ownerPlayerId = "P2-REMOTE-LAN",
                    ownerName = "Amigo LAN",
                    structureName = "Bau de Base",
                    actionType = "Place",
                    position = new Vector3(124f, 3f, 132f),
                    rotationY = 45f,
                    neededLogs = 2,
                    neededStones = 1,
                    isSharedWithGroup = true
                };
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(buildEvt)));
                Assert.AreEqual(1, WorldBlueprintSite.AllSites.Count, "Obra remota deve aparecer no mundo ao receber pacote BUILD.");

                // Replay do mesmo BUILD nao deve duplicar obra
                transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(buildEvt));
                Assert.AreEqual(1, WorldBlueprintSite.AllSites.Count);
            }
            finally
            {
                coop.SetSoloMode();
                transport.StopAll();
                bpMgr.ClearAllPlacedStructures();
            }
        }

        [Test]
        public void PlayerStatus_EffectiveMaxStamina_And_ComfortRegen_WorkWithoutFlicker()
        {
            GameObject playerObj = Track(new GameObject("TestComfortPlayer"));
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");

            Assert.AreEqual(100f, status.EffectiveMaxStamina, 0.01f);

            status.bodyTemperature = BodyTemperatureSystem.TIER2_FATIGUE_TEMP;
            Assert.AreEqual(60f, status.EffectiveMaxStamina, 0.01f);
            status.stamina = 95f;
            InvokeLifecycle(status, "UpdateStamina");
            Assert.LessOrEqual(status.stamina, 60f, "Hipotermia Tier 2 deve limitar stamina no EffectiveMaxStamina.");

            status.isSick = true;
            Assert.AreEqual(45f, status.EffectiveMaxStamina, 0.01f);

            status.bodyTemperature = 37f;
            status.isSick = false;
            status.health = 70f;
            status.hunger = 90f;
            status.thirst = 90f;
            InvokeLifecycle(status, "UpdateHungerAndThirst");
            Assert.IsTrue(status.IsComfortRegenActive, "Jogador nutrido, hidratado, aquecido e sem traumas deve ativar regeneracao de conforto.");
        }

        [Test]
        public void InventoryAdvanced_PreventsWastingConsumableWhenVitalsFull_And_ComputesEncumbrance()
        {
            ItemDatabase.Initialize();
            GameObject playerObj = Track(new GameObject("TestWasteGuardPlayer"));
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            inv.playerStatus = status;

            inv.AddItem(200, 1); // Carne Assada
            int meatSlot = inv.FindFirstSlotIndex(200);
            Assert.GreaterOrEqual(meatSlot, 0);

            // Com todos os atributos cheios, nao deve desperdiçar comida
            Assert.IsFalse(inv.UseItem(meatSlot), "Nao deve consumir comida quando atributos estao no maximo.");
            Assert.AreEqual(1, inv.GetItemCount(200));

            status.hunger = 55f;
            Assert.IsTrue(inv.UseItem(meatSlot), "Deve consumir comida quando houver necessidade real de fome.");
            Assert.AreEqual(0, inv.GetItemCount(200));
            Assert.AreEqual(95f, status.hunger, 0.01f);

            inv.AddItem(100, 56); // 56 * 0.65kg = 36.4kg (Pesado >= 32kg)
            Assert.IsTrue(inv.IsHeavyLoad);
            Assert.IsFalse(inv.IsOverEncumbered);
            Assert.AreEqual("PESADO", inv.GetEncumbranceLabel());
            Assert.Less(inv.GetMoveSpeedMultiplier(), 1f);

            inv.AddItem(101, 20); // +13kg = 49.4kg (Sobrecarga >= 45kg)
            Assert.IsTrue(inv.IsOverEncumbered);
            Assert.AreEqual("SOBRECARGA", inv.GetEncumbranceLabel());
        }

        [Test]
        public void CraftingSystem_GetMaxCraftableCount_And_CanteenRefillGuard_WorkProperly()
        {
            ItemDatabase.Initialize();
            GameObject playerObj = Track(new GameObject("TestCraftBatchPlayer"));
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            CraftingSystem craft = InvokeLifecycle(playerObj.AddComponent<CraftingSystem>(), "Awake");
            craft.inventory = inv;

            int ropeRecipeIdx = craft.GetAllRecipes().FindIndex(r => r.outputItemId == 118);
            int refillRecipeIdx = craft.GetAllRecipes().FindIndex(r => r.outputItemId == 210 && r.ingredientIds.Length > 0 && r.ingredientIds[0] == 210);
            Assert.GreaterOrEqual(ropeRecipeIdx, 0);
            Assert.GreaterOrEqual(refillRecipeIdx, 0);

            inv.AddItem(103, 6); // 2 ervas por corda => 3 lotes
            Assert.AreEqual(3, craft.GetMaxCraftableCount(ropeRecipeIdx));

            inv.AddItem(210, 1); // Cantil cheio (3/3)
            inv.AddItem(109, 1); // Agua Limpa
            Assert.IsFalse(craft.CanCraft(refillRecipeIdx, out string reason));
            Assert.IsTrue(reason.Contains("cheio"));
            Assert.AreEqual(0, craft.GetMaxCraftableCount(refillRecipeIdx));

            int canteenSlot = inv.FindFirstSlotIndex(210);
            inv.slots[canteenSlot].durability = 1f;
            Assert.IsTrue(craft.CanCraft(refillRecipeIdx));
            Assert.IsTrue(craft.Craft(refillRecipeIdx, 1));
            Assert.AreEqual(3f, inv.slots[canteenSlot].durability, 0.01f);
            Assert.AreEqual(0, inv.GetItemCount(109));
        }

        [Test]
        public void PlayerMeleeCombatDefense_ShieldBlockDegradesOffHandAndParryPreservesDurability()
        {
            ItemDatabase.Initialize();
            GameObject playerObj = Track(new GameObject("TestShieldDefensePlayer"));
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            PlayerMeleeCombatDefense defense = InvokeLifecycle(playerObj.AddComponent<PlayerMeleeCombatDefense>(), "Awake");
            defense.playerStatus = status;
            typeof(PlayerMeleeCombatDefense).GetField("inventoryCache", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(defense, inv);

            inv.AddItem(309, 1); // Escudo de Madeira (durabilidade 80)
            inv.EquipItem(309);
            int shieldSlot = inv.FindUsableEquipmentSlot(309);
            Assert.AreEqual(80f, inv.slots[shieldSlot].durability, 0.01f);

            defense.StartBlock();
            float parryDmg = defense.ProcessIncomingDamage(20f);
            Assert.AreEqual(0f, parryDmg, 0.01f);
            Assert.AreEqual(80f, inv.slots[shieldSlot].durability, 0.01f, "Parry perfeito nao deve desgastar o escudo.");

            // Simula bloqueio fora da janela de parry
            typeof(PlayerMeleeCombatDefense).GetField("blockStartTime", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(defense, Time.time - 0.5f);
            float blockDmg = defense.ProcessIncomingDamage(20f);
            Assert.AreEqual(3f, blockDmg, 0.01f);
            Assert.AreEqual(79f, inv.slots[shieldSlot].durability, 0.01f, "Bloqueio normal com escudo deve gastar 1 de durabilidade.");
        }

        [Test]
        public void CraftingSystem_TimedQueue_AdvancesDeliversAndRefunds100PercentOnCancel()
        {
            ItemDatabase.Initialize();
            GameObject playerObj = Track(new GameObject("TestCraftQueuePlayer"));
            playerObj.transform.position = Vector3.zero;
            PlayerController pc = InvokeLifecycle(playerObj.AddComponent<PlayerController>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            CraftingSystem craft = InvokeLifecycle(playerObj.AddComponent<CraftingSystem>(), "Awake");
            craft.inventory = inv;
            Assert.IsNotNull(pc);

            int ropeRecipeIdx = craft.GetAllRecipes().FindIndex(r => r.outputItemId == 118);
            int stoneArrowRecipeIdx = craft.GetAllRecipes().FindIndex(r => r.outputItemId == 110);
            Assert.GreaterOrEqual(ropeRecipeIdx, 0);
            Assert.GreaterOrEqual(stoneArrowRecipeIdx, 0);

            // 6 Ervas (103) -> 2 lotes de Corda (2 ervas cada = 4 reservadas, sobram 2)
            int initialHerbs = inv.GetItemCount(103);
            inv.AddItem(103, 6);
            Assert.IsTrue(craft.EnqueueCraft(ropeRecipeIdx, 2, out string err), $"EnqueueCraft falhou: {err}");
            Assert.AreEqual(2, craft.CraftQueue.Count);
            Assert.AreEqual(initialHerbs + 2, inv.GetItemCount(103), "Ingredientes da fila devem ser reservados imediatamente.");

            // Avanca 0.95s: 1a Corda (0.9s) conclui e entrega x2 Corda (118)
            int initialRope = inv.GetItemCount(118);
            craft.AdvanceCraftQueue(0.95f);
            Assert.AreEqual(1, craft.CraftQueue.Count, "Primeiro item da fila deve ser entregue apos craftDuration.");
            Assert.AreEqual(initialRope + 2, inv.GetItemCount(118));

            // Cancela o segundo lote restante e verifica reembolso 100% (2 ervas devolvidas)
            Assert.IsTrue(craft.CancelQueuedJob(0, silent: true));
            Assert.AreEqual(0, craft.CraftQueue.Count);
            Assert.AreEqual(initialHerbs + 4, inv.GetItemCount(103), "Cancelamento deve reembolsar 100% dos ingredientes.");

            // Testa pausa automatica de receita de Bancada ao se afastar
            GameObject wbObj = Track(new GameObject("TestWorkbenchMarker"));
            wbObj.transform.position = Vector3.zero;
            wbObj.AddComponent<CraftingStationMarker>().stationType = CraftingStationType.Workbench;

            int initialWood = inv.GetItemCount(100);
            int initialStone = inv.GetItemCount(101);
            inv.AddItem(100, 4);
            inv.AddItem(101, 4);
            Assert.IsTrue(craft.EnqueueCraft(stoneArrowRecipeIdx, 1, out string wbErr), $"Enqueue Workbench falhou: {wbErr}");

            playerObj.transform.position = new Vector3(40f, 0f, 40f);
            craft.AdvanceCraftQueue(0.5f);
            Assert.IsTrue(craft.IsCurrentJobPausedByStation, "Receita de Bancada deve pausar se o jogador se afastar.");
            Assert.AreEqual(1, craft.CraftQueue.Count);

            Assert.AreEqual(1, craft.CancelAllQueuedJobs(silent: true));
            Assert.AreEqual(initialWood + 4, inv.GetItemCount(100), "Limpar fila deve devolver toda a madeira.");
            Assert.AreEqual(initialStone + 4, inv.GetItemCount(101), "Limpar fila deve devolver toda a pedra.");
        }

        [Test]
        public void BonfireEntity_MultiSlotGrill_CooksCollectsAndPreservesProgressWhenExtinguished()
        {
            ItemDatabase.Initialize();
            GameObject playerObj = Track(new GameObject("TestBonfireGrillPlayer"));
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            inv.AddItem(104, 1); // Carne Crua (8.5s, queima)
            inv.AddItem(111, 1); // Peixe Cru (7.5s, queima)
            inv.AddItem(123, 1); // Agua Turva (5.5s, nao queima)

            GameObject fireObj = Track(new GameObject("TestMultiSlotBonfire"));
            BonfireEntity bf = InvokeLifecycle(fireObj.AddComponent<BonfireEntity>(), "Awake");
            Assert.IsTrue(bf.isLit);
            Assert.AreEqual(3, bf.EmptySlotCount);

            Assert.IsTrue(bf.TryPlaceCookItemFromInventory(inv, 104, out _));
            Assert.IsTrue(bf.TryPlaceCookItemFromInventory(inv, 111, out _));
            Assert.IsTrue(bf.TryPlaceCookItemFromInventory(inv, 123, out _));
            Assert.AreEqual(0, bf.EmptySlotCount);
            Assert.AreEqual(3, bf.CookingSlotCount);

            // Avanca 5.6s: Agua (5.5s) fica pronta; Carne e Peixe ainda assando
            bf.AdvanceSimulation(5.6f, isRaining: false, allowOvercookBurn: true);
            Assert.AreEqual(BonfireEntity.CookStage.Cooking, bf.GetSlotState(0).stage);
            Assert.AreEqual(BonfireEntity.CookStage.Cooking, bf.GetSlotState(1).stage);
            Assert.AreEqual(BonfireEntity.CookStage.Ready, bf.GetSlotState(2).stage);

            // Apagar o fogo preserva progresso parcial da grelha
            float meatTimerBeforeExtinguish = bf.GetSlotState(0).timer;
            bf.Extinguish();
            Assert.IsFalse(bf.isLit);
            Assert.AreEqual(meatTimerBeforeExtinguish, bf.GetSlotState(0).timer, 0.01f);

            // Reacende e termina cozimento de carne/peixe, depois testa overcook (Agua nunca queima)
            bf.Ignite();
            bf.AdvanceSimulation(3.5f, isRaining: false, allowOvercookBurn: true);
            Assert.AreEqual(3, bf.ReadySlotCount);

            bf.AdvanceSimulation(19.0f, isRaining: false, allowOvercookBurn: true);
            Assert.AreEqual(BonfireEntity.CookStage.Burnt, bf.GetSlotState(0).stage, "Carne esquecida no fogo deve virar Carvao (122).");
            Assert.AreEqual(122, bf.GetSlotState(0).resultItemId);
            Assert.AreEqual(BonfireEntity.CookStage.Burnt, bf.GetSlotState(1).stage, "Peixe esquecido no fogo deve virar Carvao (122).");
            Assert.AreEqual(BonfireEntity.CookStage.Ready, bf.GetSlotState(2).stage, "Agua fervida nao pode virar Carvao.");
            Assert.AreEqual(109, bf.GetSlotState(2).resultItemId);

            int collected = bf.TryCollectReadyItems(inv, out _);
            Assert.AreEqual(3, collected);
            Assert.AreEqual(3, bf.EmptySlotCount);
            Assert.GreaterOrEqual(inv.GetItemCount(122), 2);
            Assert.GreaterOrEqual(inv.GetItemCount(109), 1);
        }

        [Test]
        public void SaveSystem_And_HUDCards_PersistBonfireAndExposeQueueAndGrillControls()
        {
            ItemDatabase.Initialize();
            GameObject canvasObj = Track(new GameObject("TestPillar1HudCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject playerObj = Track(new GameObject("TestPillar1Player"));
            playerObj.transform.position = new Vector3(135f, 3f, 135f);
            DayNightCycle dnc = InvokeLifecycle(playerObj.AddComponent<DayNightCycle>(), "Awake");
            SanitySystem sanity = InvokeLifecycle(playerObj.AddComponent<SanitySystem>(), "Awake");
            PlayerStatus status = InvokeLifecycle(playerObj.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            CraftingSystem craft = InvokeLifecycle(playerObj.AddComponent<CraftingSystem>(), "Awake");
            craft.inventory = inv;
            InteractionDetector detector = InvokeLifecycle(playerObj.AddComponent<InteractionDetector>(), "Awake");

            SurvivalInventoryCraftUI ui = canvasObj.AddComponent<SurvivalInventoryCraftUI>();
            ui.inventory = inv;
            ui.playerStatus = status;
            ui.craftingSystem = craft;
            InvokeLifecycle(ui, "Awake");

            SaveSystem saveSys = InvokeLifecycle(playerObj.AddComponent<SaveSystem>(), "Awake");
            saveSys.playerTransform = playerObj.transform;
            saveSys.dayNightCycle = dnc;
            saveSys.sanitySystem = sanity;
            saveSys.playerStatus = status;
            saveSys.inventory = inv;

            string testSavePath = Path.Combine(Path.GetTempPath(), $"fraga_bonfire_save_{System.Guid.NewGuid():N}.dat");
            tempFiles.Add(testSavePath);
            saveSys.SetSavePathForTesting(testSavePath);

            GameObject fireObj = Track(new GameObject("TestPersistBonfire"));
            fireObj.transform.position = new Vector3(136f, 3f, 135f);
            BonfireEntity bf = InvokeLifecycle(fireObj.AddComponent<BonfireEntity>(), "Awake");
            inv.AddItem(104, 1);
            Assert.IsTrue(bf.TryPlaceCookItemFromInventory(inv, 104, out _));
            bf.AdvanceSimulation(4.0f, isRaining: false, allowOvercookBurn: true);
            float expectedFuel = bf.fuelLevel;

            // Salva, corrompe/reseta fogueira em memoria, carrega e valida restauracao exata
            saveSys.SaveGame();
            bf.ImportState(12f, false, null, null, null, null);
            Assert.AreEqual(3, bf.EmptySlotCount);

            saveSys.LoadGame();
            Assert.IsTrue(bf.isLit, "LoadGame deve restaurar estado aceso da fogueira.");
            Assert.AreEqual(expectedFuel, bf.fuelLevel, 0.05f, "LoadGame deve restaurar combustivel da fogueira.");
            Assert.AreEqual(104, bf.GetSlotState(0).rawItemId);
            Assert.AreEqual(4.0f, bf.GetSlotState(0).timer, 0.05f);

            // Verifica cartao diegetico de fogueira proxima e botoes de cancelamento da fila
            InvokeLifecycle(detector, "RefreshNearbyBonfire");
            InvokeLifecycle(hud, "UpdateBonfireCard");
            Transform bfCard = canvasObj.transform.Find("HUD_SurvivalElements/BonfireProximityCard");
            Assert.IsNotNull(bfCard);
            Assert.IsTrue(bfCard.gameObject.activeSelf, "BonfireProximityCard deve ficar visivel perto da fogueira.");
            Assert.IsNotNull(bfCard.Find("Btn_BonfirePrimary"));
            Assert.IsNotNull(bfCard.Find("Btn_BonfireAddWood"));
            Assert.IsNotNull(bfCard.Find("Btn_BonfireRecipes"));

            Transform cancelOneBtn = canvasObj.transform.Find("SurvivalInventory_Root/Window_Mat/Tab_Crafting/CraftProgressBG/Btn_CancelCurrentCraft");
            Transform cancelAllBtn = canvasObj.transform.Find("SurvivalInventory_Root/Window_Mat/Tab_Crafting/CraftProgressBG/Btn_CancelAllCraft");
            Assert.IsNotNull(cancelOneBtn);
            Assert.IsNotNull(cancelAllBtn);

            int ropeIdx = craft.GetAllRecipes().FindIndex(r => r.outputItemId == 118);
            inv.AddItem(103, 4);
            Assert.IsTrue(craft.EnqueueCraft(ropeIdx, 2, out _));
            InvokeLifecycle(hud, "UpdateCraftQueueHUD");
            Transform queueHud = canvasObj.transform.Find("HUD_SurvivalElements/CraftQueueHUDCard");
            Assert.IsNotNull(queueHud);
            Assert.IsTrue(queueHud.gameObject.activeSelf, "CraftQueueHUDCard deve aparecer enquanto ha fila ativa com mochila fechada.");
            Assert.AreEqual(2, craft.CancelAllQueuedJobs(silent: true));
            InvokeLifecycle(hud, "UpdateCraftQueueHUD");
            Assert.IsFalse(queueHud.gameObject.activeSelf, "CraftQueueHUDCard deve ocultar apos limpar a fila.");
        }

        [Test]
        public void InteractionDetector_3DHighlight_UsesPropertyBlockWithoutLeakingMaterialsAndIgnoresChunks()
        {
            GameObject camObj = Track(new GameObject("TestPillar2Cam"));
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(0f, 1.7f, 0f);
            camObj.transform.rotation = Quaternion.identity;

            GameObject playerObj = Track(new GameObject("TestPillar2Player"));
            playerObj.transform.position = new Vector3(0f, 0f, 0f);
            InteractionDetector detector = InvokeLifecycle(playerObj.AddComponent<InteractionDetector>(), "Awake");
            InvokeLifecycle(detector, "Start");

            GameObject chunkObj = Track(new GameObject("Chunk_0_0"));
            BoxCollider chunkSelfCol = chunkObj.AddComponent<BoxCollider>();
            chunkSelfCol.size = new Vector3(0.2f, 0.2f, 0.2f);
            chunkSelfCol.center = new Vector3(0f, -10f, 0f);
            Assert.IsNull(InteractionDetector.ResolveSafeHighlightRoot(chunkSelfCol), "Nunca deve destacar um Chunk diretamente.");

            GameObject propObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Track(propObj);
            propObj.name = "TestSupplyCrate";
            propObj.transform.SetParent(chunkObj.transform, false);
            propObj.transform.position = new Vector3(0f, 1.7f, 2.2f);
            LootContainer crate = propObj.AddComponent<LootContainer>();
            crate.containerName = "Caixa de Teste";
            crate.possibleItems = new List<int> { 100 };

            MeshRenderer rend = propObj.GetComponent<MeshRenderer>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material sharedMat = new Material(lit);
            sharedMat.color = new Color(0.4f, 0.3f, 0.2f, 1f);
            rend.sharedMaterial = sharedMat;
            Color originalMatColor = sharedMat.color;

            Physics.SyncTransforms();
            detector.ForceScanContextNow();

            Assert.IsTrue(detector.HasValidTarget);
            Assert.AreEqual(propObj, detector.CurrentHighlightedObject, "Deve destacar apenas o prop filho e nao o Chunk pai.");
            Assert.AreSame(sharedMat, rend.sharedMaterial, "Highlight 3D nao pode instanciar nem trocar sharedMaterial.");
            Assert.IsTrue(rend.HasPropertyBlock(), "Highlight 3D deve aplicar MaterialPropertyBlock no alvo.");
            Assert.AreEqual(originalMatColor, sharedMat.color, "Highlight 3D nao pode mutar o material compartilhado.");

            // Olha para longe: destaque deve limpar completamente o PropertyBlock
            camObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Physics.SyncTransforms();
            detector.ForceScanContextNow();

            Assert.IsNull(detector.CurrentHighlightedObject);
            Assert.IsFalse(rend.HasPropertyBlock(), "Ao perder o foco do alvo, PropertyBlock deve ser restaurado/limpo.");
            Object.DestroyImmediate(sharedMat);
        }

        [Test]
        public void InteractionDetector_RadialHold_SkinsCarcassArmsTrapAndCancelsCleanlyOnEarlyRelease()
        {
            ItemDatabase.Initialize();
            GameObject camObj = Track(new GameObject("TestPillar2HoldCam"));
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(0f, 1.7f, 0f);
            camObj.transform.rotation = Quaternion.identity;

            GameObject routerObj = Track(new GameObject("TestPillar2Router"));
            FragaInputRouter router = InvokeLifecycle(routerObj.AddComponent<FragaInputRouter>(), "Awake");
            InvokeLifecycle(router, "Start");

            GameObject playerObj = Track(new GameObject("TestPillar2HoldPlayer"));
            playerObj.transform.position = Vector3.zero;
            InventoryAdvanced inv = InvokeLifecycle(playerObj.AddComponent<InventoryAdvanced>(), "Awake");
            InteractionDetector detector = InvokeLifecycle(playerObj.AddComponent<InteractionDetector>(), "Awake");
            InvokeLifecycle(detector, "Start");

            // 1. Carcaca sem faca (1.25s) -> equipa Faca de Caca (302) -> cai para 0.80s
            GameObject carcassObj = Track(new GameObject("TestDeerCarcass"));
            carcassObj.transform.position = new Vector3(0f, 1.7f, 2.0f);
            carcassObj.AddComponent<BoxCollider>().size = new Vector3(1.2f, 1.0f, 1.2f);
            SkinningCarcass carcass = carcassObj.AddComponent<SkinningCarcass>();
            carcass.animalDisplayName = "Cervo";

            Physics.SyncTransforms();
            detector.ForceScanContextNow();
            Assert.IsTrue(detector.IsHoldTarget);
            Assert.AreEqual(1.25f, detector.HoldDuration, 0.01f);

            inv.AddItem(302, 1);
            inv.EquipItem(302);
            detector.ForceScanContextNow();
            Assert.AreEqual(0.80f, detector.HoldDuration, 0.01f);

            // Hold parcial (0.40s) + soltura antecipada -> cancela sem esfolar
            router.TriggerMobileInteractDown();
            InvokeLifecycle(router, "Update");
            detector.AdvanceHoldInteraction(router.InteractPressedThisFrame, router.InteractHeld, router.InteractUpThisFrame, 0.40f);
            Assert.IsTrue(detector.IsHoldInProgress);
            Assert.AreEqual(0.50f, detector.HoldProgress01, 0.02f);
            Assert.IsFalse(carcass.IsDepleted);

            router.TriggerMobileInteractUp();
            InvokeLifecycle(router, "Update");
            detector.AdvanceHoldInteraction(router.InteractPressedThisFrame, router.InteractHeld, router.InteractUpThisFrame, 0.016f);
            Assert.IsFalse(detector.IsHoldInProgress);
            Assert.AreEqual(0f, detector.HoldProgress01, 0.001f);
            Assert.IsFalse(carcass.IsDepleted);

            // Hold completo (0.85s) -> esfola com bonus da faca
            router.TriggerMobileInteractDown();
            InvokeLifecycle(router, "Update");
            detector.AdvanceHoldInteraction(router.InteractPressedThisFrame, router.InteractHeld, router.InteractUpThisFrame, 0.85f);
            Assert.IsTrue(carcass.IsDepleted);
            Assert.GreaterOrEqual(inv.GetItemCount(104), 4);
            Assert.GreaterOrEqual(inv.GetItemCount(105), 3);

            // 2. Armadilha disparada -> hold radial arma novamente
            GameObject trapObj = Track(new GameObject("TestBearTrap"));
            trapObj.transform.position = new Vector3(0f, 1.7f, 2.0f);
            trapObj.AddComponent<BoxCollider>().size = new Vector3(1.0f, 0.6f, 1.0f);
            TrapInteractable trap = trapObj.AddComponent<TrapInteractable>();
            trap.Configure(5, new Vector2Int(2, 2), armed: false);
            Assert.IsFalse(trap.isArmed);

            Physics.SyncTransforms();
            detector.ForceScanContextNow();
            Assert.IsTrue(detector.IsHoldTarget);
            router.TriggerMobileInteractUp();
            InvokeLifecycle(router, "Update");
            detector.AdvanceHoldInteraction(router.InteractPressedThisFrame, router.InteractHeld, router.InteractUpThisFrame, 0.016f);

            router.TriggerMobileInteractDown();
            InvokeLifecycle(router, "Update");
            detector.AdvanceHoldInteraction(router.InteractPressedThisFrame, router.InteractHeld, router.InteractUpThisFrame, 0.80f);
            Assert.IsTrue(trap.isArmed, "Segurar [E]/Toque na armadilha desarmada deve rearma-la.");
            Assert.IsFalse(trap.isSprung);
        }

        [Test]
        public void HUDSurvivalOverlay_RadialHoldRingAndImpactFeedback_RespondToHoldAndHits()
        {
            GameObject canvasObj = Track(new GameObject("TestPillar2HudCanvas"));
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            HUDSurvivalOverlay hud = InvokeLifecycle(canvasObj.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject routerObj = Track(new GameObject("TestPillar2HudRouter"));
            FragaInputRouter router = InvokeLifecycle(routerObj.AddComponent<FragaInputRouter>(), "Awake");
            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Desktop);

            hud.SetInteractionPrompt(true, "Esfolar Cervo [E]", FragaProIconLibrary.ControlIcon.AttackKnife, requiresHold: true);
            Text badgeTxt = canvasObj.transform.Find("HUD_SurvivalElements/ContextualInteractionPrompt/KeyBadge/KeyText")?.GetComponent<Text>();
            Assert.IsNotNull(badgeTxt);
            Assert.AreEqual("SEGURE E", badgeTxt.text);

            router.SetControlLayoutMode(FragaInputRouter.ControlLayoutMode.Mobile);
            hud.SetInteractionPrompt(true, "Esfolar Cervo [E]", FragaProIconLibrary.ControlIcon.AttackKnife, requiresHold: true);
            Assert.AreEqual("SEGURE", badgeTxt.text);

            Image holdRing = canvasObj.transform.Find("HUD_SurvivalElements/CrosshairHoldRing")?.GetComponent<Image>();
            Assert.IsNotNull(holdRing);
            Assert.AreEqual(Image.FillMethod.Radial360, holdRing.fillMethod);
            Assert.AreEqual(0f, holdRing.fillAmount, 0.001f);

            GameObject vmObj = Track(new GameObject("TestPillar2Viewmodel"));
            FirstPersonViewmodelController vm = InvokeLifecycle(vmObj.AddComponent<FirstPersonViewmodelController>(), "Awake");
            vm.TriggerImpactBounce(1.25f);
            Assert.Greater(vm.RecoilOffset, 0.03f);
            Assert.Greater(vm.ImpactBounceRemaining, 0f);

            hud.TriggerHitMarker(isCrit: true, isKill: false);
            CanvasGroup hmCg = canvasObj.transform.Find("HUD_SurvivalElements/Crosshair_HitMarker")?.GetComponent<CanvasGroup>();
            Assert.IsNotNull(hmCg);
            Assert.AreEqual(1f, hmCg.alpha, 0.01f);
        }

        [Test]
        public void InventoryAdvanced_SplitMergeHotbarAndDragDrop_PreservesPerishabilityAndBindings()
        {
            ItemDatabase.Initialize();
            GameObject host = Track(new GameObject("TestPillar3InvHost"));
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");

            // Limpa slots e adiciona 2 pilhas pereciveis de Carne Crua (104) com validades diferentes
            for (int i = 0; i < inv.maxSlots; i++) inv.slots[i].Clear();
            inv.slots[0].itemId = 104;
            inv.slots[0].quantity = 2;
            inv.slots[0].durability = 40f; // quase estragando

            inv.slots[1].itemId = 104;
            inv.slots[1].quantity = 2;
            inv.slots[1].durability = 200f; // fresca

            // Funde slot 1 -> slot 0: validade deve ser media ponderada (120s), impedindo exploit de resetar para 200s
            Assert.IsTrue(inv.MoveOrMergeSlot(1, 0, 2));
            Assert.AreEqual(4, inv.slots[0].quantity);
            Assert.IsTrue(inv.slots[1].IsEmpty());
            Assert.AreEqual(120f, inv.slots[0].durability, 0.5f);

            // Divisao customizada: separa 1 unidade para slot especifico 5
            Assert.IsTrue(inv.SplitSlot(0, 1, 5));
            Assert.AreEqual(3, inv.slots[0].quantity);
            Assert.AreEqual(104, inv.slots[5].itemId);
            Assert.AreEqual(1, inv.slots[5].quantity);
            Assert.AreEqual(120f, inv.slots[5].durability, 0.5f);

            // Hotbar: atribuir sem duplicatas + trocar atalhos + limpar
            inv.slots[2].itemId = 300; // Machado de Pedra
            inv.slots[2].quantity = 1;
            inv.slots[2].durability = 60f;

            Assert.IsTrue(inv.AssignSlotToHotbar(2, 0));
            Assert.AreEqual(300, inv.hotbarItemIds[0]);
            Assert.IsTrue(inv.AssignSlotToHotbar(2, 4)); // mover para [5] deve limpar [1]
            Assert.AreEqual(-1, inv.hotbarItemIds[0]);
            Assert.AreEqual(300, inv.hotbarItemIds[4]);

            Assert.IsTrue(inv.AssignSlotToHotbar(0, 1));
            Assert.IsTrue(inv.SwapHotbarSlots(1, 4));
            Assert.AreEqual(300, inv.hotbarItemIds[1]);
            Assert.AreEqual(104, inv.hotbarItemIds[4]);

            inv.ClearHotbarSlot(4);
            Assert.AreEqual(-1, inv.hotbarItemIds[4]);
        }

        [Test]
        public void SurvivalInventoryCraftUI_DragDropHotbarDockAndStackModal_WorkAcrossSlotsAndGear()
        {
            ItemDatabase.Initialize();
            GameObject hudCanvas = Track(new GameObject("TestPillar3Canvas"));
            hudCanvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            InvokeLifecycle(hudCanvas.AddComponent<HUDSurvivalOverlay>(), "Awake");

            GameObject host = Track(new GameObject("TestPillar3UiHost"));
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            for (int i = 0; i < inv.maxSlots; i++) inv.slots[i].Clear();
            for (int h = 0; h < inv.hotbarItemIds.Length; h++) inv.hotbarItemIds[h] = -1;

            inv.AddItem(100, 10); // 10 Madeiras no slot 0
            inv.AddItem(300, 1);  // Machado de Pedra no slot 1
            inv.equippedMainHandId = -1;

            SurvivalInventoryCraftUI ui = InvokeLifecycle(host.AddComponent<SurvivalInventoryCraftUI>(), "Awake");
            ui.inventory = inv;
            ui.OpenMenu();

            // 1. Drag & Drop entre slots da mochila (slot 0 -> slot 8)
            Assert.IsTrue(ui.TryExecuteSlotDrop(0, 8));
            Assert.IsTrue(inv.slots[0].IsEmpty());
            Assert.AreEqual(100, inv.slots[8].itemId);
            Assert.AreEqual(10, inv.slots[8].quantity);

            // 2. Modal de Dividir Pilha: escolhe 3 unidades de 10 e confirma divisao
            ui.OpenStackActionModal(8, defaultDiscardMode: false);
            Assert.IsTrue(ui.IsStackModalOpen);
            Assert.IsTrue(ui.ConfirmStackModalAction(3, discardMode: false));
            Assert.IsFalse(ui.IsStackModalOpen);
            Assert.AreEqual(7, inv.slots[8].quantity);
            Assert.AreEqual(3, inv.GetItemCount(100) - 7);
            Assert.AreEqual(10, inv.GetItemCount(100));

            // 3. Modal de Descarte: descarta 4 unidades da pilha no slot 8
            ui.OpenStackActionModal(8, defaultDiscardMode: true);
            Assert.IsTrue(ui.IsStackModalOpen);
            Assert.IsTrue(ui.ConfirmStackModalAction(4, discardMode: true));
            Assert.AreEqual(3, inv.slots[8].quantity);
            Assert.AreEqual(6, inv.GetItemCount(100));

            // 4. Drag para Hotbar Dock (slot 1 -> atalho [4], indice 3) e Equipar via Paperdoll
            Assert.IsTrue(ui.TryDropSlotToHotbar(1, 3));
            Assert.AreEqual(300, inv.hotbarItemIds[3]);

            Assert.IsFalse(ui.TryDropSlotToGear(1, EquipSlotType.BodyArmor), "Machado nao deve equipar no slot de Armadura.");
            Assert.IsTrue(ui.TryDropSlotToGear(1, EquipSlotType.MainHand));
            Assert.AreEqual(300, inv.equippedMainHandId);

            // 5. Drag de um item com qty=1 para Descarte remove imediatamente
            Assert.IsTrue(ui.TryDropSlotToDiscard(1, -1));
            Assert.IsTrue(inv.slots[1].IsEmpty());
            Assert.AreEqual(-1, inv.equippedMainHandId);

            ui.CloseMenu();
        }

        [Test]
        public void SaveSystem_PersistsPoisonWetnessBodyTempAndCustomHotbarLayout()
        {
            ItemDatabase.Initialize();

            GameObject host = Track(new GameObject("TestPillar3SaveHost"));
            DayNightCycle dnc = InvokeLifecycle(host.AddComponent<DayNightCycle>(), "Awake");
            SanitySystem sanity = InvokeLifecycle(host.AddComponent<SanitySystem>(), "Awake");
            PlayerStatus status = InvokeLifecycle(host.AddComponent<PlayerStatus>(), "Awake");
            BodyTemperatureSystem bts = InvokeLifecycle(host.AddComponent<BodyTemperatureSystem>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            SaveSystem saveSys = InvokeLifecycle(host.AddComponent<SaveSystem>(), "Awake");

            saveSys.playerTransform = host.transform;
            saveSys.dayNightCycle = dnc;
            saveSys.sanitySystem = sanity;
            saveSys.playerStatus = status;
            saveSys.inventory = inv;

            string testSavePath = Path.Combine(Path.GetTempPath(), $"fraga_p3_save_{System.Guid.NewGuid():N}.dat");
            tempFiles.Add(testSavePath);
            saveSys.SetSavePathForTesting(testSavePath);

            // Estado clinico e layout customizado da hotbar
            status.ApplyPoison(42.5f);
            bts.RestoreSavedThermalState(33.8f, 0.78f);
            status.bodyTemperature = 33.8f;

            inv.AddItem(300, 1); // Machado de Pedra
            for (int h = 0; h < inv.hotbarItemIds.Length; h++) inv.hotbarItemIds[h] = -1;
            inv.AssignHotbarSlot(5, 300); // Usuario colocou Machado apenas no atalho [6] e deixou [1..5] vazios
            inv.AssignHotbarSlot(2, 202); // Bagas no atalho [3]
            inv.activeHotbarIndex = 5;

            saveSys.SaveGame();

            // Zera estado em memoria antes de carregar
            status.isPoisoned = false;
            status.poisonTimer = 0f;
            bts.RestoreSavedThermalState(37.0f, 0.0f);
            status.bodyTemperature = 37.0f;
            inv.ResetToStartingKit();

            saveSys.LoadGame();

            Assert.IsTrue(status.isPoisoned, "Envenenamento ativo deve ser restaurado pelo SaveSystem.");
            Assert.AreEqual(42.5f, status.poisonTimer, 0.05f);
            Assert.AreEqual(33.8f, bts.currentTemperature, 0.05f);
            Assert.AreEqual(33.8f, status.bodyTemperature, 0.05f);
            Assert.AreEqual(0.78f, bts.clothesWetness, 0.01f);
            Assert.IsTrue(status.isFreezing);

            // Hotbar customizada deve permanecer exata (sem auto-preencher slots vazios que o jogador limpou)
            Assert.AreEqual(-1, inv.hotbarItemIds[0]);
            Assert.AreEqual(-1, inv.hotbarItemIds[1]);
            Assert.AreEqual(202, inv.hotbarItemIds[2]);
            Assert.AreEqual(-1, inv.hotbarItemIds[3]);
            Assert.AreEqual(-1, inv.hotbarItemIds[4]);
            Assert.AreEqual(300, inv.hotbarItemIds[5]);
            Assert.AreEqual(5, inv.activeHotbarIndex);
        }

        [Test]
        public void AudioManager_FoleyBank_ResolvesDistinctClipsUsesSpatialPoolAndMapsItemPickups()
        {
            GameObject host = Track(new GameObject("TestFase4AudioManager"));
            AudioManager am = InvokeLifecycle(host.AddComponent<AudioManager>(), "Awake");

            Assert.GreaterOrEqual(am.PooledSfxCount, 12, "Pool 3D Zero-GC deve ter pelo menos 12 fontes espaciais.");

            // Valida todos os tipos de FoleyEventType sem falhas nem clipes nulos
            System.Array allTypes = System.Enum.GetValues(typeof(AudioManager.FoleyEventType));
            foreach (AudioManager.FoleyEventType foleyType in allTypes)
            {
                AudioClip clip = am.GetFoleyClip(foleyType);
                Assert.IsNotNull(clip, $"Foley '{foleyType}' deve sintetizar ou carregar um AudioClip valido.");
                Assert.Greater(clip.samples, 256, $"Foley '{foleyType}' precisa ter amostras validas.");
            }

            // Valida mapeamento de coleta por item
            Assert.AreEqual(AudioManager.FoleyEventType.PickupWood, AudioManager.ResolvePickupFoleyForItem(100));
            Assert.AreEqual(AudioManager.FoleyEventType.PickupStone, AudioManager.ResolvePickupFoleyForItem(101));
            Assert.AreEqual(AudioManager.FoleyEventType.PickupHerb, AudioManager.ResolvePickupFoleyForItem(103));
            Assert.AreEqual(AudioManager.FoleyEventType.PickupWater, AudioManager.ResolvePickupFoleyForItem(109));
            Assert.AreEqual(AudioManager.FoleyEventType.PickupWater, AudioManager.ResolvePickupFoleyForItem(210));

            // Valida disparo espacial 3D usando o pool sem erro
            Vector3 targetPos = new Vector3(14f, 3f, 22f);
            AudioClip played = am.PlayFoley(AudioManager.FoleyEventType.ChopWood, targetPos, 0.85f);
            Assert.IsNotNull(played);
            Transform firstPooled = am.transform.Find("PooledSFX_0");
            Assert.IsNotNull(firstPooled, "Fonte espacial PooledSFX_0 deve existir.");
            Assert.AreEqual(targetPos, firstPooled.position, "Fonte espacial do pool deve ser posicionada no local do evento 3D.");
        }

        [Test]
        public void GalicianFootstepSurfaceDetector_And_SurfaceAudio_DetectAllSurfacesAndLandingImpact()
        {
            GameObject detectorGo = Track(new GameObject("TestFase4FootstepDetector"));
            GalicianFootstepSurfaceDetector detector = InvokeLifecycle(detectorGo.AddComponent<GalicianFootstepSurfaceDetector>(), "Awake");
            detector.EnsureSurfaceBanks();

            Assert.IsNotNull(detector.footstepAudioSource, "footstepAudioSource deve ser garantido automaticamente.");
            Assert.AreEqual(3, detector.humusClips.Length);
            Assert.AreEqual(3, detector.mudClips.Length);
            Assert.AreEqual(3, detector.graniteClips.Length);
            Assert.AreEqual(3, detector.mossClips.Length);
            Assert.AreEqual(3, detector.woodClips.Length);
            Assert.AreEqual(3, detector.waterClips.Length);

            // Piso de madeira sob os pes -> detecta Wood
            GameObject woodDeck = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            woodDeck.name = "Cabin_Wood_Deck";
            woodDeck.transform.position = new Vector3(500f, 100f, 500f);
            woodDeck.transform.localScale = new Vector3(6f, 0.5f, 6f);
            Physics.SyncTransforms();

            detector.PlayFootstep(new Vector3(500f, 100.3f, 500f), isSprinting: true, isCrouching: false);
            Assert.AreEqual(GalicianFootstepSurfaceDetector.SurfaceMaterialType.Wood, detector.LastDetectedSurface);
            Assert.AreEqual(0.92f, detector.LastFootstepVolume, 0.01f);

            // Piso de granito -> detecta Granite e impacto de queda escala volume
            GameObject stoneFloor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            stoneFloor.name = "Ruins_Granite_Floor";
            stoneFloor.transform.position = new Vector3(520f, 100f, 500f);
            stoneFloor.transform.localScale = new Vector3(6f, 0.5f, 6f);
            Physics.SyncTransforms();

            detector.PlayLandingImpact(new Vector3(520f, 100.3f, 500f), 11.2f);
            Assert.AreEqual(GalicianFootstepSurfaceDetector.SurfaceMaterialType.Granite, detector.LastDetectedSurface);
            Assert.AreEqual(11.2f / 14f, detector.LastLandingVolume, 0.02f);

            // SurfaceAudio legado nao duplica passos quando PlayerController + Detector existem
            GameObject playerHost = Track(new GameObject("TestFase4PlayerAudioHost"));
            PlayerController pc = playerHost.AddComponent<PlayerController>();
            SurfaceAudio sa = InvokeLifecycle(playerHost.AddComponent<SurfaceAudio>(), "Awake");
            sa.playerTransform = playerHost.transform;
            sa.playerController = pc;
            sa.PlaySurfaceStep();
            Assert.AreEqual(3, detector.waterClips.Length);
        }

        [Test]
        public void FirstPersonViewmodelController_AnimationsAndConsumeProps_DriveStatesInEditMode()
        {
            GameObject camGo = Track(new GameObject("TestFase4MainCam"));
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            Assert.IsNotNull(cam);

            GameObject playerGo = Track(new GameObject("TestFase4ViewmodelHost"));
            AudioManager am = InvokeLifecycle(playerGo.AddComponent<AudioManager>(), "Awake");
            Assert.IsNotNull(am);
            PlayerStatus status = InvokeLifecycle(playerGo.AddComponent<PlayerStatus>(), "Awake");
            Assert.IsNotNull(status);
            PlayerController pc = playerGo.AddComponent<PlayerController>();
            FirstPersonViewmodelController vm = InvokeLifecycle(playerGo.AddComponent<FirstPersonViewmodelController>(), "Awake");
            InvokeLifecycle(vm, "Start");

            Transform root = camGo.transform.Find("Fraga_Viewmodel_Root");
            Assert.IsNotNull(root, "Rig dual-arm deve ser construido na camera principal.");
            Assert.IsNotNull(root.Find("RightArmPivot/RightHandSocket/VM_Knife"), "Modelo 3D da Faca deve existir.");
            Assert.IsNotNull(root.Find("LeftArmPivot/LeftHandSocket/VM_Lighter"), "Modelo 3D do Isqueiro deve existir.");

            // Troca de arma aciona transicao de saque (Equip Draw)
            vm.OnEquipChanged(312, -1);
            Assert.IsTrue(vm.IsEquipDrawing, "Trocar para Machado de Ferro (312) deve iniciar animacao de saque.");
            Assert.IsTrue(root.Find("RightArmPivot/RightHandSocket/VM_IronAxe").gameObject.activeSelf);

            // Inspecao de arma
            vm.TriggerInspectAnimation();
            Assert.IsTrue(vm.IsInspecting);

            // Consumivel Bandagem exibe prop 3D de bandagem
            vm.TriggerConsumeAnimation(FirstPersonViewmodelController.ConsumeAnimKind.Bandage, 208);
            Assert.IsTrue(vm.IsConsuming);
            Assert.AreEqual(FirstPersonViewmodelController.ConsumeAnimKind.Bandage, vm.ActiveConsumeKind);
            Assert.IsNotNull(vm.ActiveConsumeProp);
            Assert.IsTrue(vm.ActiveConsumeProp.activeSelf);
            Assert.AreEqual("VM_Prop_Bandage", vm.ActiveConsumeProp.name);

            // Coleta manual e Escalada
            vm.TriggerGatherAnimation();
            Assert.IsTrue(vm.IsGathering);
            vm.TriggerClimbAnimation(0.40f);
            Assert.IsTrue(vm.IsClimbing);
            Assert.IsFalse(vm.IsInspecting, "Escalar deve cancelar inspecao de arma.");

            // Isqueiro aceso sem offhand mostra VM_Lighter quando nao estiver consumindo
            typeof(FirstPersonViewmodelController)
                .GetField("consumeTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(vm, 0f);
            pc.SetLighterState(true);
            vm.RefreshLeftLighterState();
            Assert.IsTrue(vm.IsLighterVisualVisible, "Isqueiro 3D na mao esquerda deve aparecer quando [L] esta ligado sem offhand.");
        }

        [Test]
        public void InventoryAdvanced_And_BonfireEntity_TriggerFase4ConsumeFoleyAndFireAudio()
        {
            ItemDatabase.Initialize();

            GameObject camGo = Track(new GameObject("TestFase4InvCam"));
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();

            GameObject host = Track(new GameObject("TestFase4GameplayHost"));
            InvokeLifecycle(host.AddComponent<AudioManager>(), "Awake");
            PlayerStatus status = InvokeLifecycle(host.AddComponent<PlayerStatus>(), "Awake");
            InventoryAdvanced inv = InvokeLifecycle(host.AddComponent<InventoryAdvanced>(), "Awake");
            FirstPersonViewmodelController vm = InvokeLifecycle(host.AddComponent<FirstPersonViewmodelController>(), "Awake");
            InvokeLifecycle(vm, "Start");

            status.health = 70f;
            status.isBleeding = true;
            status.thirst = 60f;
            inv.playerStatus = status;

            inv.AddItem(208, 1); // Bandagem
            inv.AddItem(210, 1); // Cantil

            int bandageSlot = inv.FindFirstSlotIndex(208);
            Assert.IsTrue(inv.UseItem(bandageSlot));
            Assert.IsFalse(status.isBleeding);
            Assert.AreEqual(FirstPersonViewmodelController.ConsumeAnimKind.Bandage, vm.ActiveConsumeKind);

            int canteenSlot = inv.FindFirstSlotIndex(210);
            Assert.IsTrue(inv.UseItem(canteenSlot));
            Assert.AreEqual(FirstPersonViewmodelController.ConsumeAnimKind.Drink, vm.ActiveConsumeKind);

            // Fogueira: Acender, Cozinhar e Adicionar Lenha acionam audio/estado sem erros em EditMode
            GameObject bonfireGo = Track(new GameObject("TestFase4Bonfire"));
            BonfireEntity bf = InvokeLifecycle(bonfireGo.AddComponent<BonfireEntity>(), "Awake");
            bf.Extinguish();
            bf.Ignite();
            Assert.IsTrue(bf.isLit);

            inv.AddItem(104, 1); // Carne crua
            Assert.IsTrue(bf.TryPlaceCookItemFromInventory(inv, 104, out string cookMsg));
            Assert.IsTrue(cookMsg.Contains("grelha"));

            bf.fuelLevel = 20f;
            inv.AddItem(100, 1); // Madeira
            Assert.IsTrue(bf.TryAddWoodFuel(inv, out string fuelMsg));
            Assert.IsTrue(fuelMsg.Contains("Lenha"));
        }

        [Test]
        public void InteractionDetector_FormatPromptForDevice_AdaptsDesktopAndTouchPrompts()
        {
            string tapPrompt = "Pegar Graveto no Chao (x2) [E]";
            Assert.AreEqual(tapPrompt, InteractionDetector.FormatPromptForDevice(tapPrompt, false, false));
            Assert.AreEqual("Pegar Graveto no Chao (x2) (TOCAR)", InteractionDetector.FormatPromptForDevice(tapPrompt, true, false));
            Assert.AreEqual("Esfolar Cervo (SEGURE)", InteractionDetector.FormatPromptForDevice("Esfolar Cervo [E]", true, true));
            Assert.AreEqual("Cortar Arvore com Machado (TOCAR / ATAQUE)",
                InteractionDetector.FormatPromptForDevice("Cortar Arvore com Machado [CLIQUE DO MOUSE OU E]", true, false));
        }

        [Test]
        public void FragaLanCoopTransport_ProcessesRemoteBuildLifecycle_DepositCompleteRepairAndDestroy()
        {
            GameObject host = Track(new GameObject("TestFase5CoopHost"));
            FragaCoopSession coop = InvokeLifecycle(host.AddComponent<FragaCoopSession>(), "Awake");
            coop.localPlayerId = "P1-HOST";
            coop.localPlayerName = "HostLocal";
            coop.StartCoopHost("FRAGA-5555");

            FragaLanCoopTransport transport = InvokeLifecycle(host.AddComponent<FragaLanCoopTransport>(), "Awake");

            GameObject bpMgrGo = Track(new GameObject("TestFase5BpMgr"));
            InvokeLifecycle(bpMgrGo.AddComponent<BlueprintPlacementManager>(), "Awake");

            try
            {
                CoopBuildSyncEvent placeEvt = new CoopBuildSyncEvent
                {
                    structureNetId = "NET-CABIN-01",
                    actorPlayerId = "P2-ALLY",
                    ownerPlayerId = "P2-ALLY",
                    ownerName = "Batedor",
                    structureName = "Cabana de Toras",
                    actionType = "Place",
                    position = new Vector3(12f, 0f, 18f),
                    rotationY = 45f,
                    currentLogs = 1,
                    neededLogs = 4,
                    currentStones = 0,
                    neededStones = 2,
                    isComplete = false,
                    isSharedWithGroup = true
                };
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(placeEvt)));
                Assert.IsTrue(WorldBlueprintSite.AllSites.Exists(s => s != null && s.structureId == "NET-CABIN-01" && s.currentLogs == 1));

                CoopBuildSyncEvent depositEvt = placeEvt;
                depositEvt.actorPlayerId = "P2-ALLY";
                depositEvt.actionType = "Deposit";
                depositEvt.currentLogs = 3;
                depositEvt.currentStones = 1;
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(depositEvt)));
                WorldBlueprintSite site = WorldBlueprintSite.AllSites.Find(s => s != null && s.structureId == "NET-CABIN-01");
                Assert.IsNotNull(site);
                Assert.AreEqual(3, site.currentLogs);
                Assert.AreEqual(1, site.currentStones);

                CoopBuildSyncEvent completeEvt = depositEvt;
                completeEvt.actionType = "Complete";
                completeEvt.currentLogs = 4;
                completeEvt.currentStones = 2;
                completeEvt.isComplete = true;
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(completeEvt)));

                StructureHealth sh = StructureHealth.AllStructures.Find(s => s != null && s.structureId == "NET-CABIN-01");
                Assert.IsNotNull(sh);
                Track(sh.gameObject);

                sh.currentHealth = 100f;
                CoopBuildSyncEvent repairEvt = completeEvt;
                repairEvt.actionType = "Repair";
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(repairEvt)));
                Assert.Greater(sh.currentHealth, 100f);

                CoopBuildSyncEvent destroyEvt = completeEvt;
                destroyEvt.actionType = "Destroy";
                Assert.IsTrue(transport.ProcessIncomingPacket("BUILD|" + JsonUtility.ToJson(destroyEvt)));
                Assert.IsFalse(StructureHealth.AllStructures.Exists(s => s != null && s.structureId == "NET-CABIN-01"));
            }
            finally
            {
                var snapshot = new System.Collections.Generic.List<WorldBlueprintSite>(WorldBlueprintSite.AllSites);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i] != null && snapshot[i].structureId == "NET-CABIN-01")
                        BlueprintPlacementManager.SafeDestroy(snapshot[i].gameObject);
                }
            }
        }

        [Test]
        public void Phase5Singletons_ClearStaticInstancesOnDestroy()
        {
            GameObject go = new GameObject("TestFase5SingletonCleanup");
            AudioManager am = InvokeLifecycle(go.AddComponent<AudioManager>(), "Awake");
            GalicianFootstepSurfaceDetector fd = InvokeLifecycle(go.AddComponent<GalicianFootstepSurfaceDetector>(), "Awake");
            InteractionDetector id = InvokeLifecycle(go.AddComponent<InteractionDetector>(), "Awake");

            Assert.AreSame(am, AudioManager.Instance);
            Assert.AreSame(fd, GalicianFootstepSurfaceDetector.Instance);
            Assert.AreSame(id, InteractionDetector.Instance);

            InvokeLifecycle(am, "OnDestroy");
            InvokeLifecycle(fd, "OnDestroy");
            InvokeLifecycle(id, "OnDestroy");
            Object.DestroyImmediate(go);

            Assert.IsNull(AudioManager.Instance);
            Assert.IsNull(GalicianFootstepSurfaceDetector.Instance);
            Assert.IsNull(InteractionDetector.Instance);
        }
    }
}
