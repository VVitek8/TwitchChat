using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PeakTextChat;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PeakTextChatPlugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
        
    Harmony harmony;

    public static ConfigEntry<float> configFontSize;
    public static ConfigEntry<string> configChatSize;
    public static ConfigEntry<float> configMessageFadeDelay;
    public static ConfigEntry<float> configFadeDelay;
    public static ConfigEntry<float> configHideDelay;
    public static ConfigEntry<KeyCodeShort> configKey;
    public static ConfigEntry<TextChatPosition> configPos;
    public static ConfigEntry<bool> configRichTextEnabled;
    public static ConfigEntry<bool> configIMGUI;
    public static ConfigEntry<float> configBgOpacity;
    public static ConfigEntry<bool> configFrameVisible;

    // === НОВЫЕ НАСТРОЙКИ ДЛЯ TWITCH ===
    public static ConfigEntry<string> TwitchUsername;
    public static ConfigEntry<string> TwitchOAuth;
    public static ConfigEntry<string> TwitchChannel;
    // ====================================

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"PeakTextChat is loaded!");

        // --- Оригинальные настройки ---
        configKey = Config.Bind<KeyCodeShort>(
                                "Display",
                                "ChatKey",
                                KeyCodeShort.Slash,
                                "The key that activates typing in chat"
                            );

        configIMGUI = Config.Bind<bool>(
                                "Display",
                                "UseIMGUI",
                                false,
                                "Use IMGUI for the text field (use if you're having problems with typing)"
            );

        configPos = Config.Bind<TextChatPosition>(
                                "Display",
                                "ChatPosition",
                                TextChatPosition.BottomLeft,
                                "The position of the text chat"
                            );

        configChatSize = Config.Bind<string>(
                                "Display",
                                "ChatSize",
                                "500:300",
                                "The size of the text chat (formatted X:Y)"
                            );

        configFontSize = Config.Bind<float>(
                                "Display",
                                "ChatFontSize",
                                20f,
                                "Size of the chat's text"
                            );

        configBgOpacity = Config.Bind<float>(
                                "Display",
                                "ChatBackgroundOpacity",
                                0.3f,
                                "Opacity of the chat's background/shadow"
                            );

        configFrameVisible = Config.Bind<bool>(
                                "Display",
                                "ChatFrameVisible",
                                true,
                                "Whether the frame of the chat box is visible"
                            );

        configRichTextEnabled = Config.Bind<bool>(
                                "Display",
                                "ChatRichText",
                                true,
                                "Whether rich text tags get parsed in messages (e.g. <b> for bold text)"
                            );

        configFadeDelay = Config.Bind<float>(
                                "Display",
                                "ChatFadeDelay",
                                15f,
                                "How long before the chat fades out (a negative number means never)"
                            );

        configHideDelay = Config.Bind<float>(
                                "Display",
                                "ChatHideDelay",
                                40f,
                                "How long before the chat hides completely (a negative number means never)"
                            );

        configMessageFadeDelay = Config.Bind<float>(
                                    "Display",
                                    "ChatMessageHideDelay",
                                    40f,
                                    "How long before a chat message disappears (a negative number means never)"
                                );

        // === НОВЫЕ НАСТРОЙКИ ДЛЯ TWITCH ===
        TwitchUsername = Config.Bind("Twitch", "Username", "your_username", "Twitch username");
        TwitchOAuth = Config.Bind("Twitch", "OAuthToken", "oauth:s1o2m3e4t5o6k7e8n9", "OAuth token (get it at twitchtokengenerator.com)");
        //TwitchChannel = Config.Bind("Twitch", "Channel", "your_channel", "Имя канала для чтения (обычно совпадает с Username)");
        // ==================================

        // Создаём объект для Twitch-интеграции
        //GameObject go = new GameObject("TwitchIntegration");
        //DontDestroyOnLoad(go);
        //go.AddComponent<TwitchIntegration>();
        var twitchComponent = gameObject.AddComponent<TwitchIntegration>();
        if (twitchComponent != null)
        {
            Logger.LogInfo("[PeakTextChatPlugin] TwitchIntegration компонент успешно добавлен к плагину!");
        }
        else
        {
            Logger.LogError("[PeakTextChatPlugin] НЕ УДАЛОСЬ добавить TwitchIntegration!");
        }

        // Патчи Harmony
        harmony = new Harmony("com.borealityy.peaktextchat");
        harmony.PatchAll(typeof(GameUtilsPatch));
        harmony.PatchAll(typeof(StaminaBarPatch));
        harmony.PatchAll(typeof(GUIManagerPatch));
        harmony.PatchAll(typeof(InputBlockingPatches));
    }

    private void OnDestroy() {
        if (TextChatDisplay.instance != null)
            GameObject.Destroy(TextChatDisplay.instance.gameObject);
        if (GUIManagerPatch.textChatCanvas != null)
            GameObject.Destroy(GUIManagerPatch.textChatCanvas);
            
        TextChatManager.CleanupObjects();
        StaminaBarPatch.CleanupObjects();

        harmony.UnpatchSelf();
    }

    public enum TextChatPosition {
        BottomLeft,
        TopLeft,
        TopRight
    }
}