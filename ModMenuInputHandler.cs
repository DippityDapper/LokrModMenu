using UnityEngine;

namespace LokrModMenu
{
	/// <summary>Handles Escape-to-close while the mod menu overlay is active.</summary>
	internal sealed class ModMenuInputHandler : MonoBehaviour
	{
		private void Update()
		{
			if (ModMenuOverlay.IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
			{
				ModMenuOverlay.ShowMainView();
				if (!ModMenuOverlay.IsShowingSubmenu)
				{
					ModMenuOverlay.Close();
				}
			}
		}
	}
}
