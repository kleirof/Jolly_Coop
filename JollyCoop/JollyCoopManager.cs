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
    class JollyCoopManager
    {
        internal static Gunfig gunfig = null;

        internal const string jollyCoopOnStr = "Jolly Coop On";
        internal const string itemDistribLockStr = "Item Distribution Lock";
        internal const string chestItemDoubledStr = "Chest Item Doubled";
        internal const string roomItemDropIncStr = "Room Item Drop Increace";
        internal const string masterDoubledStr = "Master Doubled";
        internal const string normalBossRewardDoubledStr = "Normal Boss Reward Doubled";
        internal const string extraEnemyHealthStr = "Extra Enemy Health";
        internal const string extraEnemyProjectileSpeedStr = "Extra Enemy Projectile Speed";

        private const string m_onStr = "<color=#7FFFD4>on</color>";
        private const string m_offStr = "<color=#DAA520>off</color>";

        private static string[] extraEnemyHealthStrings = new string[] { "<color=#8B4513>0</color>", "<color=#6495ED>0.1</color>", "<color=#3CB371>0.2</color>", "<color=#FF0000>0.3</color>", "<color=#808080>-0.2</color>", "<color=#C0C0C0>-0.1</color>" };
        private static int extraEnemyHealthIndex;

        private static List<string> extraEnemyHealthStringList = new List<string>() { "@8B45130", "@6495ED0.1", "@3CB3710.2", "@FF00000.3", "@808080-0.2", "@C0C0C0-0.1" };

        private static string[] extraEnemyProjectileSpeedStrings = new string[] { "<color=#8B4513>0</color>", "<color=#6495ED>0.02</color>", "<color=#3CB371>0.04</color>", "<color=#FF0000>0.06</color>", "<color=#808080>-0.04</color>", "<color=#C0C0C0>-0.02</color>" };
        private static int extraEnemyProjectileSpeedIndex;

        private static List<string> extraEnemyProjectileSpeedStringList = new List<string>() { "@8B45130", "@6495ED0.02", "@3CB3710.04", "@FF00000.06", "@808080-0.04", "@C0C0C0-0.02" };

        private static readonly Dictionary<string, int> ExtraEnemyHealthLookup = new Dictionary<string, int>
        {
            { "@8B45130", 0 },
            { "@6495ED0.1", 1 },
            { "@3CB3710.2", 2 },
            { "@FF00000.3", 3 },
            { "@808080-0.2", 4 },
            { "@C0C0C0-0.1", 5 },

            { "0", 0 },
            { "0.1", 1 },
            { "0.2", 2 },
            { "0.3", 3 },
            { "-0.2", 4 },
            { "-0.1", 5 }
        };

        private static readonly Dictionary<string, int> ExtraEnemyProjectileSpeedLookup = new Dictionary<string, int>
        {
            { "@8B45130", 0 },
            { "@6495ED0.02", 1 },
            { "@3CB3710.04", 2 },
            { "@FF00000.06", 3 },
            { "@808080-0.04", 4 },
            { "@C0C0C0-0.02", 5 },

            { "0", 0 },
            { "0.02", 1 },
            { "0.04", 2 },
            { "0.06", 3 },
            { "-0.04", 4 },
            { "-0.02", 5 }
        };

        public static float EnemyHealth
        {
            get
            {
                if (!gunfig.Enabled(jollyCoopOnStr))
                    return 1.4f;

                float result = 1.4f;
                if (gunfig.Enabled(chestItemDoubledStr))
                    result += 0.1f;
                if (gunfig.Enabled(roomItemDropIncStr))
                    result += 0.075f;
                if (gunfig.Enabled(masterDoubledStr))
                    result += 0.1f;
                if (gunfig.Enabled(normalBossRewardDoubledStr))
                    result += 0.025f;
                result += (extraEnemyHealthIndex > 3 ? extraEnemyHealthIndex - 6 : extraEnemyHealthIndex) * 0.1f;
                return result < 1.4f ? 1.4f : result;
            }
        }

        public static float EnemyProjectileSpeed
        {
            get
            {
                if (!gunfig.Enabled(jollyCoopOnStr))
                    return 0.95f;

                float result = 0.95f;
                if (gunfig.Enabled(chestItemDoubledStr))
                    result += 0.02f;
                if (gunfig.Enabled(roomItemDropIncStr))
                    result += 0.005f;
                if (gunfig.Enabled(masterDoubledStr))
                    result += 0.02f;
                if (gunfig.Enabled(normalBossRewardDoubledStr))
                    result += 0.005f;
                result += (extraEnemyProjectileSpeedIndex > 3 ? extraEnemyProjectileSpeedIndex - 6 : extraEnemyProjectileSpeedIndex) * 0.02f;
                return result < 0.95f ? 0.95f : result;
            }
        }

        public static float ItemRecyclingPrice
        {
            get
            {
                if (!gunfig.Enabled(jollyCoopOnStr))
                    return 1f;

                float result = 1f;
                if (gunfig.Enabled(chestItemDoubledStr))
                    result *= 0.6f;
                return result;
            }
        }

        internal static void InitializeGunfig()
        {
            gunfig = Gunfig.Get("Jolly Coop".WithColor(Color.white));

            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量 ×= (1.4 + 以下所有之和)".WithColor(Color.green) :
                "Enemy Health ×= (1.4 + sum of all below)".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "（1.4是合作模式原本的数值）".WithColor(Color.green) :
                "(1.4 is the original value in coop)".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人弹速 ×= (0.95 + 以下所有之和)".WithColor(Color.green) :
                "Enemy Projectile Speed ×= (0.95 + sum of all below)".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "（0.95是合作模式原本的数值）".WithColor(Color.green) :
                "(0.95 is the original value in coop)".WithColor(Color.green));
            gunfig.AddToggle(key: jollyCoopOnStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "开启Jolly Coop" :
                jollyCoopOnStr, enabled: true);
            gunfig.AddLabel(" ");
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "无负面作用".WithColor(Color.green) :
                "No negative effects".WithColor(Color.green));
            gunfig.AddToggle(key: itemDistribLockStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "物品分发锁" :
                itemDistribLockStr, enabled: true);
            gunfig.AddLabel(" ");
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量 += 0.1".WithColor(Color.green) :
                "Enemy Health += 0.1".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人弹速 += 0.02".WithColor(Color.green) :
                "Enemy Projectile Speed += 0.02".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "物品回收价格 ×= 0.6".WithColor(Color.green) :
                "Item Recycling Price ×= 0.6".WithColor(Color.green));
            gunfig.AddToggle(key: chestItemDoubledStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "箱子双倍物品" :
                chestItemDoubledStr, enabled: true);
            gunfig.AddLabel(" ");
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量 += 0.075".WithColor(Color.green) :
                "Enemy Health += 0.075".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人弹速 += 0.005".WithColor(Color.green) :
                "Enemy Projectile Speed += 0.005".WithColor(Color.green));
            gunfig.AddToggle(key: roomItemDropIncStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "房间掉落增加" :
                roomItemDropIncStr, enabled: true);
            gunfig.AddLabel(" ");
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量 += 0.1".WithColor(Color.green) :
                "Enemy Health += 0.1".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人弹速 += 0.02".WithColor(Color.green) :
                "Enemy Projectile Speed += 0.02".WithColor(Color.green));
            gunfig.AddToggle(key: masterDoubledStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "胜者之弹双倍" :
                masterDoubledStr, enabled: true);
            gunfig.AddLabel(" ");
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量 += 0.025".WithColor(Color.green) :
                "Enemy Health += 0.025".WithColor(Color.green));
            gunfig.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人弹速 += 0.005".WithColor(Color.green) :
                "Enemy Projectile Speed += 0.005".WithColor(Color.green));
            gunfig.AddToggle(key: normalBossRewardDoubledStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "Boss普通奖励双倍" :
                normalBossRewardDoubledStr, enabled: true);
            gunfig.AddLabel(" ");


            Gunfig manualBalance = gunfig.AddSubMenu(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "手动平衡".WithColor(Color.cyan) :
                "Manual Balance".WithColor(Color.cyan));


            manualBalance.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人血量不低于1.4".WithColor(Color.red) :
                "Enemy health not less than 1.4".WithColor(Color.red));
            manualBalance.AddScrollBox(key: extraEnemyHealthStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "额外敌人血量增量" :
                extraEnemyHealthStr, options: extraEnemyHealthStringList,
                callback: (optionKey, optionValue) => UpdateExtraEnemyHealth(optionValue));
            UpdateExtraEnemyHealth(null);

            manualBalance.AddLabel(GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "敌人子弹速度不低于0.95".WithColor(Color.red) :
                "Enemy Projectile Speed not less than 0.95".WithColor(Color.red));
            manualBalance.AddScrollBox(key: extraEnemyProjectileSpeedStr, label: GameManager.Options.CurrentLanguage == StringTableManager.GungeonSupportedLanguages.CHINESE ?
                "额外敌人子弹速度" :
                extraEnemyProjectileSpeedStr, options: extraEnemyProjectileSpeedStringList,
                callback: (optionKey, optionValue) => UpdateExtraEnemyProjectileSpeed(optionValue));
            UpdateExtraEnemyProjectileSpeed(null);

            ETGModConsole.Log("<color=#FFFACD>Enter 'jollycoop' to see Jolly Coop status. Switch options in Mod Config.</color>");
            ListStatus();

            ETGModConsole.Commands.AddGroup("jollycoop", args => ListStatus());

            ETGModConsole.Commands.GetGroup("jollycoop").AddUnit("status", args => ListStatus());
        }

        private static void ListStatus()
        {
            ETGModConsole.Log("Jolly Coop is " + (gunfig.Enabled(jollyCoopOnStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Item Distribution Lock " + (gunfig.Enabled(itemDistribLockStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Chest Item Doubled " + (gunfig.Enabled(chestItemDoubledStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Room Item Drop Increase " + (gunfig.Enabled(roomItemDropIncStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Master Doubled " + (gunfig.Enabled(masterDoubledStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Normal Boss Reward Doubled " + (gunfig.Enabled(normalBossRewardDoubledStr) ? m_onStr : m_offStr));
            ETGModConsole.Log("   Extra Enemy Health " + extraEnemyHealthStrings[extraEnemyHealthIndex]);
            ETGModConsole.Log("   Extra Enemy Projectile Speed " + extraEnemyProjectileSpeedStrings[extraEnemyProjectileSpeedIndex]);

            ETGModConsole.Log("<color=#00CED1>In coop:</color>");
            ETGModConsole.Log("<color=#FFFACD> -- Enemy Health multiplies </color>" + EnemyHealth.ToString("F3"));
            ETGModConsole.Log("<color=#FFFACD> -- Enemy Projectile Speed multiplies </color>" + EnemyProjectileSpeed.ToString("F3"));
            ETGModConsole.Log("<color=#FFFACD> -- Item Recycling Price multiplies </color>" + ItemRecyclingPrice.ToString("F3"));
        }

        private static void UpdateExtraEnemyHealth(string value)
        {
            if (value == null)
                value = gunfig.Value(extraEnemyHealthStr);

            if (!ExtraEnemyHealthLookup.TryGetValue(value, out int index))
            {
                index = 0;
            }

            extraEnemyHealthIndex = index;
        }

        private static void UpdateExtraEnemyProjectileSpeed(string value)
        {
            if (value == null)
                value = gunfig.Value(extraEnemyProjectileSpeedStr);

            if (!ExtraEnemyProjectileSpeedLookup.TryGetValue(value, out int index))
            {
                index = 0;
            }

            extraEnemyProjectileSpeedIndex = index;
        }

        public static void AddItem(Chest c)
        {
            if (gunfig.Enabled(jollyCoopOnStr) && gunfig.Enabled(chestItemDoubledStr) && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
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
                        if (pickupObject == c.contents[i])
                            pickupObject = UnityEngine.Object.Instantiate(c.contents[i]);
                        c.contents.Add(pickupObject);
                        JollyCoopPatches.playerOneExclusiveLoots.Add(pickupObject);
                        JollyCoopPatches.playerTwoExclusiveLoots.Add(c.contents[i]);
                    }
                    else if (c.contents[i] is PassiveItem)
                    {
                        pickupObject = UnityEngine.Object.Instantiate(c.contents[i]);
                        c.contents.Add(pickupObject);
                        JollyCoopPatches.playerOneExclusiveLoots.Add(pickupObject);
                        JollyCoopPatches.playerTwoExclusiveLoots.Add(c.contents[i]);
                    }
                    else if (!(c.contents[i] is KeyBulletPickup))
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
            JollyCoopPatches.playerOneExclusiveChests.Add(c);
            yield break;
        }
    }
}
