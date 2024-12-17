using BepInEx;
using HarmonyLib;

namespace JollyCoop
{
    [BepInDependency("etgmodding.etg.mtgapi")]
    [BepInDependency("pretzel.etg.gunfig")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class JollyCoopModule : BaseUnityPlugin
    {
        public const string GUID = "kleirof.etg.jollycoop";
        public const string NAME = "Jolly Coop";
        public const string VERSION = "1.3.0";
        public const string TEXT_COLOR = "#00CED1";

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);

            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll();
        }

        public void GMStart(GameManager g)
        {
            Log($"{NAME} v{VERSION} started successfully.", TEXT_COLOR);

            JollyCoopManager.InitializeGunfig();
        }

        public static void Log(string text, string color = "FFFFFF")
        {
            ETGModConsole.Log($"<color={color}>{text}</color>");
        }
    }
}
