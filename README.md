# TwitchChat

Displays Twitch chat messages in the PEAK in-game chat. Messages appear with the `[Twitch]` prefix.

**Fork of [PeakTextChat](https://github.com/borealityy/PeakTextChat)**

## Features
- Display Twitch chat messages in the game chat with colored nicknames.
- Send messages from the game to Twitch using the `!` prefix.
- Anonymous mode (read-only) without a token.
- **Quick messages** – send predefined messages with hotkeys (configurable).
- **Permission system**: only host and allowed players can send messages.
- **System messages** with colors (green for success, red for errors, orange for warnings).
- **RPC sending**: players without a token can send messages via the host (if added to `AllowedSenders`).

## Configuration

1. Open `BepInEx/config/V8.TwitchChat.cfg`.
2. Fill in the sections:

```ini
[Twitch]
Username = your_twitch_channel   # The channel whose chat is displayed and where messages with ! are sent
AllowedSenders =                 # Steam IDs (comma separated) allowed to send messages. Empty = only host.
UseSteamName = true              # If true, shows Steam name in Twitch. If false, hides the name.
SendWithoutPrefix = false        # If true, messages without '!' go to Twitch, with '!' go only to game chat.

[Auth]
OAuthToken = oauth:your_token    # The token of the account that will send messages (required for sending, optional for read-only)

[QuickMessages]
QuickMessages =                  # Format: Key$Message:::Key2$Message2. Keys: F1, G, NUM4, J, Alpha1, etc.
                                 # Example: F1$Hello!:::G$Follow me:::NUM4$Look:::J$!chat see??