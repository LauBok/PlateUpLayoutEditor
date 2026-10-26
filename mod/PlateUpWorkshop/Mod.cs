using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Kitchen;
using KitchenMods;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>Workshop entry point, discovered by the game's own mod loader (no KitchenLib required).</summary>
    public class Main : IModInitializer
    {
        public const string MOD_GUID = "LauBok.PlateUp.LayoutEditor";
        public const string MOD_NAME = "Layout Editor";
        public const string MOD_VERSION = "0.2.0";

        internal static readonly ViewType EditorViewType = (ViewType)StableHash("LauBok.LayoutEditor.View");

        /// <summary>Host setting: whether requests from other players are executed.</summary>
        internal static bool ClientsAllowed
        {
            get => EditorSettings.Current.AllowClients;
        }

        private static GameObject? _gui;

        /// <summary>True when the Harmony Workshop mod is present and the network view patch is active.
        /// Without it the editor still works for the host / solo player, but not for clients.</summary>
        internal static bool NetworkingAvailable { get; private set; }

        public Main()
        {
            if (NetworkingAvailable) return;
            try
            {
                PatchWithHarmony();
                NetworkingAvailable = true;
            }
            catch (Exception e)
            {
                LogWarning("Harmony is not available (" + e.GetType().Name + "); multiplayer client editing is disabled. " +
                           "Subscribe to the 'Harmony' Workshop mod to enable it.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchWithHarmony()
        {
            var harmony = new Harmony(MOD_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void PostActivate(KitchenMods.Mod mod)
        {
            LogInfo("v" + MOD_VERSION + " loaded. Press F7 in a restaurant to open the editor.");
            EditorSettings.Load();
            if (_gui == null)
            {
                _gui = new GameObject("LayoutEditorWindow");
                UnityEngine.Object.DontDestroyOnLoad(_gui);
                _gui.AddComponent<EditorWindow>();
            }
        }

        public void PreInject() { }

        public void PostInject() { }

        private static int StableHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToInt32(bytes, 0);
            }
        }

        internal static void LogInfo(string message) => Debug.Log("[" + MOD_NAME + "] " + message);
        internal static void LogWarning(string message) => Debug.LogWarning("[" + MOD_NAME + "] " + message);
        internal static void LogError(string message) => Debug.LogError("[" + MOD_NAME + "] " + message);
    }
}
