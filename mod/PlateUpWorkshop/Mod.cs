using System.Reflection;
using KitchenLib;
using KitchenMods;
using UnityEngine;

namespace KitchenPlateUpLayoutEditor
{
    /// <summary>
    /// Actual Workshop mod entry point, based on the public PlateUpModTemplate.
    /// First milestone: confirm loader activation. ECS mutation is not yet enabled.
    /// </summary>
    public sealed class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "LauBok.PlateUp.LayoutEditor";
        public const string MOD_NAME = "PlateUp Layout Editor";
        public const string MOD_AUTHOR = "LauBok";
        public const string MOD_VERSION = "0.1.0";
        public const string MOD_GAMEVERSION = ">=1.1.3";

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION,
            MOD_GAMEVERSION, Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise()
        {
            Debug.Log("[PlateUp Layout Editor] Workshop mod loaded.");
            BlueprintSpawnProbe.Initialize();
        }

        protected override void OnUpdate()
        {
            BlueprintSpawnProbe.Update();
        }

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            Debug.Log("[PlateUp Layout Editor] Workshop mod activated.");
        }
    }
}
