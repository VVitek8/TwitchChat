using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PeakTextChat;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PeakTextChatPlugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null!;
    Harmony harmony = null!;

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

    public static ConfigEntry<string> TwitchUsername = null!;
    public static ConfigEntry<string> TwitchOAuth = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"PeakTextChat is loaded!");

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

        TwitchUsername = Config.Bind("Twitch", "Username", "your_username", "Twitch username");
        TwitchOAuth = Config.Bind("Twitch", "OAuthToken", "oauth:s1o2m3e4t5o6k7e8n9", "(optional) OAuth token (get it at twitchtokengenerator.com)");

        var twitchComponent = gameObject.AddComponent<TwitchIntegration>();
        if (twitchComponent != null)
            Logger.LogInfo("[PeakTextChatPlugin] TwitchIntegration компонент успешно добавлен!");
        else
            Logger.LogError("[PeakTextChatPlugin] НЕ УДАЛОСЬ добавить TwitchIntegration!");

        harmony = new Harmony("com.borealityy.peaktextchat");
        harmony.PatchAll(typeof(GameUtilsPatch));
        harmony.PatchAll(typeof(StaminaBarPatch));
        harmony.PatchAll(typeof(GUIManagerPatch));
        harmony.PatchAll(typeof(InputBlockingPatches));
    }

    private void OnDestroy()
    {
        if (TextChatDisplay.instance != null)
            GameObject.Destroy(TextChatDisplay.instance.gameObject);
        if (GUIManagerPatch.textChatCanvas != null)
            GameObject.Destroy(GUIManagerPatch.textChatCanvas);

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