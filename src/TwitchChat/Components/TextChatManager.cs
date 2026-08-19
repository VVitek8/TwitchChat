using System;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakTextChat;

public class TextChatManager : MonoBehaviour
{
    public static TextChatManager instance = null!;
    Character character = null!;
    byte chatEventCode = 81;
    private PhotonView photonView = null!;

    void Start()
    {
        instance = this;
        character = GetComponent<Character>();
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            photonView = gameObject.AddComponent<PhotonView>();
            photonView.ViewID = 1000;
        }
        PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] Start()");
    }

    void OnEnable() => PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
    void OnDisable() => PhotonNetwork.NetworkingClient.EventReceived -= OnEventReceived;

    void OnEventReceived(EventData eventData)
    {
        if (eventData.Code != chatEventCode) return;
        var data = (object[])eventData.CustomData;
        if (data.Length < 4) return;
        string userId = data[2]?.ToString() ?? "";
        string message = data[1]?.ToString() ?? "";
        bool isDead = bool.TryParse(data[3]?.ToString(), out var d) && d;
        ReceiveChatMessage(userId, message, isDead);
        PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Event received: userId={userId}, message='{message}'");
    }

    public void ReceiveChatMessage(string userId, string message, bool isDead)
    {
        if (TextChatDisplay.instance != null)
            TextChatDisplay.instance.AddMessage(new Message(userId, message, isDead));
    }

    [PunRPC]
    private void RPCSendTwitchMessage(string twitchMessage, string steamName, string cleanMessage, PhotonMessageInfo info)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            TwitchIntegration.SendToTwitch(twitchMessage);
            PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] RPC: Host sent: {twitchMessage}");
            
            ShowLocalConfirmation(cleanMessage, steamName);
        }
    }

    private void ShowLocalConfirmation(string message, string steamName)
    {
        if (TextChatDisplay.instance == null) return;
        string displaySteamName = string.IsNullOrEmpty(steamName) ? "Player" : steamName;
        Color playerColor = Character.localCharacter?.refs?.customization?.PlayerColor ?? new Color(0.87f, 0.85f, 0.76f);
        string colorHex = $"#{ColorUtility.ToHtmlStringRGB(playerColor)}";
        string confirmMessage = $"<color=#9147FF>[Twitch]</color> <color={colorHex}>[{displaySteamName}]</color>: {message}";
        TextChatDisplay.instance.AddMessage(confirmMessage);
        PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Local confirmation: '{confirmMessage}'");
    }

    public void SendChatMessage(string message)
    {
        PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] SendChatMessage called: '{message}'");
        if (string.IsNullOrWhiteSpace(message)) return;

        bool isTwitchCommand = message.StartsWith("!");
        string cleanMessage = isTwitchCommand ? message.Substring(1).Trim() : message;

        if (string.IsNullOrEmpty(cleanMessage))
            return;

        bool sendWithoutPrefix = PeakTextChatPlugin.SendWithoutPrefix.Value;
        bool useSteamName = PeakTextChatPlugin.UseSteamName.Value;

        bool shouldSendToTwitch = sendWithoutPrefix ? !isTwitchCommand : isTwitchCommand;

        if (shouldSendToTwitch)
        {
            string oauth = PeakTextChatPlugin.TwitchOAuth.Value;
            bool hasValidToken = !string.IsNullOrEmpty(oauth) && oauth != "oauth:s1o2m3e4t5o6k7e8n9" && oauth.Length >= 5;

            string userId = PhotonNetwork.LocalPlayer.UserId;
            bool isHost = PhotonNetwork.IsMasterClient;

            bool canSend = false;
            string sendMode = "";

            if (isHost && hasValidToken)
            {
                canSend = true;
                sendMode = "host_own_token";
                PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] Host sending with own token.");
            }
            else if (!isHost && hasValidToken)
            {
                canSend = true;
                sendMode = "player_own_token";
                PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] Player has own token, sending from own account.");
            }
            else if (!isHost && !hasValidToken)
            {
                string allowedList = PeakTextChatPlugin.AllowedSenders.Value;
                if (!string.IsNullOrEmpty(allowedList))
                {
                    var allowedIds = allowedList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(id => id.Trim())
                                               .ToList();
                    if (allowedIds.Contains(userId))
                    {
                        canSend = true;
                        sendMode = "rpc_via_host";
                        PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] Player without token, but in allowed list. Sending via RPC to host.");
                    }
                    else
                    {
                        PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] User NOT in allowed list.");
                    }
                }
                else
                {
                    PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] AllowedSenders is empty.");
                }
            }

            if (!canSend)
            {
                string msg = "<color=#FF4444>[System]</color> <color=#FF8888>You don't have permission to send to Twitch. (No token and not in allowed list)</color>";
                if (TextChatDisplay.instance != null)
                    TextChatDisplay.instance.AddMessage(msg);
                PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Player {PhotonNetwork.LocalPlayer.NickName} has no permission to send.");
                return;
            }

            string steamName = PhotonNetwork.LocalPlayer.NickName;
            string tokenOwnerName = TwitchIntegration.TokenOwnerName;
            if (string.IsNullOrEmpty(tokenOwnerName))
                tokenOwnerName = "Twitch";

            // Формируем сообщение для Twitch (НЕ включаем tokenOwnerName, Twitch добавит его сам)
            string twitchMessage;
            if (useSteamName)
                twitchMessage = $"[{steamName}]: {cleanMessage}";
            else
                twitchMessage = cleanMessage;

            PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Twitch message to send: '{twitchMessage}' (token owner: {tokenOwnerName})");

            if (sendMode == "rpc_via_host")
            {
                if (photonView != null)
                {
                    photonView.RPC("RPCSendTwitchMessage", RpcTarget.MasterClient, twitchMessage, steamName, cleanMessage);
                    PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] RPC sent to host: {twitchMessage}");
                }
                else
                {
                    PeakTextChatPlugin.Logger.LogError("[TextChatManager] PhotonView is null, cannot send RPC.");
                    string msg = "<color=#FF4444>[System]</color> <color=#FF8888>Failed to send message to host.</color>";
                    if (TextChatDisplay.instance != null)
                        TextChatDisplay.instance.AddMessage(msg);
                }
                return;
            }

            if (hasValidToken)
            {
                TwitchIntegration.SendToTwitch(twitchMessage);
                PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Sent to Twitch: {twitchMessage}");

                ShowLocalConfirmation(cleanMessage, steamName);
            }
            else
            {
                string msg = "<color=#FF4444>[System]</color> <color=#FF8888>No OAuth token. Please set a valid token in the config.</color>";
                if (TextChatDisplay.instance != null)
                    TextChatDisplay.instance.AddMessage(msg);
                PeakTextChatPlugin.Logger.LogInfo("[TextChatManager] Token invalid, message shown.");
            }
        }
        else
        {
            // Обычное сообщение в игровой чат (не в Twitch)
            bool isDeadNormal = false;
            try { isDeadNormal = Character.localCharacter?.data?.dead ?? false; } catch { }
            object[] normalPayload = {
                PhotonNetwork.LocalPlayer.NickName,
                message,
                PhotonNetwork.LocalPlayer.UserId,
                isDeadNormal
            };
            PhotonNetwork.RaiseEvent(chatEventCode, normalPayload,
                new RaiseEventOptions() { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
            PeakTextChatPlugin.Logger.LogInfo($"[TextChatManager] Game chat message sent: '{message}'");
        }
    }

    public static void CleanupObjects()
    {
        if (instance != null) GameObject.Destroy(instance.gameObject);
    }

    public class Message
    {
        public Character character;
        public string message;
        public bool isDead;

        public Message(string userId, string message, bool isDead)
        {
            this.character = Character.AllCharacters.Find(c => c.photonView?.Owner?.UserId == userId);
            this.message = message;
            this.isDead = isDead;
        }
    }
}