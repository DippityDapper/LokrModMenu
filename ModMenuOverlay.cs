using System.Collections.Generic;
using Ironhide.Legends;
using Ironhide.Legends.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LokrModMenu
{
	/// <summary>Persistent overlay that lists registered mod menu entries.</summary>
	internal static class ModMenuOverlay
	{
		private const string SceneName = "LokrModMenu";
		private const int CanvasSortOrder = 19000;

		private static Scene menuScene;
		private static bool isBuilt;
		private static bool isOpen;

		private static Transform mainView;
		private static Transform submenuView;
		private static Transform buttonContainer;
		private static Transform submenuContent;
		private static Text submenuTitle;
		private static string activeSubmenuId;

		private static readonly List<EventSystem> disabledEventSystems = new List<EventSystem>();
		private static readonly Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

		internal static bool IsOpen => isOpen;
		internal static bool IsShowingSubmenu => submenuView != null && submenuView.gameObject.activeSelf;

		internal static void Toggle()
		{
			RecoverIfSceneWasDestroyed();

			if (IsLoadingScreenBlocking())
			{
				if (isOpen)
				{
					Close();
				}

				return;
			}

			if (isOpen)
			{
				Close();
				return;
			}

			if (ModMenuAPI.HasBlockingOverlayOpen)
			{
				ModMenuAPI.CloseBlockingOverlay();
				return;
			}

			Open();
		}

		/// <summary>True while the base-game fade/loading overlay or a transition/splash scene is up.</summary>
		/// <remarks>FadeScreen.updating is only true during the 0.75s tween; the loading graphic stays visible after that (canvas + content alpha). The dedicated transition and splash scenes have no playable UI to attach a menu to.</remarks>
		private static bool IsLoadingScreenBlocking()
		{
			if (IsFadeScreenShowingLoad())
			{
				return true;
			}

			Scene scene = SceneManager.GetActiveScene();
			if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
			{
				return true;
			}

			return IsSceneDbName(scene.name, "transition") || IsSceneDbName(scene.name, "splashScreen");
		}

		private static bool IsFadeScreenShowingLoad()
		{
			if (!MonoSingleton<FadeScreen>.IsInstanceValid)
			{
				return false;
			}

			FadeScreen fade = MonoSingleton<FadeScreen>.Instance;
			if (fade == null)
			{
				return false;
			}

			if (fade.updating)
			{
				return true;
			}

			bool canvasUp = fade.canvasGroup != null && fade.canvasGroup.alpha > 0.05f;
			bool loadingUp = fade.contentCanvasGroup != null && fade.contentCanvasGroup.alpha > 0.05f;
			return canvasUp && loadingUp;
		}

		private static bool IsSceneDbName(string sceneName, string sceneDbKey)
		{
			string expected = SceneDB.GetScene(sceneDbKey);
			return !string.IsNullOrEmpty(expected)
				&& string.Equals(sceneName, expected, System.StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>If a scene transition this class never triggered itself silently destroyed its own scene while it thought it was still open (see EnsureBuilt's own remarks), drop the stale "open" state rather than let Close()/ShowMainView() touch now-destroyed Transforms.</summary>
		private static void RecoverIfSceneWasDestroyed()
		{
			if (isBuilt && !menuScene.IsValid())
			{
				isBuilt = false;
				isOpen = false;
			}
		}

		internal static void Open()
		{
			RecoverIfSceneWasDestroyed();

			if (isOpen || ModMenuAPI.HasBlockingOverlayOpen || IsLoadingScreenBlocking())
			{
				return;
			}

			EnsureBuilt();
			ShowMainView();
			RefreshButtons();
			SetRootsActive(true);
			BlockForeignEventSystems();

			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;

			isOpen = true;
		}

		internal static void Close()
		{
			RecoverIfSceneWasDestroyed();

			if (!isOpen)
			{
				return;
			}

			ShowMainView();
			SetRootsActive(false);
			RestoreForeignEventSystems();
			isOpen = false;
		}

		/// <summary>Always hides the menu and restores foreign EventSystems (e.g. after a scene load).</summary>
		internal static void ForceClose()
		{
			RecoverIfSceneWasDestroyed();
			ShowMainView();
			RestoreForeignEventSystems();
			if (isBuilt)
			{
				SetRootsActive(false);
			}
			isOpen = false;
		}

		internal static void ShowMainView()
		{
			if (mainView != null)
			{
				mainView.gameObject.SetActive(true);
			}
			if (submenuView != null)
			{
				submenuView.gameObject.SetActive(false);
			}
			activeSubmenuId = null;
		}

		private static void ShowSubmenu(ModMenuAPI.SubmenuEntry entry)
		{
			activeSubmenuId = entry.Id;
			mainView.gameObject.SetActive(false);
			submenuView.gameObject.SetActive(true);
			submenuTitle.text = entry.Label;

			if (!entry.HasBuilt)
			{
				ClearChildren(submenuContent);
				entry.BuildContent(submenuContent);
				entry.HasBuilt = true;
			}
		}

		/// <summary>Builds this overlay's own scene if it isn't already built and still valid.</summary>
		/// <remarks>menuScene.IsValid() is the real guard, not just isBuilt: LoadSceneMode.Single (used by every ordinary scene transition in this game, including Character Lab's/Ability Lab's own real scene-transition model) unloads EVERY currently loaded scene, not just whichever one is "active" -- so this scene can be silently destroyed by a transition this class never triggered itself and had no way to observe, leaving isBuilt=true pointing at dead GameObjects.</remarks>
		private static void EnsureBuilt()
		{
			if (isBuilt && menuScene.IsValid())
			{
				return;
			}

			menuScene = SceneManager.CreateScene(SceneName);
			BuildEventSystem(menuScene);
			BuildUI(menuScene);
			SetRootsActive(false);
			isBuilt = true;
		}

		private static void RefreshButtons()
		{
			ClearChildren(buttonContainer);

			List<ModMenuAPI.Entry> sorted = new List<ModMenuAPI.Entry>(ModMenuAPI.Entries);
			sorted.Sort(CompareEntries);

			if (sorted.Count == 0)
			{
				CreateListLabel(buttonContainer, "EmptyLabel",
					"No mod menu entries registered yet.", Color.gray);
				return;
			}

			for (int i = 0; i < sorted.Count; i++)
			{
				ModMenuAPI.Entry entry = sorted[i];
				CreateMenuButton(buttonContainer, entry);
			}
		}

		private static int CompareEntries(ModMenuAPI.Entry a, ModMenuAPI.Entry b)
		{
			int order = a.SortOrder.CompareTo(b.SortOrder);
			if (order != 0)
			{
				return order;
			}
			return string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase);
		}

		private static void CreateMenuButton(Transform parent, ModMenuAPI.Entry entry)
		{
			if (entry is ModMenuAPI.ButtonEntry buttonEntry)
			{
				CreateButton(parent, "Btn_" + entry.Id, entry.Label, () =>
				{
					if (buttonEntry.CloseOnClick)
					{
						Close();
					}
					buttonEntry.OnClick?.Invoke();
				});
				return;
			}

			if (entry is ModMenuAPI.SubmenuEntry submenuEntry)
			{
				CreateButton(parent, "Btn_" + entry.Id, entry.Label + " \u203a", () =>
				{
					ShowSubmenu(submenuEntry);
				});
			}
		}

		private static void SetRootsActive(bool active)
		{
			if (!isBuilt)
			{
				return;
			}
			foreach (GameObject root in menuScene.GetRootGameObjects())
			{
				root.SetActive(active);
			}
		}

		private static void BlockForeignEventSystems()
		{
			disabledEventSystems.Clear();
			EventSystem[] systems = Object.FindObjectsOfType<EventSystem>();
			foreach (EventSystem system in systems)
			{
				if (system == null || system.gameObject.scene == menuScene)
				{
					continue;
				}
				if (system.enabled)
				{
					system.enabled = false;
					disabledEventSystems.Add(system);
				}
			}
		}

		private static void RestoreForeignEventSystems()
		{
			for (int i = disabledEventSystems.Count - 1; i >= 0; i--)
			{
				EventSystem system = disabledEventSystems[i];
				if (system != null)
				{
					system.enabled = true;
				}
			}
			disabledEventSystems.Clear();
		}

		private static void BuildEventSystem(Scene scene)
		{
			GameObject eventSystemObject = new GameObject(
				"ModMenuEventSystem",
				typeof(EventSystem),
				typeof(StandaloneInputModule),
				typeof(ModMenuInputHandler));
			SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
		}

		private static void BuildUI(Scene scene)
		{
			GameObject canvasObject = new GameObject(
				"ModMenuCanvas",
				typeof(Canvas),
				typeof(CanvasScaler),
				typeof(GraphicRaycaster));
			Canvas canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = CanvasSortOrder;
			CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			SceneManager.MoveGameObjectToScene(canvasObject, scene);

			CreateBackdrop(canvasObject.transform);
			BuildMainPanel(canvasObject.transform);
			BuildSubmenuPanel(canvasObject.transform);
		}

		private static void CreateBackdrop(Transform parent)
		{
			GameObject backdropObject = new GameObject("Backdrop", typeof(Image), typeof(Button));
			backdropObject.transform.SetParent(parent, false);
			RectTransform rect = backdropObject.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			Image image = backdropObject.GetComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0.55f);

			Button button = backdropObject.GetComponent<Button>();
			button.onClick.AddListener(Close);
			backdropObject.transform.SetAsFirstSibling();
		}

		private static void BuildMainPanel(Transform parent)
		{
			GameObject panelObject = CreatePanelShell(parent, "MainPanel", new Vector2(420f, 520f));
			mainView = panelObject.transform;

			VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(20, 20, 20, 16);
			layout.spacing = 12f;
			layout.childAlignment = TextAnchor.UpperCenter;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			CreateHeaderBlock(mainView, "LoKR Mods", "Select a tool");

			GameObject scrollObject = CreateScrollArea(mainView, out buttonContainer);
			LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
			scrollLayout.flexibleHeight = 1f;
			scrollLayout.minHeight = 180f;

			CreateFooterButton(mainView, "CloseButton", "Close", Close);
		}

		private static void BuildSubmenuPanel(Transform parent)
		{
			GameObject panelObject = CreatePanelShell(parent, "SubmenuPanel", new Vector2(460f, 560f));
			submenuView = panelObject.transform;
			submenuView.gameObject.SetActive(false);

			VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
			layout.padding = new RectOffset(20, 20, 16, 16);
			layout.spacing = 10f;
			layout.childAlignment = TextAnchor.UpperCenter;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			GameObject backRow = new GameObject("BackRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			backRow.transform.SetParent(submenuView, false);
			LayoutElement backRowLayout = backRow.GetComponent<LayoutElement>();
			backRowLayout.minHeight = 36f;
			backRowLayout.preferredHeight = 36f;
			HorizontalLayoutGroup backLayout = backRow.GetComponent<HorizontalLayoutGroup>();
			backLayout.childAlignment = TextAnchor.MiddleLeft;
			backLayout.childControlWidth = false;
			backLayout.childControlHeight = true;

			CreateInlineButton(backRow.transform, "BackButton", "\u2039 Back", ShowMainView, 120f);

			submenuTitle = CreateHeaderText(submenuView, "SubmenuTitle", string.Empty, 22);
			LayoutElement titleLayout = submenuTitle.gameObject.AddComponent<LayoutElement>();
			titleLayout.minHeight = 32f;
			titleLayout.preferredHeight = 32f;

			GameObject contentObject = new GameObject("SubmenuContent", typeof(RectTransform), typeof(LayoutElement));
			contentObject.transform.SetParent(submenuView, false);
			LayoutElement contentLayout = contentObject.GetComponent<LayoutElement>();
			contentLayout.flexibleHeight = 1f;
			contentLayout.minHeight = 200f;
			RectTransform contentRect = contentObject.GetComponent<RectTransform>();
			contentRect.anchorMin = Vector2.zero;
			contentRect.anchorMax = Vector2.one;
			submenuContent = contentObject.transform;
		}

		private static GameObject CreatePanelShell(Transform parent, string name, Vector2 size)
		{
			GameObject panelObject = new GameObject(name, typeof(Image), typeof(Outline));
			panelObject.transform.SetParent(parent, false);
			RectTransform rect = panelObject.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = size;
			rect.anchoredPosition = Vector2.zero;
			panelObject.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.13f, 0.98f);
			Outline outline = panelObject.GetComponent<Outline>();
			outline.effectColor = new Color(0.45f, 0.55f, 0.75f, 0.55f);
			outline.effectDistance = new Vector2(1f, -1f);
			return panelObject;
		}

		private static void CreateHeaderBlock(Transform parent, string title, string subtitle)
		{
			GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
			headerObject.transform.SetParent(parent, false);
			LayoutElement headerLayout = headerObject.GetComponent<LayoutElement>();
			headerLayout.minHeight = 64f;
			headerLayout.preferredHeight = 64f;
			VerticalLayoutGroup headerGroup = headerObject.GetComponent<VerticalLayoutGroup>();
			headerGroup.spacing = 4f;
			headerGroup.childAlignment = TextAnchor.UpperCenter;
			headerGroup.childControlWidth = true;
			headerGroup.childControlHeight = true;
			headerGroup.childForceExpandWidth = true;
			headerGroup.childForceExpandHeight = false;

			Text titleText = CreateHeaderText(headerObject.transform, "TitleLabel", title, 26);
			LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
			titleLayout.minHeight = 32f;

			Text subtitleText = CreateHeaderText(headerObject.transform, "SubtitleLabel", subtitle, 16);
			subtitleText.color = new Color(0.78f, 0.82f, 0.88f);
			LayoutElement subtitleLayout = subtitleText.gameObject.AddComponent<LayoutElement>();
			subtitleLayout.minHeight = 22f;
		}

		private static Text CreateHeaderText(Transform parent, string name, string text, int fontSize)
		{
			GameObject labelObject = new GameObject(name, typeof(Text));
			labelObject.transform.SetParent(parent, false);
			Text label = labelObject.GetComponent<Text>();
			label.text = text;
			label.font = defaultFont;
			label.fontSize = fontSize;
			label.alignment = TextAnchor.MiddleCenter;
			label.color = Color.white;
			return label;
		}

		private static GameObject CreateScrollArea(Transform parent, out Transform contentTransform)
		{
			GameObject scrollObject = new GameObject("ButtonScroll", typeof(Image), typeof(ScrollRect));
			scrollObject.transform.SetParent(parent, false);
			scrollObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

			GameObject viewportObject = new GameObject("Viewport", typeof(RectMask2D), typeof(Image));
			viewportObject.transform.SetParent(scrollObject.transform, false);
			RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = new Vector2(4f, 4f);
			viewportRect.offsetMax = new Vector2(-4f, -4f);
			viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

			GameObject contentObject = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			contentObject.transform.SetParent(viewportObject.transform, false);
			RectTransform contentRect = contentObject.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.offsetMin = new Vector2(0f, 0f);
			contentRect.offsetMax = new Vector2(0f, 0f);

			VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
			layout.spacing = 8f;
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.childControlHeight = true;
			layout.childControlWidth = true;
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;

			ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
			scroll.content = contentRect;
			scroll.viewport = viewportRect;
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;

			contentTransform = contentObject.transform;
			return scrollObject;
		}

		private static void CreateFooterButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
		{
			GameObject footerObject = new GameObject("Footer", typeof(LayoutElement));
			footerObject.transform.SetParent(parent, false);
			LayoutElement footerLayout = footerObject.GetComponent<LayoutElement>();
			footerLayout.minHeight = 44f;
			footerLayout.preferredHeight = 44f;
			CreateInlineButton(footerObject.transform, name, label, onClick, 0f, stretch: true);
		}

		private static void CreateInlineButton(Transform parent, string name, string label,
			UnityEngine.Events.UnityAction onClick, float width, bool stretch = false)
		{
			GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button), typeof(LayoutElement));
			buttonObject.transform.SetParent(parent, false);
			LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
			layoutElement.minHeight = 44f;
			layoutElement.preferredHeight = 44f;
			if (stretch)
			{
				layoutElement.flexibleWidth = 1f;
			}
			else if (width > 0f)
			{
				layoutElement.preferredWidth = width;
				layoutElement.minWidth = width;
			}

			Image image = buttonObject.GetComponent<Image>();
			image.color = new Color(0.18f, 0.36f, 0.68f);

			Button button = buttonObject.GetComponent<Button>();
			button.onClick.AddListener(onClick);

			GameObject labelObject = new GameObject("Label", typeof(Text));
			labelObject.transform.SetParent(buttonObject.transform, false);
			RectTransform labelRect = labelObject.GetComponent<RectTransform>();
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = new Vector2(12f, 0f);
			labelRect.offsetMax = new Vector2(-12f, 0f);

			Text labelText = labelObject.GetComponent<Text>();
			labelText.text = label;
			labelText.font = defaultFont;
			labelText.fontSize = 18;
			labelText.alignment = TextAnchor.MiddleCenter;
			labelText.color = Color.white;
		}

		private static void CreateListLabel(Transform parent, string name, string text, Color color)
		{
			GameObject labelObject = new GameObject(name, typeof(Text), typeof(LayoutElement));
			labelObject.transform.SetParent(parent, false);
			LayoutElement layoutElement = labelObject.GetComponent<LayoutElement>();
			layoutElement.minHeight = 48f;
			layoutElement.preferredHeight = 48f;

			Text label = labelObject.GetComponent<Text>();
			label.text = text;
			label.font = defaultFont;
			label.fontSize = 16;
			label.alignment = TextAnchor.MiddleCenter;
			label.color = color;
		}

		private static void CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick,
			Vector2? anchorCenter = null, Vector2? size = null)
		{
			CreateInlineButton(parent, name, label, onClick, 0f, stretch: anchorCenter == null && size == null);
		}

		private static void ClearChildren(Transform parent)
		{
			for (int i = parent.childCount - 1; i >= 0; i--)
			{
				Object.Destroy(parent.GetChild(i).gameObject);
			}
		}
	}
}
