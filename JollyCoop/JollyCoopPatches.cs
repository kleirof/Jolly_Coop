using System;
using UnityEngine;
using System.Reflection;
using Dungeonator;
using HarmonyLib;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace JollyCoop
{
    public static class JollyCoopPatches
    {
        private static bool[] playerOneSpawnMasterFlags = new bool[4];
        private static bool[] playerTwoSpawnMasterFlags = new bool[4];
        internal static bool playerOneHasTakenDamageInThisRoom = false;
        internal static bool playerTwoHasTakenDamageInThisRoom = false;
        internal static bool playerOneHasGivenMasteryToken = false;
        internal static bool playerTwoHasGivenMasteryToken = false;

        internal static Dictionary<PickupObject, bool> playerOneExclusivePickups = new Dictionary<PickupObject, bool>();
        internal static Dictionary<PickupObject, bool> playerTwoExclusivePickups = new Dictionary<PickupObject, bool>();

        internal static HashSet<RewardPedestal> playerOneExclusivePedestals = new HashSet<RewardPedestal>();
        internal static HashSet<RewardPedestal> playerTwoExclusivePedestals = new HashSet<RewardPedestal>();

        internal static HashSet<Chest> playerOneExclusiveChests = new HashSet<Chest>();
        internal static HashSet<Chest> playerTwoExclusiveChests = new HashSet<Chest>();

        public static Color playerOneOutlineColor = OutlineColorManager.defaultOutlineColor;
        public static Color playerTwoOutlineColor = OutlineColorManager.defaultOutlineColor;

        internal static Dictionary<Chest, int> chestOriginalCount = new Dictionary<Chest, int>();

        public static void EmitCall<T>(this ILCursor iLCursor, string methodName, Type[] parameters = null, Type[] generics = null)
        {
            MethodInfo methodInfo = AccessTools.Method(typeof(T), methodName, parameters, generics);
            iLCursor.Emit(OpCodes.Call, methodInfo);
        }

        public static T GetFieldInEnumerator<T>(object instance, string fieldNamePattern)
        {
            return (T)instance.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("$" + fieldNamePattern) || f.Name.Contains("<" + fieldNamePattern + ">"))
                .GetValue(instance);
        }

        public static void SetFieldInEnumerator<T>(object instance, string fieldNamePattern, T value)
        {
            instance.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("$" + fieldNamePattern) || f.Name.Contains("<" + fieldNamePattern + ">"))
                .SetValue(instance, value);
        }

        public static bool TheNthTime(this Func<bool> predict, int n = 1)
        {
            for (int i = 0; i < n; ++i)
            {
                if (!predict())
                    return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Chest), nameof(Chest.PresentItem), MethodType.Enumerator)]
        public class PresentItemPatchClass
        {
            [HarmonyILManipulator]
            public static void PresentItemPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchCallvirt<GameStatsManager>("get_IsRainbowRun")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<PresentItemPatchClass>(nameof(PresentItemPatchClass.PresentItemPatchCall));
                }
            }

            private static void PresentItemPatchCall(object stateMachine)
            {
                Chest chest = GetFieldInEnumerator<Chest>(stateMachine, "this");

                if (chest != null && !chest.IsRainbowChest)
                    AddItem(chest);
            }


            public static void AddItem(Chest c)
            {
                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr) || GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                int count = c.contents.Count;
                chestOriginalCount[c] = count;
                for (int i = 0; i < count; i++)
                {
                    PickupObject pickupObject;
                    if (c.contents[i].quality == PickupObject.ItemQuality.A || c.contents[i].quality == PickupObject.ItemQuality.B || c.contents[i].quality == PickupObject.ItemQuality.C || c.contents[i].quality == PickupObject.ItemQuality.D || c.contents[i].quality == PickupObject.ItemQuality.S)
                    {
                        RewardManager rewardManager = GameManager.Instance.RewardManager;
                        GenericLootTable lootTable = c.contents[i] is Gun ? rewardManager.GunsLootTable : rewardManager.ItemsLootTable;
                        pickupObject = rewardManager.GetItemForPlayer(GameManager.Instance.SecondaryPlayer, lootTable, c.contents[i].quality, null).GetComponent<PickupObject>();
                        c.contents.Add(pickupObject);
                    }
                    else if (!(c.contents[i] is KeyBulletPickup))
                        c.contents.Add(c.contents[i]);
                }
            }
        }

        [HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.HandleRoomClearReward))]
        public class HandleRoomClearRewardPatchClass
        {
            [HarmonyILManipulator]
            public static void HandleRoomClearRewardPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.Match(OpCodes.Ble_Un)))
                {
                    crs.EmitCall<HandleRoomClearRewardPatchClass>(nameof(HandleRoomClearRewardPatchClass.HandleRoomClearRewardPatchCall));
                }
            }

            private static float HandleRoomClearRewardPatchCall(float orig)
            {
                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.roomItemDropIncStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                    return orig * 1.5f;
                else
                    return orig;
            }
        }

        [HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.HandleBossClearReward))]
        public class HandleBossClearRewardPatchClass
        {
            [HarmonyILManipulator]
            public static void HandleBossClearRewardPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.Before,
                    x => x.Match(OpCodes.Brfalse)
                    ))).TheNthTime(7))
                {
                    crs.EmitCall<HandleBossClearRewardPatchClass>(nameof(HandleBossClearRewardPatchClass.HandleBossClearRewardPatchCall_1));
                }
                crs.Index = 0;

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchStloc(12)))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)2);
                    crs.Emit(OpCodes.Ldloc_S, (byte)11);
                    crs.Emit(OpCodes.Ldloc_S, (byte)12);
                    crs.EmitCall<HandleBossClearRewardPatchClass>(nameof(HandleBossClearRewardPatchClass.HandleBossClearRewardPatchCall_2));
                }
                crs.Index = 0;

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.After,
                    x => x.MatchStfld<CellData>("isOccupied")
                    ))).TheNthTime(3))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.Emit(OpCodes.Ldloca_S, (byte)2);
                    crs.Emit(OpCodes.Ldloc_S, (byte)10);
                    crs.Emit(OpCodes.Ldloc_S, (byte)13);
                    crs.EmitCall<HandleBossClearRewardPatchClass>(nameof(HandleBossClearRewardPatchClass.HandleBossClearRewardPatchCall_3));
                }
                crs.Index = 0;

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.Before,
                    x => x.Match(OpCodes.Brfalse)
                    ))).TheNthTime(12))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.Emit(OpCodes.Ldloca_S, (byte)2);
                    crs.Emit(OpCodes.Ldloc_S, (byte)10);
                    crs.Emit(OpCodes.Ldloc_0);
                    crs.EmitCall<HandleBossClearRewardPatchClass>(nameof(HandleBossClearRewardPatchClass.HandleBossClearRewardPatchCall_4));
                }
                crs.Index = 0;

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchStfld<RewardPedestal>("MimicGuid")))
                {
                    crs.Emit(OpCodes.Ldloc_S, (byte)18);
                    crs.EmitCall<HandleBossClearRewardPatchClass>(nameof(HandleBossClearRewardPatchClass.HandleBossClearRewardPatchCall_5));
                }
            }

            private static bool HandleBossClearRewardPatchCall_1(bool orig)
            {
                return true;
            }

            private static void HandleBossClearRewardPatchCall_2(ref IntVector2 orig, bool flag, bool flag2)
            {
                orig -= IntVector2.Left;

                if (flag && JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.normalBossRewardDoubledStr)
                    && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    orig += IntVector2.Left;
                }

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.masterIndependentStr)
                    && GameManager.Instance.Dungeon.BossMasteryTokenItemId >= 0
                    && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    if (!playerOneHasTakenDamageInThisRoom && !playerOneHasGivenMasteryToken)
                    {
                        playerOneHasGivenMasteryToken = true;
                        for (int i = 0; i < playerOneSpawnMasterFlags.Length; ++i)
                            playerOneSpawnMasterFlags[i] = true;
                        orig += IntVector2.Left;
                    }
                    if (!playerTwoHasTakenDamageInThisRoom && !playerTwoHasGivenMasteryToken)
                    {
                        playerTwoHasGivenMasteryToken = true;
                        for (int i = 0; i < playerTwoSpawnMasterFlags.Length; ++i)
                            playerTwoSpawnMasterFlags[i] = true;
                        orig += IntVector2.Left;
                    }
                }
                else if (flag2)
                {
                    orig += IntVector2.Left;
                }
            }

            private static void HandleBossClearRewardPatchCall_3(RoomHandler self, ref IntVector2 intVector, RewardPedestal component, RewardPedestal rewardPedestal)
            {
                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.normalBossRewardDoubledStr)
                    || GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                Dungeon dungeon = GameManager.Instance.Dungeon;
                intVector += new IntVector2(2, 0);
                RewardPedestal rewardPedestal2 = RewardPedestal.Spawn(component, intVector, self);
                rewardPedestal2.SpawnsTertiarySet = false;
                rewardPedestal2.IsBossRewardPedestal = true;
                rewardPedestal2.lootTable.lootTable = self.OverrideBossRewardTable;
                rewardPedestal2.RegisterChestOnMinimap(self);
                dungeon.data[intVector].isOccupied = true;
                dungeon.data[intVector + IntVector2.Right].isOccupied = true;
                dungeon.data[intVector + IntVector2.Up].isOccupied = true;
                dungeon.data[intVector + IntVector2.One].isOccupied = true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    playerTwoExclusivePedestals.Add(rewardPedestal);
                    playerOneExclusivePedestals.Add(rewardPedestal2);
                }
            }

            private static bool HandleBossClearRewardPatchCall_4(bool orig, RoomHandler self, ref IntVector2 intVector, RewardPedestal component, GlobalDungeonData.ValidTilesets tilesetId)
            {
                if (GameManager.Instance.CurrentGameType == GameManager.GameType.SINGLE_PLAYER)
                    return orig;

                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.masterIndependentStr))
                    return orig;

                if (playerTwoSpawnMasterFlags[0])
                {
                    playerTwoSpawnMasterFlags[0] = false;

                    bool isForgegeon = tilesetId == GlobalDungeonData.ValidTilesets.FORGEGEON;
                    Dungeon dungeon = GameManager.Instance.Dungeon;
                    if (!playerOneHasTakenDamageInThisRoom)
                    {
                        if (!isForgegeon)
                            intVector += new IntVector2(4, 0);
                        else
                            intVector += new IntVector2(2, 0);
                    }
                    else
                    {
                        GameStatsManager.Instance.RegisterStatChange(TrackedStats.MASTERY_TOKENS_RECEIVED, 1f);
                        GameManager.Instance.PrimaryPlayer.MasteryTokensCollectedThisRun++;
                        dungeon.HasGivenMasteryToken = true;
                        if (!isForgegeon)
                            intVector += new IntVector2(2, 0);
                    }
                    RewardPedestal rewardPedestal4 = RewardPedestal.Spawn(component, intVector, self);
                    dungeon.data[intVector].isOccupied = true;
                    dungeon.data[intVector + IntVector2.Right].isOccupied = true;
                    dungeon.data[intVector + IntVector2.Up].isOccupied = true;
                    dungeon.data[intVector + IntVector2.One].isOccupied = true;

                    rewardPedestal4.SpawnsTertiarySet = false;
                    rewardPedestal4.contents = PickupObjectDatabase.GetById(dungeon.BossMasteryTokenItemId);
                    rewardPedestal4.MimicGuid = null;

                    if (!playerOneHasTakenDamageInThisRoom)
                    {
                        if (!isForgegeon)
                            intVector -= new IntVector2(4, 0);
                        else
                            intVector -= new IntVector2(2, 0);
                    }

                    if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                    {
                        playerOneExclusivePedestals.Add(rewardPedestal4);
                    }
                }
                return playerOneSpawnMasterFlags[0];
            }

            private static void HandleBossClearRewardPatchCall_5(RewardPedestal rewardPedestal3)
            {
                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr))
                    return;

                playerOneSpawnMasterFlags[0] = false;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    playerTwoExclusivePedestals.Add(rewardPedestal3);
                }
            }
        }

        [HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.SpawnBehavior_CR), MethodType.Enumerator)]
        public class SpawnBehavior_CRPatchClass
        {
            [HarmonyILManipulator]
            public static void SpawnBehavior_CRPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdcR4(-3f)))
                {
                    crs.EmitCall<SpawnBehavior_CRPatchClass>(nameof(SpawnBehavior_CRPatchClass.SpawnBehavior_CRPatchCall_1));
                }
                crs.Index = 0;

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdcR4(2f)))
                {
                    crs.EmitCall<SpawnBehavior_CRPatchClass>(nameof(SpawnBehavior_CRPatchClass.SpawnBehavior_CRPatchCall_2));
                }
                crs.Index = 0;

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdcR4(0f)
                    ))).TheNthTime(4))
                {
                    crs.EmitCall<SpawnBehavior_CRPatchClass>(nameof(SpawnBehavior_CRPatchClass.SpawnBehavior_CRPatchCall_3));
                }
                crs.Index = 0;

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchCall<RewardPedestal>("DetermineContents")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<SpawnBehavior_CRPatchClass>(nameof(SpawnBehavior_CRPatchClass.SpawnBehavior_CRPatchCall_4));
                }
            }

            private static float SpawnBehavior_CRPatchCall_1(float orig)
            {
                orig = -4f;
                int addCount = 0;
                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.normalBossRewardDoubledStr))
                        addCount++;
                }

                if (playerOneSpawnMasterFlags[1])
                {
                    playerOneSpawnMasterFlags[1] = false;
                    addCount++;
                }
                if (playerTwoSpawnMasterFlags[1])
                {
                    playerTwoSpawnMasterFlags[1] = false;
                    addCount++;
                }

                if (addCount != 3)
                    orig += 1f;
                return orig;
            }

            private static float SpawnBehavior_CRPatchCall_2(float orig)
            {
                orig = 1f;
                int addCount = 0;
                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.normalBossRewardDoubledStr))
                        addCount++;
                }

                if (playerOneSpawnMasterFlags[2])
                {
                    playerOneSpawnMasterFlags[2] = false;
                    addCount++;
                }
                if (playerTwoSpawnMasterFlags[2])
                {
                    playerTwoSpawnMasterFlags[2] = false;

                    addCount++;
                }

                orig += addCount * 2f;
                if (addCount != 3)
                    orig += 1f;
                return orig;
            }

            private static float SpawnBehavior_CRPatchCall_3(float orig)
            {
                int addCount = 0;
                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.normalBossRewardDoubledStr))
                        addCount++;
                }

                if (playerOneSpawnMasterFlags[3])
                {
                    playerOneSpawnMasterFlags[3] = false;
                    addCount++;
                }
                if (playerTwoSpawnMasterFlags[3])
                {
                    playerTwoSpawnMasterFlags[3] = false;

                    addCount++;
                }

                orig += addCount * 1f;

                return orig;
            }

            private static PlayerController SpawnBehavior_CRPatchCall_4(PlayerController orig, object selfObject)
            {
                if (playerOneExclusivePedestals.Contains(GetFieldInEnumerator<RewardPedestal>(selfObject, "this")))
                    return GameManager.Instance.SecondaryPlayer;
                return orig;
            }
        }

        [HarmonyPatch(typeof(Dungeon), nameof(Dungeon.Regenerate), MethodType.Enumerator)]
        public class RegeneratePatchClass
        {
            [HarmonyILManipulator]
            public static void RegeneratePatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                MethodInfo methodInfo = AccessTools.Method(typeof(Vector3?), "get_Value");

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall(methodInfo)))
                {
                    crs.EmitCall<RegeneratePatchClass>(nameof(RegeneratePatchClass.RegeneratePatchCall_1));
                }
                crs.Index = 0;

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCallvirt<Chest>("BecomeRainbowChest")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.Emit(OpCodes.Ldloc, 10);
                    crs.Emit(OpCodes.Ldloc, 15);
                    crs.EmitCall<RegeneratePatchClass>(nameof(RegeneratePatchClass.RegeneratePatchCall_2));
                }
            }

            private static Vector3 RegeneratePatchCall_1(Vector3 orig)
            {

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                    return orig - new Vector3(4f, 0f, 0f);
                else
                    return orig;

            }

            private static void RegeneratePatchCall_2(object selfObject, Vector3? vector, Chest firstChest)
            {
                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                {
                    playerTwoExclusiveChests.Add(firstChest);
                    Vector3? vector2 = vector + new Vector3(4f, 0f, 0f);
                    Dungeon self = GetFieldInEnumerator<Dungeon>(selfObject, "this");
                    self.StartCoroutine(JollyCoopManager.SpawnChest(vector2.Value, self.data.Entrance));
                }
            }
        }

        [HarmonyPatch(typeof(AIActor), "BaseLevelHealthModifier", MethodType.Getter)]
        public class get_BaseLevelHealthModifierPatchClass
        {
            [HarmonyILManipulator]
            public static void get_BaseLevelHealthModifierPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<GameManager>("COOP_ENEMY_HEALTH_MULTIPLIER")))
                {
                    crs.EmitCall<get_BaseLevelHealthModifierPatchClass>(nameof(get_BaseLevelHealthModifierPatchClass.get_BaseLevelHealthModifierPatchCall));
                }
            }

            private static float get_BaseLevelHealthModifierPatchCall(float orig)
            {
                return JollyCoopManager.EnemyHealth;
            }
        }

        [HarmonyPatch(typeof(HealthHaver), nameof(HealthHaver.Start))]
        public class HealthHaverStartPatchClass
        {
            [HarmonyILManipulator]
            public static void HealthHaverStartPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<GameManager>("COOP_ENEMY_HEALTH_MULTIPLIER")))
                {
                    crs.EmitCall<HealthHaverStartPatchClass>(nameof(HealthHaverStartPatchClass.HealthHaverStartPatchCall));
                }
            }

            private static float HealthHaverStartPatchCall(float orig)
            {
                return JollyCoopManager.EnemyHealth;
            }
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateEnemyBulletSpeedMultiplier))]
        public class UpdateEnemyBulletSpeedMultiplierPatchClass
        {
            [HarmonyILManipulator]
            public static void UpdateEnemyBulletSpeedMultiplierPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<GameManager>("COOP_ENEMY_PROJECTILE_SPEED_MULTIPLIER")))
                {
                    crs.EmitCall<UpdateEnemyBulletSpeedMultiplierPatchClass>(nameof(UpdateEnemyBulletSpeedMultiplierPatchClass.UpdateEnemyBulletSpeedMultiplierPatchCall));
                }
            }

            private static float UpdateEnemyBulletSpeedMultiplierPatchCall(float orig)
            {
                return JollyCoopManager.EnemyProjectileSpeed;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsLoadingLevel), MethodType.Setter)]
        public class Set_IsLoadingLevelPatchClass
        {
            [HarmonyPrefix]
            public static void Set_IsLoadingLevelPrefix(bool value)
            {
                if (value)
                {
                    playerOneExclusivePickups.Clear();
                    playerTwoExclusivePickups.Clear();
                    playerOneExclusivePedestals.Clear();
                    playerTwoExclusivePedestals.Clear();
                    playerOneExclusiveChests.Clear();
                    playerTwoExclusiveChests.Clear();
                    chestOriginalCount.Clear();
                    playerOneHasGivenMasteryToken = false;
                    playerTwoHasGivenMasteryToken = false;
                    playerOneHasTakenDamageInThisRoom = false;
                    playerTwoHasTakenDamageInThisRoom = false;
                    Array.Clear(playerOneSpawnMasterFlags, 0, playerOneSpawnMasterFlags.Length);
                    Array.Clear(playerTwoSpawnMasterFlags, 0, playerTwoSpawnMasterFlags.Length);
                }
            }
        }

        [HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.Interact))]
        public class PassiveItemInteractPatchClass
        {
            [HarmonyPrefix]
            public static bool InteractPrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);

                return true;
            }
        }

        [HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.GetRidOfMinimapIcon))]
        public class PassiveItemGetRidOfMinimapIconPatchClass
        {
            [HarmonyPostfix]
            public static void GetRidOfMinimapIconPostfix(PickupObject __instance)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.OnEnteredRange))]
        public class PassiveItemOnEnteredRangePatchClass
        {
            [HarmonyPrefix]
            public static bool OnEnteredRangePrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Gun), nameof(Gun.Interact))]
        public class GunInteractPatchClass
        {
            [HarmonyPrefix]
            public static bool InteractPrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);

                return true;
            }
        }

        [HarmonyPatch(typeof(Gun), nameof(Gun.GetRidOfMinimapIcon))]
        public class GunGetRidOfMinimapIconPatchClass
        {
            [HarmonyPostfix]
            public static void GetRidOfMinimapIconPostfix(PickupObject __instance)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(Gun), nameof(Gun.OnEnteredRange))]
        public class GunOnEnteredRangePatchClass
        {
            [HarmonyPrefix]
            public static bool OnEnteredRangePrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.Interact))]
        public class PlayerItemInteractPatchClass
        {
            [HarmonyPrefix]
            public static bool InteractPrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.GetRidOfMinimapIcon))]
        public class PlayerItemGetRidOfMinimapIconPatchClass
        {
            [HarmonyPostfix]
            public static void GetRidOfMinimapIconPostfix(PickupObject __instance)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                playerTwoExclusivePickups.Remove(__instance);
                playerOneExclusivePickups.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.OnEnteredRange))]
        public class PlayerItemOnEnteredRangePatchClass
        {
            [HarmonyPrefix]
            public static bool OnEnteredRangePrefix(PickupObject __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.PlayerEnter))]
        public class PlayerEnterPatchClass
        {
            [HarmonyPostfix]
            public static void PlayerEnterPostfix(RoomHandler __instance, PlayerController playerEntering)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                if (playerEntering == GameManager.Instance.PrimaryPlayer)
                {
                    playerOneHasTakenDamageInThisRoom = false;
                    List<PickupObject> pickupsToUpdate = new List<PickupObject>();
                    foreach (PickupObject pickupObject in playerTwoExclusivePickups.Keys)
                    {
                        if (!playerTwoExclusivePickups[pickupObject] && __instance == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(pickupObject.transform.position.IntXY(VectorConversions.Round)))
                        {
                            pickupsToUpdate.Add(pickupObject);
                        }
                    }
                    foreach (PickupObject pickup in pickupsToUpdate)
                    {
                        playerTwoExclusivePickups[pickup] = true;
                    }
                }
                else
                {
                    playerTwoHasTakenDamageInThisRoom = false;
                    List<PickupObject> pickupsToUpdate = new List<PickupObject>();
                    foreach (PickupObject pickupObject in playerOneExclusivePickups.Keys)
                    {
                        if (!playerOneExclusivePickups[pickupObject] && __instance == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(pickupObject.transform.position.IntXY(VectorConversions.Round)))
                        {
                            pickupsToUpdate.Add(pickupObject);
                        }
                    }
                    foreach (PickupObject pickup in pickupsToUpdate)
                    {
                        playerOneExclusivePickups[pickup] = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PickupObject), nameof(PickupObject.ShouldBeTakenByRat))]
        public class ShouldBeTakenByRatClass
        {
            [HarmonyPrefix]
            public static bool ShouldBeTakenByRatPrefix(PickupObject __instance, ref bool __result)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                bool hasEntered;
                if (playerTwoExclusivePickups.TryGetValue(__instance, out hasEntered)
                    || playerOneExclusivePickups.TryGetValue(__instance, out hasEntered))
                {
                    if (!hasEntered)
                    {
                        __result = false;
                        return false;
                    }
                    return true;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Damaged))]
        public class DamagedPatchClass
        {
            [HarmonyPrefix]
            public static void DamagedPrefix(PlayerController __instance)
            {
                if (__instance == GameManager.Instance.PrimaryPlayer)
                {
                    playerOneHasTakenDamageInThisRoom = true;
                }
                else
                {
                    playerTwoHasTakenDamageInThisRoom = true;
                }
            }
        }

        [HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.Interact))]
        public class RewardPedestalInteractPatchClass
        {
            [HarmonyPrefix]
            public static bool RewardPedestalInteractPrefix(RewardPedestal __instance, PlayerController player)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePedestals.Contains(__instance) && player == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePedestals.Contains(__instance) && player == GameManager.Instance.PrimaryPlayer)
                        return false;
                }
                playerTwoExclusivePedestals.Remove(__instance);
                playerOneExclusivePedestals.Remove(__instance);

                return true;
            }
        }

        [HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.OnEnteredRange))]
        public class RewardPedestalOnEnteredRangePatchClass
        {
            [HarmonyPrefix]
            public static bool RewardPedestalOnEnteredRangePrefix(RewardPedestal __instance, PlayerController interactor)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return true;

                if (JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr) && JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr))
                {
                    if (playerTwoExclusivePedestals.Contains(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
                        return false;

                    if (playerOneExclusivePedestals.Contains(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
                        return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Chest), nameof(Chest.SpewContentsOntoGround))]
        public class SpewContentsOntoGroundPatchClass
        {
            [HarmonyPostfix]
            public static void SpewContentsOntoGroundPostfix(Chest __instance)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;
                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr))
                    return;

                playerTwoExclusiveChests.Remove(__instance);
                playerOneExclusiveChests.Remove(__instance);
                chestOriginalCount.Remove(__instance);
            }

            [HarmonyILManipulator]
            public static void HandleRoomClearRewardPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall(typeof(LootEngine), "SpewLoot")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.Emit(OpCodes.Ldloc_S, (byte)4);
                    crs.EmitCall<SpewContentsOntoGroundPatchClass>(nameof(SpewContentsOntoGroundPatchClass.SpewContentsOntoGroundPatchCall));
                }
            }

            private static List<DebrisObject> SpewContentsOntoGroundPatchCall(List<DebrisObject> orig, Chest self, int i)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return orig;
                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.itemDistribLockStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr))
                    return orig;

                if (orig.Count == 0)
                    return orig;
                DebrisObject debris = orig[0];
                if (debris == null)
                    return orig;
                PickupObject pickupObject = debris.GetComponentInChildren<PickupObject>();
                if (pickupObject == null)
                    return orig;

                if (playerTwoExclusiveChests.Contains(self))
                {
                    playerTwoExclusivePickups.Add(
                        pickupObject, 
                        GameManager.Instance.PrimaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(debris.transform.position.IntXY(VectorConversions.Round))
                    );
                }
                else if (playerOneExclusiveChests.Contains(self))
                {
                    playerOneExclusivePickups.Add(
                        pickupObject, 
                        GameManager.Instance.SecondaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(debris.transform.position.IntXY(VectorConversions.Round))
                    );
                }
                else 
                { 
                    if (!chestOriginalCount.TryGetValue(self, out var count))
                        return orig;

                    if (i < count)
                    {
                        playerTwoExclusivePickups.Add(
                            pickupObject, 
                            GameManager.Instance.PrimaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(debris.transform.position.IntXY(VectorConversions.Round))
                        );
                    }
                    else
                    {
                        playerOneExclusivePickups.Add(
                            pickupObject, 
                            GameManager.Instance.SecondaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(debris.transform.position.IntXY(VectorConversions.Round))
                        );
                    }
                }

                return orig;
            }
        }

        [HarmonyPatch(typeof(SellCellController), nameof(SellCellController.HandleSoldItem), MethodType.Enumerator)]
        public class HandleSoldItemPatchClass
        {
            [HarmonyILManipulator]
            public static void HandleSoldItemPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                MethodInfo methodInfo = AccessTools.Method(typeof(Vector3?), "get_Value");

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.After,
                    x => x.MatchCallvirt<tk2dBaseSprite>("get_WorldCenter")
                    ))).TheNthTime(2))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<HandleSoldItemPatchClass>(nameof(HandleSoldItemPatchClass.HandleSoldItemPatchCall));
                }
            }

            private static void HandleSoldItemPatchCall(object selfObject)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER)
                    return;

                if (!JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr)
                    || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.chestItemDoubledStr))
                    return;

                int sellPrice = GetFieldInEnumerator<int>(selfObject, "sellPrice");
                SetFieldInEnumerator(selfObject, "sellPrice", (int)(sellPrice * JollyCoopManager.ItemRecyclingPrice));
            }
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Update))]
        public class PlayerControllerUpdatePatchClass
        {
            [HarmonyILManipulator]
            public static void PlayerControllerUpdatePatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<GameOptions>("IncreaseSpeedOutOfCombat")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<PlayerControllerUpdatePatchClass>(nameof(PlayerControllerUpdatePatchClass.PlayerControllerUpdatePatchCall));
                }
            }

            private static bool PlayerControllerUpdatePatchCall(bool orig, PlayerController self)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr))
                    return orig;

                if (self.IsPrimaryPlayer)
                    return JollyCoopManager.gunfig.Enabled(JollyCoopManager.increasePlayerOneSpeedOutOfCombatStr);
                return JollyCoopManager.gunfig.Enabled(JollyCoopManager.increasePlayerTwoSpeedOutOfCombatStr);
            }

            [HarmonyPostfix]
            public static void PlayerControllerUpdatePostfix(PlayerController __instance)
            {
                try
                {
                    if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER && JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr))
                    {
                        if (!IsPlayerValid(__instance))
                            return;
                        Color targetColor = __instance.IsPrimaryPlayer ? playerOneOutlineColor : playerTwoOutlineColor;
                        if (__instance.outlineColor == targetColor)
                            return;
                        var sprite = __instance?.sprite;
                        if (sprite == null)
                            return;
                        SpriteOutlineManager.RemoveOutlineFromSprite(sprite, true);
                        __instance.outlineColor = targetColor;
                        SpriteOutlineManager.AddOutlineToSprite(sprite, __instance.outlineColor, 0.1f, 0f, (__instance.characterIdentity != PlayableCharacters.Eevee) ? SpriteOutlineManager.OutlineType.NORMAL : SpriteOutlineManager.OutlineType.EEVEE);
                    } 
                    else
                    {
                        if (!IsPlayerValid(__instance))
                            return;
                        Color targetColor = Color.black;
                        if (__instance.outlineColor == targetColor)
                            return;
                        var sprite = __instance?.sprite;
                        if (sprite == null)
                            return;
                        SpriteOutlineManager.RemoveOutlineFromSprite(sprite, true);
                        if (__instance.IsGhost)
                            return;
                        __instance.outlineColor = targetColor;
                        SpriteOutlineManager.AddOutlineToSprite(sprite, __instance.outlineColor, 0.1f, 0f, (__instance.characterIdentity != PlayableCharacters.Eevee) ? SpriteOutlineManager.OutlineType.NORMAL : SpriteOutlineManager.OutlineType.EEVEE);
                    }
                }
                catch { }
            }

            private static bool IsPlayerValid(PlayerController player)
            {
                if (!player || player.IsGone)
                {
                    return false;
                }
                if (!player.specRigidbody.enabled || player.specRigidbody.GetPixelCollider(ColliderType.HitBox) == null)
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(BraveInput), nameof(BraveInput.LateUpdate))]
        public class BraveInputLateUpdatePatchClass
        {
            [HarmonyILManipulator]
            public static void BraveInputLateUpdatePatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<GameOptions>("RumbleEnabled")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<BraveInputLateUpdatePatchClass>(nameof(BraveInputLateUpdatePatchClass.BraveInputLateUpdatePatchCall));
                }
            }

            private static bool BraveInputLateUpdatePatchCall(bool orig, BraveInput self)
            {
                if (GameManager.Instance.CurrentGameType != GameManager.GameType.COOP_2_PLAYER || !JollyCoopManager.gunfig.Enabled(JollyCoopManager.jollyCoopOnStr))
                    return orig;

                if (self.m_playerID == 0)
                    return JollyCoopManager.gunfig.Enabled(JollyCoopManager.playerOneVibrationStr);
                return JollyCoopManager.gunfig.Enabled(JollyCoopManager.playerTwoVibrationStr);
            }
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.HandleGunEquipInternal))]
        public class HandleGunEquipInternalPatchClass
        {
            [HarmonyILManipulator]
            public static void HandleGunEquipInternalPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld<PlayerController>("outlineColor")))
                {
                    crs.EmitCall<HandleGunEquipInternalPatchClass>(nameof(HandleGunEquipInternalPatchClass.HandleGunEquipInternalPatchCall));
                }
            }

            private static Color HandleGunEquipInternalPatchCall(Color orig)
            {
                return Color.black;
            }
        }
    }
}