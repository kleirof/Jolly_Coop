using BepInEx;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Dungeonator;
using HarmonyLib;
using Gunfiguration;

namespace JollyCoop
{
    [BepInDependency("etgmodding.etg.mtgapi")]
    [BepInDependency("pretzel.etg.gunfig")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class JollyCoopModule : BaseUnityPlugin
    {
        public const string GUID = "kleirof.etg.jollycoop";
        public const string NAME = "Jolly Coop";
        public const string VERSION = "1.2.0";
        public const string TEXT_COLOR = "#00CED1";

		internal static Dictionary<PickupObject, bool> playerOneExclusivePickups = new Dictionary<PickupObject, bool>();
		internal static List<PickupObject> playerOneExclusiveLoots = new List<PickupObject>();

		internal static Dictionary<PickupObject, bool> playerTwoExclusivePickups = new Dictionary<PickupObject, bool>();
		internal static List<PickupObject> playerTwoExclusiveLoots = new List<PickupObject>();

		internal static List<RewardPedestal> playerOneExclusivePedestals = new List<RewardPedestal>();
		internal static List<RewardPedestal> playerTwoExclusivePedestals = new List<RewardPedestal>();

		internal static Gunfig gunfig = null;

		internal const string jollyCoopIsOnStr = "Jolly Coop Is On";
		internal const string itemDistribLockStr = "Item Distribution Lock";
		internal const string chestItemDoubledStr = "Chest Item Doubled";
		internal const string roomItemDropIncStr = "Room Item Drop Increace";
		internal const string masterDoubledStr = "Master Doubled";
		internal const string normalBossRewardDoubledStr = "Normal Boss Reward Doubled";
		internal const string rainbowDoubledStr = "Rainbow Doubled";
		internal const string enhancedEnemyAttrStr = "Enhanced Enemy Attributes";

		private const string m_onStr = "<color=#7FFFD4>on</color>";
		private const string m_offStr = "<color=#DAA520>off</color>";

		private static string[] m_EnemyAttrStr = new string[]{ "<color=#8B4513>normal</color>", "<color=#6495ED>medium</color>", "<color=#3CB371>hard</color>", "<color=#FF0000>brutal</color>" };
		private static int enemyAttr;

		private static List<string> attrStr = new List<string>() { "@6495EDmedium", "@3CB371hard", "@FF0000brutal", "@8B4513normal" };

		private static readonly List<string> descriptionStr = new List<string>()
		{
			("enemy health x1.6 (1.4 originally)\nenemy projectile speed x0.975 (0.95 originally)").Green(),
			("enemy health x1.8 (1.4 originally)\nenemy projectile speed x1.0 (0.95 originally)").Green(),
			("enemy health x2.0 (1.4 originally)\nenemy projectile speed x1.0 (0.95 originally)").Green(),
			("enemy health x1.4 (originally in coop)\nenemy projectile speed x0.95 (originally in coop)").Green()
		};

		public static float EnemyHealth
        {
            get
            {
				if (gunfig.Enabled(jollyCoopIsOnStr))
				{
					switch (enemyAttr)
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
				else
					return 1.4f;
            }
        }

		public static float EnemyProjectileSpeed
        {
			get
			{
				if (gunfig.Enabled(jollyCoopIsOnStr))
				{
					switch (enemyAttr)
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
				else
					return 0.95f;
			}
		}

		private void ListStatus()
        {
			ETGModConsole.Log("Jolly Coop is " + (gunfig.Enabled(jollyCoopIsOnStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Item Distribution Lock " + (gunfig.Enabled(itemDistribLockStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Chest Item Doubled " + (gunfig.Enabled(chestItemDoubledStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Room Item Drop Increase " + (gunfig.Enabled(roomItemDropIncStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Master Doubled " + (gunfig.Enabled(masterDoubledStr) ? m_onStr : m_offStr)); 
			ETGModConsole.Log("   Normal Boss Reward Doubled " + (gunfig.Enabled(normalBossRewardDoubledStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Rainbow Doubled " + (gunfig.Enabled(rainbowDoubledStr) ? m_onStr : m_offStr));
			ETGModConsole.Log("   Enhanced Enemy Attributes " + m_EnemyAttrStr[enemyAttr]);

			ETGModConsole.Log("enemy health multiplies " + EnemyHealth.ToString() + (enemyAttr == 0 ? " (originally)" : " (1.4 originally)"));
			ETGModConsole.Log("enemy projectile speed multiplies " + EnemyProjectileSpeed.ToString() + (enemyAttr == 0 ? " (originally)" : " (0.95 originally)"));

			ETGModConsole.Log("   <color=#FFDEAD>Enhanced Enemy Attributes</color>  - The mode that multiplies enemy health and enemy projectile speed");
			ETGModConsole.Log("                              " + m_EnemyAttrStr[0] + ": enemy health x1.4, enemy projectile speed x0.95 (originally in coop)");
			ETGModConsole.Log("                              " + m_EnemyAttrStr[1] + ": enemy health x1.6, enemy projectile speed x0.975");
			ETGModConsole.Log("                              " + m_EnemyAttrStr[2] + ": enemy health x1.8, enemy projectile speed x1.0");
			ETGModConsole.Log("                              " + m_EnemyAttrStr[3] + ": enemy health x2.0, enemy projectile speed x1.0");
		}

		public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
        }

        private void UpdateEnhancedEnemyAttr(string value)
        {
			if (value == null)
				value = gunfig.Value(enhancedEnemyAttrStr);

			switch (value)
			{
				case "@8B4513normal":
					enemyAttr = 0;
					break;
				case "@6495EDmedium":
					enemyAttr = 1;
					break;
				case "@3CB371hard":
					enemyAttr = 2;
					break;
				case "@FF0000brutal":
					enemyAttr = 3;
					break;

				case "normal":
					enemyAttr = 0;
					break;
				case "medium":
					enemyAttr = 1;
					break;
				case "hard":
					enemyAttr = 2;
					break;
				case "brutal":
					enemyAttr = 3;
					break;

				default:
					enemyAttr = 0;
					break;
			}
		}

        public static void Log(string text, string color = "FFFFFF")
		{
			ETGModConsole.Log($"<color={color}>{text}</color>");
		}

		public void GMStart(GameManager g)
		{
			Log($"{NAME} v{VERSION} started successfully.", TEXT_COLOR);

			gunfig = Gunfig.Get("Jolly Coop".WithColor(Color.white));

			gunfig.AddToggle(key: jollyCoopIsOnStr, enabled: true);
			gunfig.AddToggle(key: itemDistribLockStr, enabled: true);
			gunfig.AddToggle(key: chestItemDoubledStr, enabled: true);
			gunfig.AddToggle(key: roomItemDropIncStr, enabled: true);
			gunfig.AddToggle(key: masterDoubledStr, enabled: true);
			gunfig.AddToggle(key: normalBossRewardDoubledStr, enabled: true);
			gunfig.AddToggle(key: rainbowDoubledStr, enabled: true);
			gunfig.AddScrollBox(key: enhancedEnemyAttrStr, options: attrStr, info: descriptionStr, 
				callback: (optionKey, optionValue) => UpdateEnhancedEnemyAttr(optionValue));
			UpdateEnhancedEnemyAttr(null);

			Harmony.CreateAndPatchAll(typeof(JollyCoopPatches));
			
			ETGModConsole.Log("<color=#FFFACD>Enter 'jollycoop' to see Jolly Coop status. Switch options in Modded Config.</color>");
			ListStatus();

			ETGModConsole.Commands.AddGroup("jollycoop", args => ListStatus());

			ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("status", args => ListStatus());
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
			if (gunfig.Enabled(jollyCoopIsOnStr) && gunfig.Enabled(chestItemDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
			{
				int count = c.contents.Count;
				for (int i = 0; i < count; i++)
				{
					PickupObject pickupObject;
					if (c.contents[i].quality == PickupObject.ItemQuality.A || c.contents[i].quality == PickupObject.ItemQuality.B || c.contents[i].quality == PickupObject.ItemQuality.C || c.contents[i].quality == PickupObject.ItemQuality.D || c.contents[i].quality == PickupObject.ItemQuality.S)
					{
						RewardManager rewardManager = GameManager.Instance.RewardManager;
						GenericLootTable lootTable = c.contents[i] is Gun ? rewardManager.GunsLootTable : rewardManager.ItemsLootTable;
						pickupObject = rewardManager.GetItemForPlayer(GameManager.Instance.SecondaryPlayer, lootTable, c.contents[i].quality, null).GetComponent<PickupObject>();

						c.contents.Add(pickupObject);
						playerOneExclusiveLoots.Add(pickupObject);
						playerTwoExclusiveLoots.Add(c.contents[i]);
					}
					else if (c.contents[i] is PassiveItem)
                    {
						pickupObject = Instantiate(c.contents[i]);
						c.contents.Add(pickupObject);
						playerOneExclusiveLoots.Add(pickupObject);
						playerTwoExclusiveLoots.Add(c.contents[i]);
					}
					else
						c.contents.Add(c.contents[i]);
				}
			}
		}

		public static IEnumerator SpawnChest(Vector3 v, RoomHandler room)
		{
			yield return new WaitForSeconds(0.45f);
			Chest c = Chest.Spawn(GameManager.Instance.RewardManager.A_Chest, v, room, true);
			c.IsRainbowChest = true;
			c.BecomeRainbowChest();
			yield break;
		}
	}
}
