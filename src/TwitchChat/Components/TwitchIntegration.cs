using UnityEngine;
using TwitchChatAPI;

namespace PeakTextChat
{
    public class TwitchIntegration : MonoBehaviour
    {
        private void Awake()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Awake() вызван!");
        }

        private void OnEnable()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] OnEnable() вызван!");
        }

        private void Start()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Start() вызван!");

            // Тестовое сообщение — чтобы убедиться, что чат работает
            if (TextChatDisplay.instance != null)
            {
                TextChatDisplay.instance.AddMessage("[Twitch] Интеграция запущена! Проверка связи.");
                PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Тестовое сообщение отправлено в чат.");
            }
            else
            {
                PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] TextChatDisplay.instance == null, чат не доступен.");
            }

            string username = PeakTextChatPlugin.TwitchUsername.Value;
            string oauth = PeakTextChatPlugin.TwitchOAuth.Value;
            string channel = PeakTextChatPlugin.TwitchUsername.Value;

            PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Настройки: Username='{username}', OAuth='{oauth}', Channel='{channel}'");

            if (string.IsNullOrEmpty(username) || username == "your_nick" ||
                string.IsNullOrEmpty(oauth) || oauth == "oauth:token" ||
                string.IsNullOrEmpty(channel) || channel == "your_channel")
            {
                PeakTextChatPlugin.Logger.LogError("[TwitchIntegration] Некорректные настройки в конфиге!");
                return;
            }

            try
            {
                // Подписываемся на сообщения
                API.OnMessage += (msg) =>
                {
                    // === ИЗВЛЕКАЕМ НИК ===
                    string userName = "Unknown";
                    if (msg.User != null)
                    {
                        // Пробуем получить DisplayName (отображаемое имя)
                        var displayName = msg.User.GetType().GetProperty("DisplayName")?.GetValue(msg.User) as string;
                        if (!string.IsNullOrEmpty(displayName))
                            userName = displayName;
                        else
                        {
                            // Если нет, пробуем Name или Login
                            var name = msg.User.GetType().GetProperty("Name")?.GetValue(msg.User) as string;
                            if (!string.IsNullOrEmpty(name))
                                userName = name;
                            else
                            {
                                var login = msg.User.GetType().GetProperty("Login")?.GetValue(msg.User) as string;
                                if (!string.IsNullOrEmpty(login))
                                    userName = login;
                            }
                        }
                    }
                    // =========================

                    PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Получено сообщение от {userName}: {msg.Message}");

                    string formatted = $"<color=#9147FF>[Twitch]</color> <color=#FFFFFF>{userName}:</color> {msg.Message}";
                    if (TextChatDisplay.instance != null)
                    {
                        TextChatDisplay.instance.AddMessage(formatted);
                        PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] Сообщение добавлено в чат.");
                    }
                    else
                    {
                        PeakTextChatPlugin.Logger.LogWarning("[TwitchIntegration] TextChatDisplay отсутствует, сообщение потеряно.");
                    }
                };

                PeakTextChatPlugin.Logger.LogInfo($"[TwitchIntegration] Подключаюсь к каналу '{channel}'...");
                API.Connect(channel);
                PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] API.Connect() выполнен без исключений.");
            }
            catch (System.Exception ex)
            {
                PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Исключение при подключении: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            PeakTextChatPlugin.Logger.LogInfo("[TwitchIntegration] OnDestroy() вызван!");
            try
            {
                API.Disconnect();
            }
            catch (System.Exception ex)
            {
                PeakTextChatPlugin.Logger.LogError($"[TwitchIntegration] Ошибка при отключении: {ex.Message}");
            }
        }
    }
}