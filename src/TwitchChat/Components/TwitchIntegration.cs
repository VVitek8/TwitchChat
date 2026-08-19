using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Client.Events;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace PeakTextChat
{
    public class TwitchIntegration : MonoBehaviour
    {
        private static TwitchIntegration instance = null!;
        private TwitchClient client = null!;
        private string channelName = null!;
        private bool isConnected = false;

        public static string TokenOwnerName = "";

        private static readonly Queue<Action> mainThreadActions = new Queue<Action>();
        private readonly object queueLock = new object();
        private static readonly Queue<string> pendingSystemMessages = new Queue<string>();

        private static readonly string[] DefaultColors = {
            "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50",
            "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E",
            "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F"
        };

        public static string GetColorForName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2)
                return "#FFFFFF";
            int hash = name[0] + name[name.Length - 1];
            int index = hash % DefaultColors.Length;
            return DefaultColors[index];
        }

        private void Awake()
        {
            instance = this;
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Awake()");
        }

        private void OnEnable() => PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] OnEnable()");

        private void Update()
        {
            lock (queueLock)
            {
                while (mainThreadActions.Count > 0)
                {
                    var action = mainThreadActions.Dequeue();
                    try { action?.Invoke(); }
                    catch (Exception ex) { PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] UI thread error: {ex.Message}"); }
                }
            }

            // Отображаем отложенные системные сообщения, как только появится чат
            if (TextChatDisplay.instance != null && pendingSystemMessages.Count > 0)
            {
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Displaying {pendingSystemMessages.Count} pending system messages.");
                while (pendingSystemMessages.Count > 0)
                {
                    string msg = pendingSystemMessages.Dequeue();
                    TextChatDisplay.instance.AddMessage(msg);
                    PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Added system message to chat: {msg}");
                }
            }
        }

        private void Start()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Start()");

            string username = PeakTextChatPlugin.TwitchUsername.Value;
            string oauth = PeakTextChatPlugin.TwitchOAuth.Value;
            channelName = username;

            if (string.IsNullOrEmpty(username) || username == "your_username")
            {
                ShowSystemError("Twitch username not set! Please set it in the config (BepInEx/config/V8.TwitchChat.cfg).");
                PeakTextChatPlugin.Logger.LogError("[TwitchIntegration] Username not set.");
                return;
            }

            if (string.IsNullOrEmpty(channelName))
            {
                ShowSystemError("Channel name is empty. Please set Username in the config.");
                PeakTextChatPlugin.Logger.LogError("[TwitchIntegration] Channel name is empty.");
                return;
            }

            bool hasValidToken = !string.IsNullOrEmpty(oauth) && oauth != "oauth:s1o2m3e4t5o6k7e8n9" && oauth.Length >= 5;

            if (!hasValidToken)
            {
                ShowSystemWarning("No OAuth token. Read-only mode (anonymous). To send messages, set a token in the config.");
                PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] No valid token, falling back to anonymous.");
                StartCoroutine(ConnectAnonymous(username, channelName));
                return;
            }

            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Token is valid, attempting connection...");
            StartCoroutine(ConnectAndWait(username, oauth!, channelName, 10f));
        }

        private void ShowSystemSuccess(string message)
        {
            string formatted = $"<color=#00FF00>[System]</color> <color=#88FF88>{message}</color>";
            pendingSystemMessages.Enqueue(formatted);
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System success enqueued: {message}");
            if (TextChatDisplay.instance != null)
            {
                TextChatDisplay.instance.AddMessage(formatted);
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System success added directly (chat exists).");
            }
        }

        private void ShowSystemError(string message)
        {
            string formatted = $"<color=#FF4444>[System]</color> <color=#FF8888>{message}</color>";
            pendingSystemMessages.Enqueue(formatted);
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System error enqueued: {message}");
            if (TextChatDisplay.instance != null)
            {
                TextChatDisplay.instance.AddMessage(formatted);
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System error added directly (chat exists).");
            }
        }

        private void ShowSystemWarning(string message)
        {
            string formatted = $"<color=#FFAA00>[System]</color> <color=#FFCC88>{message}</color>";
            pendingSystemMessages.Enqueue(formatted);
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System warning enqueued: {message}");
            if (TextChatDisplay.instance != null)
            {
                TextChatDisplay.instance.AddMessage(formatted);
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] System warning added directly (chat exists).");
            }
        }

        private IEnumerator ConnectAnonymous(string username, string channel)
        {
            ShowSystemWarning($"Connecting to Twitch channel '{channel}' in anonymous mode...");
            string anonymousNick = "justinfan12345";
            string anonymousPass = "";
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Trying anonymous connection with nick {anonymousNick}");
            yield return StartCoroutine(ConnectAndWait(anonymousNick, anonymousPass, channel, 10f));
            if (isConnected)
            {
                PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Anonymous connection successful! Chat reading.");
                TokenOwnerName = "Anonymous";
                ShowSystemSuccess($"Connected to Twitch channel '{channel}' in anonymous mode (read-only).");
            }
            else
            {
                PeakTextChatPlugin.Logger.LogError("[TwitchIntegration] Anonymous connection failed. Please provide OAuth token.");
                ShowSystemError($"Failed to connect to Twitch channel '{channel}' anonymously. Check your internet and try again.");
            }
        }

        private IEnumerator ConnectAndWait(string username, string oauth, string channel, float timeout)
        {
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] ConnectAndWait started. username={username}, channel={channel}, timeout={timeout}");
            isConnected = false;
            TwitchClient tempClient = null!;

            try
            {
                var credentials = new ConnectionCredentials(username, oauth);
                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 20,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };
                var customClient = new WebSocketClient(clientOptions);
                tempClient = new TwitchClient(customClient);

                tempClient.OnConnected += (s, e) =>
                {
                    PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] OnConnected: Connected to Twitch! Channel: {e.AutoJoinChannel}");
                    isConnected = true;
                    TokenOwnerName = username;
                };
                tempClient.OnJoinedChannel += (s, e) =>
                {
                    PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] OnJoinedChannel: Joined channel {e.Channel}");
                };
                tempClient.OnMessageReceived += OnMessageReceived;
                tempClient.OnError += OnError;

                tempClient.Initialize(credentials, channel);
                tempClient.Connect();
            }
            catch (Exception ex)
            {
                PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Initialization error: {ex.Message}");
                tempClient?.Disconnect();
                yield break;
            }

            float elapsed = 0f;
            while (!isConnected && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (isConnected)
            {
                client = tempClient;
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Connection established in {elapsed:F1} sec.");
                ShowSystemSuccess($"Connected to Twitch channel '{channel}' with OAuth token.");
            }
            else
            {
                PeakTextChatPlugin.Logger.LogWarning($"[TwitchIntegration] Timeout {timeout} sec. Failed to connect.");
                tempClient?.Disconnect();
                ShowSystemError($"Failed to connect to Twitch channel '{channel}'. Check your token and internet.");
            }
        }

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string displayName = e.ChatMessage.DisplayName;
            string message = e.ChatMessage.Message;
            string colorHex = GetUserColor(displayName, e.ChatMessage.ColorHex);

            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] {displayName}: {message}");

            lock (queueLock)
            {
                mainThreadActions.Enqueue(() =>
                {
                    try
                    {
                        if (TextChatDisplay.instance != null)
                            TextChatDisplay.instance.AddTwitchMessage(displayName, colorHex, message);
                        else
                            PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] TextChatDisplay missing");
                    }
                    catch (Exception ex)
                    {
                        PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Error adding message: {ex.Message}");
                    }
                });
            }
        }

        private string GetUserColor(string displayName, string colorHex)
        {
            if (!string.IsNullOrEmpty(colorHex))
                return colorHex;

            if (string.IsNullOrEmpty(displayName) || displayName.Length < 2)
                return "#FFFFFF";

            int hash = displayName[0] + displayName[displayName.Length - 1];
            int index = hash % DefaultColors.Length;
            return DefaultColors[index];
        }

        private void OnError(object sender, EventArgs e)
        {
            try
            {
                var exProp = e.GetType().GetProperty("Exception");
                if (exProp != null)
                {
                    var ex = exProp.GetValue(e) as Exception;
                    if (ex != null)
                        PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] TwitchLib error: {ex.Message}");
                    else
                        PeakTextChatPlugin.Logger.LogError("[TwitchIntegration] Error, but Exception is null.");
                }
                else
                {
                    PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Unknown error. Type: {e.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Error in OnError handler: {ex.Message}");
            }
        }

        public static void SendToTwitch(string message)
        {
            if (instance == null)
            {
                PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] Instance not created!");
                return;
            }
            instance.SendMessageToTwitch(message);
        }

        public void SendMessageToTwitch(string message)
        {
            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] SendMessageToTwitch called. client={client != null}, IsConnected={client?.IsConnected ?? false}, channelName={channelName}");

            if (client != null && client.IsConnected && !string.IsNullOrEmpty(channelName))
            {
                if (PeakTextChatPlugin.TwitchOAuth.Value == "oauth:s1o2m3e4t5o6k7e8n9" ||
                    string.IsNullOrEmpty(PeakTextChatPlugin.TwitchOAuth.Value))
                {
                    PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] No rights to send (anonymous mode)");
                    return;
                }
                client.SendMessage(channelName, message);
                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Sent to Twitch: {message}");
            }
            else
            {
                PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] Client not ready to send");
            }
        }

        private void OnDestroy()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] OnDestroy()");
            try
            {
                client?.Disconnect();
            }
            catch (Exception ex)
            {
                PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Disconnect error: {ex.Message}");
            }
        }
    }
}