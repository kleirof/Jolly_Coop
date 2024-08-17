using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;
using MonoMod.RuntimeDetour;
using System.Reflection;
using Dungeonator;
using Pathfinding;
using System.Threading;
using HarmonyLib;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace JollyCoop
{
    [BepInDependency("etgmodding.etg.mtgapi")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class JollyCoopModule : BaseUnityPlugin
    {
        public const string GUID = "kleirof.etg.jollycoop";
        public const string NAME = "Jolly Coop";
        public const string VERSION = "1.1.0";
        public const string TEXT_COLOR = "#00CED1";

		private static bool m_JollyCoopIsOn = true;
		private static bool m_ChestItemDoubled = true;
		private static bool m_RoomItemDropInc = true;
		private static bool m_MasterDoubled = true;
		private static bool m_RainbowDoubled = true;

		private const string m_onStr = "<color=#7FFFD4>on</color>";
		private const string m_offStr = "<color=#DAA520>off</color>";
		private const string m_nameStr = "<color=#00CED1>Jolly Coop</color>";

		private string[] m_EnemyAttrStr = new string[]{ "<color=#8B4513>normal</color>", "<color=#6495ED>medium</color>", "<color=#3CB371>hard</color>", "<color=#FF0000>brutal</color>" };
		private static int EnemyAttr = 1;
		private static float EnemyHealth
        {
            get
            {
				switch(EnemyAttr)
                {
					case 0:
						return 1.4f;
					case 1:
						return 1.6f;
					case 2:
						return 1.8f;
					case 3:
						return 2.0f;
					default:
						return 1.4f;
                }
            }
        }

		private static float EnemyProjectileSpeed
        {
			get
			{
				switch (EnemyAttr)
				{
					case 0:
						return 0.95f;
					case 1:
						return 0.975f;
					case 2:
						return 1.0f;
					case 3:
						return 1.0f;
					default:
						return 0.95f;
				}
			}
		}

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
								AddItem(self);
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
							AddItem(self);
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
								AddItem(self);
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
							if (m_RoomItemDropInc && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
								return orig * 1.5f;
							else
								return orig;
						});
				}
			}

			[HarmonyILManipulator, HarmonyPatch(typeof(RoomHandler), nameof(RoomHandler.HandleBossClearReward))]
			public static void HandleBossClearRewardPatch(ILContext ctx)
			{
				ILCursor crs = new ILCursor(ctx);
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
							if (m_MasterDoubled && flag2 && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
								return orig + IntVector2.Left;
							else
								return orig;
						});
					crs.Emit(OpCodes.Stloc_2);
				}
				crs.Index = 0;

				if (crs.TryGotoNext(MoveType.After,
					x => x.MatchLdloc(18),
					x => x.MatchLdnull(),
					x => x.MatchStfld<RewardPedestal>("MimicGuid")
					))
				{
					crs.Emit(OpCodes.Ldarg_0);
					crs.Emit(OpCodes.Ldloc_2);
					crs.Emit(OpCodes.Ldloc, 10);
					crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Action<RoomHandler, IntVector2, RewardPedestal>>
						((self, intVector, component) =>
						{
							if (m_MasterDoubled && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
							{
								Dungeon dungeon = GameManager.Instance.Dungeon;
								intVector += new IntVector2(2, 0);
								RewardPedestal rewardPedestal4 = RewardPedestal.Spawn(component, intVector, self);
								dungeon.data[intVector].isOccupied = true;
								dungeon.data[intVector + IntVector2.Right].isOccupied = true;
								dungeon.data[intVector + IntVector2.Up].isOccupied = true;
								dungeon.data[intVector + IntVector2.One].isOccupied = true;
								dungeon.HasGivenMasteryToken = true;
								rewardPedestal4.SpawnsTertiarySet = false;
								rewardPedestal4.contents = PickupObjectDatabase.GetById(dungeon.BossMasteryTokenItemId);
								rewardPedestal4.MimicGuid = null;
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
					x => x.MatchLdcR4(2f)
					))
				{
					crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
						(orig =>
						{
							if (m_RoomItemDropInc && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER
							&& GameManager.Instance.Dungeon.HasGivenMasteryToken)
								return orig + 2f;
							else
								return orig;
						});
				}

				if (crs.TryGotoNext(MoveType.After,
					x => x.MatchCall<BraveBehaviour>("get_sprite"),
					x => x.MatchCallvirt<tk2dBaseSprite>("get_WorldCenter"),
					x => x.MatchLdcR4(0f),
					x => x.MatchLdcR4(-3f)
					))
				{
					crs.EmitDelegate<RuntimeILReferenceBag.FastDelegateInvokers.Func<float, float>>
						(orig =>
						{
							if (m_RoomItemDropInc && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER
							&& GameManager.Instance.Dungeon.HasGivenMasteryToken)
								return orig - 2f;
							else
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
							if (m_RainbowDoubled && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
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
							if (m_RainbowDoubled && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                            {
								Vector3? vector2 = vector + new Vector3(4f, 0f, 0f);
								self.StartCoroutine(SpawnChest(vector2.Value, self.data.Entrance));
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
							return EnemyHealth;
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
							return EnemyHealth;
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
							return EnemyProjectileSpeed;
						});
				}
			}
		}

		private void ListStatus()
        {
			ETGModConsole.Log("   ChestItemDoubled " + (m_ChestItemDoubled ? m_onStr : m_offStr));
			ETGModConsole.Log("   RoomItemDropInc " + (m_RoomItemDropInc ? m_onStr : m_offStr));
			ETGModConsole.Log("   MasterDoubled " + (m_MasterDoubled ? m_onStr : m_offStr));
			ETGModConsole.Log("   RainbowDoubled " + (m_RainbowDoubled ? m_onStr : m_offStr));
			ETGModConsole.Log("   EnhancedEnemyAttr " + m_EnemyAttrStr[EnemyAttr]);

			ETGModConsole.Log("enemy health multiplies " + EnemyHealth.ToString() + (EnemyAttr == 0 ? " (originally)" : " (1.4 originally)"));
			ETGModConsole.Log("enemy projectile speed multiplies " + EnemyProjectileSpeed.ToString() + (EnemyAttr == 0 ? " (originally)" : " (0.95 originally)"));
		}

		public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }

		private void ShowMessage(string opt, string cmd, string stat)
		{
			ETGModConsole.Log(m_nameStr + (opt != null ? (": " + opt) : "") + " is " + stat + ". Type 'jollycoop" + (cmd != null ? (" " + cmd) : "") + "' to switch.");
		}

		private void RequestRejected()
		{
			ETGModConsole.Log("Request rejected. " + m_nameStr + " is now " + m_offStr + ". Type 'jollycoop' to switch.");
		}

		private void SwitchJollyCoop(string[] args)
        {
			if (m_JollyCoopIsOn)
            {
				m_ChestItemDoubled = false;
				m_RoomItemDropInc = false;
				m_MasterDoubled = false;
				m_RainbowDoubled = false;
				EnemyAttr = 0;

				ShowMessage(null, null, m_offStr);
			}
            else
            {
				m_ChestItemDoubled = true;
				m_RoomItemDropInc = true;
				m_MasterDoubled = true;
				m_RainbowDoubled = true;
				EnemyAttr = 1;

				ShowMessage(null, null, m_onStr);
			}
			m_JollyCoopIsOn = !m_JollyCoopIsOn;
			ListStatus();
		}

		private void SwitchChestItemDoubled(string[] args)
		{
			if (!m_JollyCoopIsOn)
            {
				RequestRejected();
				return;
			}
			ShowMessage("ChestItemDoubled", "chestitemdoubled", m_ChestItemDoubled ? m_offStr : m_onStr);
			m_ChestItemDoubled = !m_ChestItemDoubled;
			ListStatus();
		}

		private void SwitchRoomItemDropInc(string[] args)
		{
			if (!m_JollyCoopIsOn)
			{
				RequestRejected();
				return;
			}
			ShowMessage("RoomItemDropInc", "roomitemdropinc", m_RoomItemDropInc ? m_offStr : m_onStr);
			m_RoomItemDropInc = !m_RoomItemDropInc;
			ListStatus();
		}

		private void SwitchMasterDoubled(string[] args)
		{
			if (!m_JollyCoopIsOn)
			{
				RequestRejected();
				return;
			}
			ShowMessage("MasterDoubled", "masterdoubled", m_MasterDoubled ? m_offStr : m_onStr);
			m_MasterDoubled = !m_MasterDoubled;
			ListStatus();
		}

		private void SwitchRainbowDoubled(string[] args)
		{
			if (!m_JollyCoopIsOn)
			{
				RequestRejected();
				return;
			}
			ShowMessage("RainbowDoubled", "rainbowdoubled", m_RainbowDoubled ? m_offStr : m_onStr);
			m_RainbowDoubled = !m_RainbowDoubled;
			ListStatus();
		}

		private void SwitchEnhancedEnemyAttr(string[] args)
        {
			if (!m_JollyCoopIsOn)
			{
				RequestRejected();
				return;
			}

			EnemyAttr = (EnemyAttr + 1) % 4;

			ETGModConsole.Log("EnhancedEnemyAttr switched to " + m_EnemyAttrStr[EnemyAttr]);
			ListStatus();
		}

		public static void Log(string text, string color = "FFFFFF")
		{
			ETGModConsole.Log($"<color={color}>{text}</color>");
		}

		public void GMStart(GameManager g)
		{
			Log($"{NAME} v{VERSION} started successfully.", TEXT_COLOR);

			Harmony.CreateAndPatchAll(typeof(JollyCoopPatches));

			ShowMessage(null, null, m_onStr);
			ETGModConsole.Log("<color=#FFFACD>Enter 'jollycoop help' for more options.</color>");
			ListStatus();

			ETGModConsole.Commands.AddGroup("jollycoop", this.SwitchJollyCoop);
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("help", args =>
			{
				ETGModConsole.Log("<color=#FFDEAD>jollycoop</color> - switch Jolly Coop mode");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop status</color> - show the status of jollycoop");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop chestitemdoubled</color> - switch the mode that chests drop double items");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop roomitemdropinc</color> - switch the mode that room clear items drop increase by 50%");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop masterdoubled</color> - switch the mode that boss's master bullet doubled");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop rainbowdoubled</color> - switch the mode that rainbow chest doubled in the rainbow run");
				ETGModConsole.Log("<color=#FFDEAD>jollycoop enhancedenemyattr</color> - switch the mode that multiplies enemy health and enemy projectile speed");
				ETGModConsole.Log("                              " + m_EnemyAttrStr[0] + ": enemy health x1.4, enemy projectile speed x0.95(originally in game)");
				ETGModConsole.Log("                              " + m_EnemyAttrStr[1] + ": enemy health x1.6, enemy projectile speed x0.975");
				ETGModConsole.Log("                              " + m_EnemyAttrStr[2] + "\t: enemy health x1.8, enemy projectile speed x1.0");
				ETGModConsole.Log("                              " + m_EnemyAttrStr[3] + ": enemy health x2.0, enemy projectile speed x1.0");
			});
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("chestitemdoubled", this.SwitchChestItemDoubled);
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("roomitemdropinc", this.SwitchRoomItemDropInc);
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("masterdoubled", this.SwitchMasterDoubled);
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("rainbowdoubled", this.SwitchRainbowDoubled);
			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("enhancedenemyattr", this.SwitchEnhancedEnemyAttr);

			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("status", args =>
			{
				ListStatus();
			});
		}

		public static PlayerItem GetRandomActiveOfQualities(System.Random usedRandom, List<int> excludedIDs, params PickupObject.ItemQuality[] qualities)
		{
			List<PlayerItem> list = new List<PlayerItem>();
			for (int i = 0; i < PickupObjectDatabase.Instance.Objects.Count; i++)
			{
				if (PickupObjectDatabase.Instance.Objects[i] != null && PickupObjectDatabase.Instance.Objects[i] is PlayerItem && PickupObjectDatabase.Instance.Objects[i].quality != PickupObject.ItemQuality.EXCLUDED && PickupObjectDatabase.Instance.Objects[i].quality != PickupObject.ItemQuality.SPECIAL && !(PickupObjectDatabase.Instance.Objects[i] is ContentTeaserItem) && Array.IndexOf<PickupObject.ItemQuality>(qualities, PickupObjectDatabase.Instance.Objects[i].quality) != -1 && !excludedIDs.Contains(PickupObjectDatabase.Instance.Objects[i].PickupObjectId))
				{
					EncounterTrackable component = PickupObjectDatabase.Instance.Objects[i].GetComponent<EncounterTrackable>();
					if (component && component.PrerequisitesMet())
					{
						list.Add(PickupObjectDatabase.Instance.Objects[i] as PlayerItem);
					}
				}
			}
			int num = usedRandom.Next(list.Count);
			if (num < 0 || num >= list.Count)
			{
				return null;
			}
			return list[num];
		}

		public static void AddItem(Chest c)
		{
			if (m_ChestItemDoubled && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
			{
				int count = c.contents.Count;
				for (int i = 0; i < count; i++)
				{
					if (c.contents[i].quality == PickupObject.ItemQuality.A || c.contents[i].quality == PickupObject.ItemQuality.B || c.contents[i].quality == PickupObject.ItemQuality.C || c.contents[i].quality == PickupObject.ItemQuality.D || c.contents[i].quality == PickupObject.ItemQuality.S)
					{
						if (c.contents[i] is Gun)
						{
							c.contents.Add(PickupObjectDatabase.GetRandomGunOfQualities(new System.Random(), new List<int>(), new PickupObject.ItemQuality[]
							{
								c.contents[i].quality
							}));
						}
						else if (c.contents[i] is PassiveItem)
						{
							c.contents.Add(PickupObjectDatabase.GetRandomPassiveOfQualities(new System.Random(), new List<int>(), new PickupObject.ItemQuality[]
							{
								c.contents[i].quality
							}));
						}
						else if (c.contents[i] is PlayerItem)
						{
							c.contents.Add(GetRandomActiveOfQualities(new System.Random(), new List<int>(), new PickupObject.ItemQuality[]
							{
								c.contents[i].quality
							}));
						}
					}
					else
					{
						c.contents.Add(c.contents[i]);
					}
				}
			}
		}

		private static IEnumerator SpawnChest(Vector3 v, RoomHandler room)
		{
			yield return new WaitForSeconds(0.45f);
			Chest c = Chest.Spawn(GameManager.Instance.RewardManager.A_Chest, v, room, true);
			c.IsRainbowChest = true;
			c.BecomeRainbowChest();
			yield break;
		}
	}
}
