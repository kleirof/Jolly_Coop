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
	public class JollyCoopPatches
	{
		[HarmonyILManipulator, HarmonyPatch(typeof(Chest), nameof(Chest.Open))]
		public static void OpenPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdarg(1),
				x => x.MatchCallvirt<PlayerController>("TriggerItemAcquisition")
				))
			{
				crs.Emit(OpCodes.Ldarg_0);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<Chest>>
					((self) =>
					{
						if (!self.IsRainbowChest)
							JollyCoopModule.AddItem(self);
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(Chest), nameof(Chest.HandleSynergyGambleChest), MethodType.Enumerator)]
		public static void HandleSynergyGambleChestPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			FieldInfo fi = AccessTools.Field(Type.GetType("Chest+<HandleSynergyGambleChest>c__Iterator4, Assembly-CSharp"), "$this");

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdarg(0),
				x => x.MatchLdfld("Chest+<HandleSynergyGambleChest>c__Iterator4", "player"),
				x => x.MatchCallvirt<PlayerController>("TriggerItemAcquisition")
				))
			{
				crs.Emit(OpCodes.Ldarg_0);
				crs.Emit(OpCodes.Ldfld, fi);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<Chest>>
					((self) =>
					{
						JollyCoopModule.AddItem(self);
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(Chest), nameof(Chest.OnBroken))]
		public static void OnBrokenPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			for (int i = 0; i < 6; ++i)
			{
				if (crs.TryGotoNext(MoveType.Before,
					x => x.MatchLdarg(0),
					x => x.MatchLdarg(0),
					x => x.MatchCall<Chest>("PresentItem"),
					x => x.MatchCall<MonoBehaviour>("StartCoroutine"),
					x => x.MatchPop(),
					x => x.Match(OpCodes.Br)
					))
				{
					crs.Emit(OpCodes.Ldarg_0);
					crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<Chest>>
						((self) =>
						{
							JollyCoopModule.AddItem(self);
						});
				}
				crs.Index += 6;
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.HandleRoomClearReward))]
		public static void HandleRoomClearRewardPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.Before,
				x => x.MatchLdloc(4),
				x => x.MatchLdloc(5),
				x => x.Match(OpCodes.Ble_Un)
				))
			{
				crs.Index += 2;
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.roomItemDropIncStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
							return orig * 1.5f;
						else
							return orig;
					});
			}
		}

		private static bool[] playerOneSpawnMasterFlags = new bool[4];
		private static bool[] playerTwoSpawnMasterFlags = new bool[4];
		internal static bool playerOneHasTakenDamageInThisRoom = false;
		internal static bool playerTwoHasTakenDamageInThisRoom = false;
		internal static bool playerOneHasGivenMasteryToken = false;
		internal static bool playerTwoHasGivenMasteryToken = false;

		[HarmonyILManipulator, HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.HandleBossClearReward))]
		public static void HandleBossClearRewardPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdloc(12)
				))
			{
				crs.EmitDelegate<Func<bool, bool>>
					(orig =>
					{
						return true;
					});
			}
			crs.Index = 0;

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdsfld<IntVector2>("Left"),
				x => x.MatchCall<IntVector2>("op_Addition"),
				x => x.MatchStloc(2)
				))
			{
				crs.Emit(OpCodes.Ldloc_2);
				crs.Emit(OpCodes.Ldloc, 12);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<IntVector2, bool, IntVector2>>
					((orig, flag2) =>
					{
						orig -= IntVector2.Left;

						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.normalBossRewardDoubledStr) 
						&& GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
							orig += IntVector2.Left;

						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.masterDoubledStr)
						&& GameManager.Instance.Dungeon.BossMasteryTokenItemId >= 0/* && !GameManager.Instance.Dungeon.HasGivenMasteryToken*/
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

						return orig;
					});
				crs.Emit(OpCodes.Stloc_2);
			}
			crs.Index = 0;

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdsfld<IntVector2>("One"),
				x => x.MatchCall<IntVector2>("op_Addition"),
				x => x.MatchCallvirt<DungeonData>("get_Item"),
				x => x.MatchLdcI4(1),
				x => x.MatchStfld<CellData>("isOccupied")
				))
			{
				crs.Emit(OpCodes.Ldarg_0);
				crs.Emit(OpCodes.Ldloc_2);
				crs.Emit(OpCodes.Ldloc_S, (byte)10);
				crs.Emit(OpCodes.Ldloc_S, (byte)13);
				crs.EmitDelegate<Func<RoomHandler, IntVector2, RewardPedestal, RewardPedestal, IntVector2>>
					((self, intVector, component, rewardPedestal) =>
					{
						if (!JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr))
							return intVector;

						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.normalBossRewardDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
						{
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

							if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
							{
								JollyCoopModule.playerTwoExclusivePedestals.Add(rewardPedestal);
								JollyCoopModule.playerOneExclusivePedestals.Add(rewardPedestal2);
							}
						}

						return intVector;
					});
				crs.Emit(OpCodes.Stloc_2);
			}

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCallvirt<PlayerController>("ResurrectFromBossKill"),
				x => x.MatchLdloc(12)
				))
			{
				crs.Emit(OpCodes.Ldarg_0);
				crs.Emit(OpCodes.Ldloc_2);
				crs.Emit(OpCodes.Ldloc_S, (byte)10);
				crs.EmitDelegate<Func<bool, RoomHandler, IntVector2, RewardPedestal, bool>>
					((orig, self, intVector, component) =>
					{
						if (GameManager.Instance.CurrentGameType == GameManager.GameType.SINGLE_PLAYER)
							return orig;

						if (!JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) || !JollyCoopModule.gunfig.Enabled(JollyCoopModule.masterDoubledStr))
							return orig;

						if (playerTwoSpawnMasterFlags[0])
						{
							playerTwoSpawnMasterFlags[0] = false;

							Dungeon dungeon = GameManager.Instance.Dungeon;
							if (!playerOneHasTakenDamageInThisRoom)
								intVector += new IntVector2(4, 0);
							else
							{
								GameStatsManager.Instance.RegisterStatChange(TrackedStats.MASTERY_TOKENS_RECEIVED, 1f);
								GameManager.Instance.PrimaryPlayer.MasteryTokensCollectedThisRun++;
								dungeon.HasGivenMasteryToken = true;
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
								intVector -= new IntVector2(2, 0);

							if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
							{
								JollyCoopModule.playerOneExclusivePedestals.Add(rewardPedestal4);
							}
						}
						return playerOneSpawnMasterFlags[0];
					});
			}

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdloc(18),
				x => x.MatchLdnull(),
				x => x.MatchStfld<RewardPedestal>("MimicGuid")
				))
			{
				crs.Emit(OpCodes.Ldloc_S, (byte)18);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<RewardPedestal>>
					(rewardPedestal3 =>
					{
						if (!JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr))
							return;

						playerOneSpawnMasterFlags[0] = false;

						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
						{
							JollyCoopModule.playerTwoExclusivePedestals.Add(rewardPedestal3);
						}
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.SpawnBehavior_CR), MethodType.Enumerator)]
		public static void SpawnBehavior_CRPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<BraveBehaviour>("get_sprite"),
				x => x.MatchCallvirt<tk2dBaseSprite>("get_WorldCenter"),
				x => x.MatchLdcR4(-3f)
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						orig = -4f;
						int addCount = 0;
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
						{
							if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.normalBossRewardDoubledStr))
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
					});
			}

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<BraveBehaviour>("get_sprite"),
				x => x.MatchCallvirt<tk2dBaseSprite>("get_WorldCenter"),
				x => x.MatchLdcR4(2f)
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						orig = 1f;
						int addCount = 0;
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
						{
							if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.normalBossRewardDoubledStr))
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
					});
			}

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<BraveBehaviour>("get_sprite"),
				x => x.MatchCallvirt<tk2dBaseSprite>("get_WorldCenter"),
				x => x.MatchLdcR4(0f)
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						int addCount = 0;
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
						{
							if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.normalBossRewardDoubledStr))
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
					});
			}

			if (crs.TryGotoNext(MoveType.Before,
				x => x.MatchCall<RewardPedestal>("DetermineContents")
				))
			{
				FieldInfo fi = AccessTools.Field(Type.GetType("RewardPedestal+<SpawnBehavior_CR>c__Iterator0, Assembly-CSharp"), "$this");

				crs.Emit(OpCodes.Ldarg_0);
				crs.Emit(OpCodes.Ldfld, fi);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<PlayerController, RewardPedestal, PlayerController>>
					((orig, self) =>
					{
						if (JollyCoopModule.playerOneExclusivePedestals.Contains(self))
							return GameManager.Instance.SecondaryPlayer;
						return orig;
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(Dungeon), nameof(Dungeon.Regenerate), MethodType.Enumerator)]
		public static void RegeneratePatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			MethodInfo mi = AccessTools.Method(typeof(Vector3?), "get_Value");

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCallvirt<GameManager>("get_RewardManager"),
				x => x.MatchLdfld<RewardManager>("A_Chest"),
				x => x.MatchLdloca(10),
				x => x.MatchCall(mi)
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<Vector3, Vector3>>
					(orig =>
					{
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.rainbowDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
							return orig - new Vector3(4f, 0f, 0f);
						else
							return orig;
					});
			}

			FieldInfo fi = AccessTools.Field(Type.GetType("Dungeonator.Dungeon+<Regenerate>c__Iterator1, Assembly-CSharp"), "$this");

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchLdloc(15),
				x => x.MatchCallvirt<Chest>("BecomeRainbowChest")
				))
			{
				crs.Emit(OpCodes.Ldarg_0);
				crs.Emit(OpCodes.Ldfld, fi);
				crs.Emit(OpCodes.Ldloc, 10);
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<Dungeon, Vector3?>>
					((self, vector) =>
					{
						if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.rainbowDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
						{
							Vector3? vector2 = vector + new Vector3(4f, 0f, 0f);
							self.StartCoroutine(JollyCoopModule.SpawnChest(vector2.Value, self.data.Entrance));
						}
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(AIActor), "BaseLevelHealthModifier", MethodType.Getter)]
		public static void get_BaseLevelHealthModifierPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<GameManager>("get_Instance"),
				x => x.MatchLdfld<GameManager>("COOP_ENEMY_HEALTH_MULTIPLIER")
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						return JollyCoopModule.EnemyHealth;
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(HealthHaver), nameof(HealthHaver.Start))]
		public static void StartPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<GameManager>("get_Instance"),
				x => x.MatchLdfld<GameManager>("COOP_ENEMY_HEALTH_MULTIPLIER")
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						return JollyCoopModule.EnemyHealth;
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateEnemyBulletSpeedMultiplier))]
		public static void UpdateEnemyBulletSpeedMultiplierPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchCall<GameManager>("get_Instance"),
				x => x.MatchLdfld<GameManager>("COOP_ENEMY_PROJECTILE_SPEED_MULTIPLIER")
				))
			{
				crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
					(orig =>
					{
						return JollyCoopModule.EnemyProjectileSpeed;
					});
			}
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(LootEngine), nameof(LootEngine.SpewLoot), new Type[] { typeof(List<GameObject>), typeof(Vector3) })]
		public static void SpewLootPatch(ILContext ctx)
		{
			ILCursor crs = new ILCursor(ctx);

			if (crs.TryGotoNext(MoveType.After,
				x => x.MatchStloc(5)
				))
			{
				crs.Emit(OpCodes.Ldloc_3);
				crs.Emit(OpCodes.Ldloc_S, (byte)5);
				crs.Emit(OpCodes.Ldarg_0);
				crs.EmitDelegate<Action<int, GameObject, List<GameObject>>>
					((index, gameObject, list) =>
					{
						if (!JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) || !JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
							return;

						if (JollyCoopModule.playerOneExclusiveLoots.Contains(list[index].gameObject.GetComponent<PickupObject>()))
						{
							JollyCoopModule.playerOneExclusiveLoots.Remove(list[index].gameObject.GetComponent<PickupObject>());
							JollyCoopModule.playerOneExclusivePickups.Add(gameObject.GetComponent<PickupObject>(), 
								GameManager.Instance.SecondaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(gameObject.transform.position.IntXY(VectorConversions.Round)));
						}
						if (JollyCoopModule.playerTwoExclusiveLoots.Contains(list[index].gameObject.GetComponent<PickupObject>()))
						{
							JollyCoopModule.playerTwoExclusiveLoots.Remove(list[index].gameObject.GetComponent<PickupObject>());
							JollyCoopModule.playerTwoExclusivePickups.Add(gameObject.GetComponent<PickupObject>(),
								GameManager.Instance.PrimaryPlayer.CurrentRoom == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(gameObject.transform.position.IntXY(VectorConversions.Round)));
						}
					});
			}
		}

		[HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.IsLoadingLevel), MethodType.Setter)]
		public static void Set_IsLoadingLevel_Pre(bool value)
		{
			if(value)
            {
				JollyCoopModule.playerOneExclusivePickups.Clear();
				JollyCoopModule.playerTwoExclusivePickups.Clear();
				JollyCoopModule.playerOneExclusiveLoots.Clear();
				JollyCoopModule.playerTwoExclusiveLoots.Clear();
				JollyCoopModule.playerOneExclusivePedestals.Clear();
				JollyCoopModule.playerTwoExclusivePedestals.Clear();
				playerOneHasGivenMasteryToken = false;
				playerTwoHasGivenMasteryToken = false;
				playerOneHasTakenDamageInThisRoom = false;
				playerTwoHasTakenDamageInThisRoom = false;
				Array.Clear(playerOneSpawnMasterFlags, 0, playerOneSpawnMasterFlags.Length);
				Array.Clear(playerTwoSpawnMasterFlags, 0, playerTwoSpawnMasterFlags.Length);
            }
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.Interact))]
		[HarmonyPatch(typeof(Gun), nameof(Gun.Interact))]
		[HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.Interact))]
		public static bool PassiveItemInteractPrefix(PickupObject __instance, PlayerController interactor)
		{
			if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
			{
				if (JollyCoopModule.playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
					return false;

				if (JollyCoopModule.playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
					return false;
			}

			if (JollyCoopModule.playerTwoExclusivePickups.ContainsKey(__instance))
				JollyCoopModule.playerTwoExclusivePickups.Remove(__instance);

			if (JollyCoopModule.playerOneExclusivePickups.ContainsKey(__instance))
				JollyCoopModule.playerOneExclusivePickups.Remove(__instance);

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.GetRidOfMinimapIcon))]
		[HarmonyPatch(typeof(Gun), nameof(Gun.GetRidOfMinimapIcon))]
		[HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.GetRidOfMinimapIcon))]
		public static void PassiveItemGetRidOfMinimapIcon_Pre(PickupObject __instance)
		{
			if (JollyCoopModule.playerTwoExclusivePickups.ContainsKey(__instance))
				JollyCoopModule.playerTwoExclusivePickups.Remove(__instance);

			if (JollyCoopModule.playerOneExclusivePickups.ContainsKey(__instance))
				JollyCoopModule.playerOneExclusivePickups.Remove(__instance);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(PassiveItem), nameof(PassiveItem.OnEnteredRange))]
		[HarmonyPatch(typeof(Gun), nameof(Gun.OnEnteredRange))]
		[HarmonyPatch(typeof(PlayerItem), nameof(PlayerItem.OnEnteredRange))]
		public static bool PassiveItemOnEnteredRange_Pre(PickupObject __instance, PlayerController interactor)
		{
			if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
			{
				if (JollyCoopModule.playerTwoExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
					return false;

				if (JollyCoopModule.playerOneExclusivePickups.ContainsKey(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
					return false;
			}

			return true;
		}

		[HarmonyPostfix, HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.PlayerEnter))]
		public static void PlayerEnter_Post(RoomHandler __instance, PlayerController playerEntering)
		{
			if (playerEntering == GameManager.Instance.PrimaryPlayer)
			{
				playerOneHasTakenDamageInThisRoom = false;
				foreach (PickupObject pickupObject in JollyCoopModule.playerTwoExclusivePickups.Keys)
				{
					if (!JollyCoopModule.playerTwoExclusivePickups[pickupObject] && __instance == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(pickupObject.transform.position.IntXY(VectorConversions.Round)))
						JollyCoopModule.playerTwoExclusivePickups[pickupObject] = true;
				}
			}
			else
			{
				playerTwoHasTakenDamageInThisRoom = false;
				foreach (PickupObject pickupObject in JollyCoopModule.playerOneExclusivePickups.Keys)
				{
					if (!JollyCoopModule.playerOneExclusivePickups[pickupObject] && __instance == GameManager.Instance.Dungeon.data.GetAbsoluteRoomFromPosition(pickupObject.transform.position.IntXY(VectorConversions.Round)))
						JollyCoopModule.playerOneExclusivePickups[pickupObject] = true;
				}
			}
		}

		[HarmonyPrefix, HarmonyPatch(typeof(PickupObject), nameof(PickupObject.ShouldBeTakenByRat))]
		public static bool ShouldBeTakenByRat_Pre(PickupObject __instance, ref bool __result)
		{
			bool hasEntered;
			if (JollyCoopModule.playerTwoExclusivePickups.TryGetValue(__instance, out hasEntered) 
				|| JollyCoopModule.playerOneExclusivePickups.TryGetValue(__instance, out hasEntered))
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

		[HarmonyPrefix, HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Damaged))]
		public static void Damaged_Pre(PlayerController __instance)
		{
			if (__instance == GameManager.Instance.PrimaryPlayer)
				playerOneHasTakenDamageInThisRoom = true;
			else
				playerTwoHasTakenDamageInThisRoom = true;
		}

		[HarmonyPrefix, HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.Interact))]
		public static bool RewardPedestalInteractPrefix(RewardPedestal __instance, PlayerController player)
		{
			if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
			{
				if (JollyCoopModule.playerTwoExclusivePedestals.Contains(__instance) && player == GameManager.Instance.SecondaryPlayer)
					return false;

				if (JollyCoopModule.playerOneExclusivePedestals.Contains(__instance) && player == GameManager.Instance.PrimaryPlayer)
					return false;
			}

			if (JollyCoopModule.playerTwoExclusivePedestals.Contains(__instance))
				JollyCoopModule.playerTwoExclusivePedestals.Remove(__instance);

			if (JollyCoopModule.playerOneExclusivePedestals.Contains(__instance))
				JollyCoopModule.playerOneExclusivePedestals.Remove(__instance);

			return true;
		}

		[HarmonyPrefix, HarmonyPatch(typeof(RewardPedestal), nameof(RewardPedestal.OnEnteredRange))]
		public static bool RewardPedestalOnEnteredRangePrefix(RewardPedestal __instance, PlayerController interactor)
		{
			if (JollyCoopModule.gunfig.Enabled(JollyCoopModule.jollyCoopIsOnStr) && JollyCoopModule.gunfig.Enabled(JollyCoopModule.itemDistribLockStr))
			{
				if (JollyCoopModule.playerTwoExclusivePedestals.Contains(__instance) && interactor == GameManager.Instance.SecondaryPlayer)
					return false;

				if (JollyCoopModule.playerOneExclusivePedestals.Contains(__instance) && interactor == GameManager.Instance.PrimaryPlayer)
					return false;
			}

			return true;
		}
	}
}
