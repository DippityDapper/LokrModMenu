using System;
using System.Collections.Generic;
using UnityEngine;

namespace LokrModMenu
{
	/// <summary>Extension point for adding entries to the global LoKR mod menu popup.</summary>
	/// <remarks>
	/// Call <see cref="RegisterButton"/> or <see cref="RegisterSubmenu"/> from your plugin's
	/// Awake (after declaring a BepInDependency on <see cref="LokrModMenuPlugin"/>). The menu
	/// rebuilds its button list every time it opens, so late registration before first open works.
	/// </remarks>
	public static class ModMenuAPI
	{
		internal abstract class Entry
		{
			internal string Id;
			internal string Label;
			internal int SortOrder;
		}

		internal sealed class ButtonEntry : Entry
		{
			internal Action OnClick;
			internal bool CloseOnClick = true;
		}

		internal sealed class SubmenuEntry : Entry
		{
			internal Action<Transform> BuildContent;
			internal bool HasBuilt;
		}

		private static readonly List<Entry> entries = new List<Entry>();
		private static readonly List<(Func<bool> IsOpen, Action Close)> blockingOverlays = new List<(Func<bool>, Action)>();

		/// <summary>True while the mod menu overlay is visible.</summary>
		public static bool IsOpen => ModMenuOverlay.IsOpen;

		/// <summary>Shows the mod menu popup.</summary>
		public static void Open() => ModMenuOverlay.Open();

		/// <summary>Hides the mod menu popup.</summary>
		public static void Close() => ModMenuOverlay.Close();

		/// <summary>Shows or hides the mod menu popup.</summary>
		public static void Toggle() => ModMenuOverlay.Toggle();

		/// <summary>
		/// Registers another full-screen overlay (e.g. Character Lab, Ability Lab). While any
		/// registered overlay is open the mod menu will not stack on top; the shared hotkey closes
		/// it first. Multiple plugins may each register their own overlay -- this is a list, not a
		/// single slot, so one plugin's registration never clobbers another's.
		/// </summary>
		public static void RegisterBlockingOverlay(Func<bool> isOpen, Action close)
		{
			blockingOverlays.Add((isOpen, close));
		}

		internal static bool HasBlockingOverlayOpen => blockingOverlays.Exists(overlay => overlay.IsOpen());

		internal static void CloseBlockingOverlay()
		{
			foreach ((Func<bool> IsOpen, Action Close) overlay in blockingOverlays)
			{
				if (overlay.IsOpen())
				{
					overlay.Close();
				}
			}
		}

		/// <summary>Registers a menu button. Replaces any existing entry with the same id.</summary>
		/// <param name="id">Unique id (use your plugin guid prefix, e.g. "character-lab").</param>
		/// <param name="label">Button label shown in the menu.</param>
		/// <param name="onClick">Called when the player clicks the button.</param>
		/// <param name="sortOrder">Lower values appear first.</param>
		/// <param name="closeOnClick">When true (default), the menu closes before onClick runs.</param>
		public static void RegisterButton(string id, string label, Action onClick,
			int sortOrder = 0, bool closeOnClick = true)
		{
			entries.RemoveAll(entry => entry.Id == id);
			entries.Add(new ButtonEntry
			{
				Id = id,
				Label = label,
				SortOrder = sortOrder,
				OnClick = onClick,
				CloseOnClick = closeOnClick,
			});
		}

		/// <summary>Registers a submenu entry that opens a nested panel with custom UI.</summary>
		/// <param name="id">Unique id.</param>
		/// <param name="label">Button label on the main menu.</param>
		/// <param name="buildContent">Called once on first open; build child widgets under the given root.</param>
		/// <param name="sortOrder">Lower values appear first.</param>
		public static void RegisterSubmenu(string id, string label, Action<Transform> buildContent,
			int sortOrder = 0)
		{
			entries.RemoveAll(entry => entry.Id == id);
			entries.Add(new SubmenuEntry
			{
				Id = id,
				Label = label,
				SortOrder = sortOrder,
				BuildContent = buildContent,
			});
		}

		/// <summary>Removes a previously registered entry.</summary>
		public static void Unregister(string id)
		{
			entries.RemoveAll(entry => entry.Id == id);
		}

		internal static IReadOnlyList<Entry> Entries => entries;

		internal static void ResetSubmenuBuildState(string id)
		{
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i] is SubmenuEntry submenu && submenu.Id == id)
				{
					submenu.HasBuilt = false;
				}
			}
		}
	}
}
