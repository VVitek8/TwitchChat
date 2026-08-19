using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PeakTextChat;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PeakTextChatPlugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;
    Harmony harmony = null!;

    // Display
    public static ConfigEntry<float> configFontSize = null!;
    public static ConfigEntry<string> configChatSize = null!;
    public static ConfigEntry<float> configMessageFadeDelay = null!;
    public static ConfigEntry<float> configFadeDelay = null!;
    public static ConfigEntry<float> configHideDelay = null!;
    public static ConfigEntry<KeyCodeShort> configKey = null!;
    public static ConfigEntry<TextChatPosition> configPos = null!;
    public static ConfigEntry<bool> configRichTextEnabled = null!;
    public static ConfigEntry<bool> configIMGUI = null!;
    public static ConfigEntry<float> configBgOpacity = null!;
    public static ConfigEntry<bool> configFrameVisible = null!;

    // Quick Messages
    public static ConfigEntry<string> QuickMessages = null!;

    // Twitch
    public static ConfigEntry<string> TwitchUsername = null!;
    public static ConfigEntry<string> AllowedSenders = null!;
    public static ConfigEntry<bool> UseSteamName = null!;
    public static ConfigEntry<bool> SendWithoutPrefix = null!;

    // Auth
    public static ConfigEntry<string> TwitchOAuth = null!;

    // Словарь для быстрых сообщений
    private Dictionary<KeyCode, string> quickMessageMap = new Dictionary<KeyCode, string>();

    // Для предупреждения в главном меню
    private GameObject warningObject = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"PeakTextChat is loaded!");

        // ===== DISPLAY =====
        configKey = Config.Bind<KeyCodeShort>("Display", "ChatKey", KeyCodeShort.Slash, "The key that activates typing in chat");
        configIMGUI = Config.Bind<bool>("Display", "UseIMGUI", false, "Use IMGUI for the text field");
        configPos = Config.Bind<TextChatPosition>("Display", "ChatPosition", TextChatPosition.BottomLeft, "The position of the text chat");
        configChatSize = Config.Bind<string>("Display", "ChatSize", "500:300", "The size of the text chat (formatted X:Y)");
        configFontSize = Config.Bind<float>("Display", "ChatFontSize", 20f, "Size of the chat's text");
        configBgOpacity = Config.Bind<float>("Display", "ChatBackgroundOpacity", 0.3f, "Opacity of the chat's background/shadow");
        configFrameVisible = Config.Bind<bool>("Display", "ChatFrameVisible", true, "Whether the frame of the chat box is visible");
        configRichTextEnabled = Config.Bind<bool>("Display", "ChatRichText", true, "Whether rich text tags get parsed in messages");
        configFadeDelay = Config.Bind<float>("Display", "ChatFadeDelay", 15f, "How long before the chat fades out");
        configHideDelay = Config.Bind<float>("Display", "ChatHideDelay", 40f, "How long before the chat hides completely");
        configMessageFadeDelay = Config.Bind<float>("Display", "ChatMessageHideDelay", 40f, "How long before a chat message disappears");

        // ===== QUICK MESSAGES =====
        QuickMessages = Config.Bind(
            "QuickMessages",
            "QuickMessages",
            "",
            "Format: 'Key$Message:::Key2$Message2'. Keys are Unity KeyCode names (e.g., F1, G, NUM4, J, Alpha1).\n" +
            "Example: F1$Hello!:::G$Follow me:::NUM4$Look:::J$!chat see??"
        );

        // ===== TWITCH (visible) =====
        TwitchUsername = Config.Bind("Twitch", "Username", "your_username", "Twitch channel to read and send messages to.");
        AllowedSenders = Config.Bind("Twitch", "AllowedSenders", "", "Steam IDs (comma separated) allowed to send messages to Twitch. Empty = only host.");
        UseSteamName = Config.Bind("Twitch", "UseSteamName", true, "If true, shows Steam name in Twitch. If false, hides the name.");
        SendWithoutPrefix = Config.Bind("Twitch", "SendWithoutPrefix", false, "If true, messages without '!' go to Twitch, with '!' go only to game chat.");

        // ===== AUTH (hidden) =====
        TwitchOAuth = Config.Bind("Auth", "OAuthToken", "oauth:s1o2m3e4t5o6k7e8n9", "OAuth token for Twitch (required for sending messages). Get it at twitchtokengenerator.com.");

        // Парсим быстрые сообщения
        ParseQuickMessages();

        // Подписываемся на загрузку сцен для предупреждения в главном меню
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Создаём TwitchIntegration
        var twitchComponent = gameObject.AddComponent<TwitchIntegration>();
        if (twitchComponent != null)
            Logger.LogInfo("[PeakTextChatPlugin] TwitchIntegration component successfully added!");
        else
            Logger.LogError("[PeakTextChatPlugin] Failed to add TwitchIntegration!");

        harmony = new Harmony("com.borealityy.peaktextchat");
        harmony.PatchAll(typeof(GameUtilsPatch));
        harmony.PatchAll(typeof(StaminaBarPatch));
        harmony.PatchAll(typeof(GUIManagerPatch));
        harmony.PatchAll(typeof(InputBlockingPatches));
    }

    private void Update()
    {
        // Обрабатываем быстрые сообщения
        if (quickMessageMap.Count > 0 && TextChatManager.instance != null)
        {
            foreach (var kvp in quickMessageMap)
            {
                if (Input.GetKeyDown(kvp.Key))
                {
                    TextChatManager.instance.SendChatMessage(kvp.Value);
                    Logger.LogInfo($"[PeakTextChatPlugin] Quick message sent: '{kvp.Value}' (key: {kvp.Key})");
                }
            }
        }
    }

    // ===== ПАРСИНГ БЫСТРЫХ СООБЩЕНИЙ =====
    private void ParseQuickMessages()
    {
        quickMessageMap.Clear();
        string raw = QuickMessages.Value.Trim();
        if (string.IsNullOrEmpty(raw))
            return;

        string[] commands = raw.Split(new[] { ":::" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string cmd in commands)
        {
            int dollarIndex = cmd.IndexOf('$');
            if (dollarIndex <= 0 || dollarIndex >= cmd.Length - 1)
            {
                Logger.LogWarning($"[PeakTextChatPlugin] Invalid quick message format (missing $ or empty): '{cmd}'");
                continue;
            }

            string keyString = cmd.Substring(0, dollarIndex).Trim();
            string message = cmd.Substring(dollarIndex + 1).Trim();

            if (string.IsNullOrEmpty(keyString) || string.IsNullOrEmpty(message))
            {
                Logger.LogWarning($"[PeakTextChatPlugin] Empty key or message in: '{cmd}'");
                continue;
            }

            if (Enum.TryParse<KeyCode>(keyString, true, out KeyCode key))
            {
                quickMessageMap[key] = message;
                Logger.LogInfo($"[PeakTextChatPlugin] Quick message registered: {key} -> '{message}'");
            }
            else
            {
                Logger.LogWarning($"[PeakTextChatPlugin] Unknown key '{keyString}' in quick messages.");
            }
        }
    }

    // ===== ПРЕДУПРЕЖДЕНИЕ В ГЛАВНОМ МЕНЮ =====
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" || scene.name == "Menu")
        {
            ShowUsernameWarningIfNeeded();
        }
        else
        {
            HideUsernameWarning();
        }
    }

    private void ShowUsernameWarningIfNeeded()
    {
        string username = TwitchUsername.Value;
        bool isInvalid = string.IsNullOrEmpty(username) || username == "your_username";

        if (!isInvalid)
        {
            HideUsernameWarning();
            return;
        }

        if (warningObject != null)
            return;

        GameObject canvasObj = new GameObject("TwitchWarningCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject textObj = new GameObject("WarningText");
        textObj.transform.SetParent(canvasObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = "⚠️ Twitch username not set!\nPlease edit BepInEx/config/V8.TwitchChat.cfg";
        text.color = Color.red;
        text.fontSize = 48;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        warningObject = canvasObj;
        DontDestroyOnLoad(canvasObj);
    }

    private void HideUsernameWarning()
    {
        if (warningObject != null)
        {
            Destroy(warningObject);
            warningObject = null!;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (TextChatDisplay.instance != null)
            Destroy(TextChatDisplay.instance.gameObject);
        if (GUIManagerPatch.textChatCanvas != null)
            Destroy(GUIManagerPatch.textChatCanvas);

        TextChatManager.CleanupObjects();
        StaminaBarPatch.CleanupObjects();

        harmony.UnpatchSelf();
    }

    public enum TextChatPosition
    {
        BottomLeft,
        TopLeft,
        TopRight
    }
}