using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LokrModAPI;
using LokrModAPI.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LokrModMenu
{
	/// <summary>Global mod menu popup and hotkey entry point. Other plugins extend via <see cref="ModMenuAPI"/>.</summary>
	[BepInPlugin(Guid, Name, Version)]
	[BepInDependency(LokrModAPIPlugin.Guid)]
	public class LokrModMenuPlugin : BaseUnityPlugin
	{
		/// <summary>This plugin's BepInEx GUID.</summary>
		public const string Guid = "com.lokrmodding.modmenu";
		/// <summary>This plugin's display name.</summary>
		public const string Name = "LoKR Mod Menu";
		/// <summary>This plugin's version string.</summary>
		public const string Version = "1.1.1";

		internal static ManualLogSource Log;
		internal static ConfigEntry<KeyCode> ToggleMenuKey;
		internal static ConfigEntry<bool> ToggleMenuControl;
		internal static ConfigEntry<bool> ToggleMenuShift;
		internal static ConfigEntry<bool> ToggleMenuAlt;
		internal static ConfigEntry<bool> AlsoToggleOnF3;

		private Harmony harmony;

		private void Awake()
		{
			Log = Logger;

			// Legacy config key names kept so existing .cfg files keep working.
			ToggleMenuKey = Config.Bind(
				"Hotkeys",
				"ToggleCharacterLab",
				KeyCode.BackQuote,
				"Primary key for the mod menu popup. Backquote (`) works on Linux/Proton where bare F-keys are often stolen by the desktop.");

			ToggleMenuControl = Config.Bind(
				"Hotkeys",
				"ToggleCharacterLabControl",
				false,
				"Require Control held with the mod menu hotkey.");

			ToggleMenuShift = Config.Bind(
				"Hotkeys",
				"ToggleCharacterLabShift",
				false,
				"Require Shift held with the mod menu hotkey.");

			ToggleMenuAlt = Config.Bind(
				"Hotkeys",
				"ToggleCharacterLabAlt",
				false,
				"Require Alt held with the mod menu hotkey.");

			AlsoToggleOnF3 = Config.Bind(
				"Hotkeys",
				"AlsoToggleOnF3",
				true,
				"Also listen for bare F3 (may not reach the game on Linux unless the desktop passes function keys through).");

			RegisterHotkeys();

			SceneManager.sceneLoaded += OnSceneLoaded;

			harmony = new Harmony(Guid);
			harmony.PatchAll();

			Log.LogInfo(string.Format(
				"{0} v{1} loaded — press {2} to open the mod menu{3}. Plugins register entries via ModMenuAPI.",
				Name,
				Version,
				DescribePrimaryBinding(),
				AlsoToggleOnF3.Value ? " (F3 also bound)" : string.Empty));
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ModMenuOverlay.ForceClose();
		}

		private static void RegisterHotkeys()
		{
			KeyBinding primary = BuildPrimaryBinding();
			KeyBinding bareF3 = new KeyBinding(KeyCode.F3);

			GameInputPoll.Register("ToggleModMenu", primary, ModMenuAPI.Toggle);

			if (AlsoToggleOnF3.Value && !primary.Equals(bareF3))
			{
				GameInputPoll.Register("ToggleModMenuF3", bareF3, ModMenuAPI.Toggle);
			}
			else
			{
				GameInputPoll.Unregister("ToggleModMenuF3");
			}
		}

		private static KeyBinding BuildPrimaryBinding()
		{
			return new KeyBinding(
				ToggleMenuKey.Value,
				ToggleMenuControl.Value,
				ToggleMenuShift.Value,
				ToggleMenuAlt.Value);
		}

		private static string DescribePrimaryBinding()
		{
			return BuildPrimaryBinding().ToString();
		}
	}
}
