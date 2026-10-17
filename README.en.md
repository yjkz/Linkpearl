<p align="center"><a href="README.md">简体中文</a> | English</p>

<div align="center">
  <img src="images/icon.png" alt="Linkpearl icon" width="96">
  <h1>Linkpearl</h1>
  <p><strong>Real-time FFXIV chat in your browser, with an AI assistant.</strong></p>
  <p>
    <a href="https://github.com/yjkz/Linkpearl/releases"><img src="https://img.shields.io/github/v/release/yjkz/Linkpearl" alt="Latest release"></a>
    <a href="https://github.com/yjkz/Linkpearl/actions/workflows/release.yml"><img src="https://github.com/yjkz/Linkpearl/actions/workflows/release.yml/badge.svg" alt="Release workflow"></a>
  </p>
</div>

Linkpearl runs a lightweight web server inside Final Fantasy XIV. It streams your in-game chat to any browser in real time, lets you reply from anywhere, and adds an OpenAI-compatible AI assistant that can answer, offer reply options, or suggest replies in the channels you choose.

![Linkpearl chat view with the AI assistant panel open](images/readme-chat.png)

## Highlights

### Chat, everywhere

- Real-time streaming over Server-Sent Events — no polling
- Supports FC, LS/CWLS 1–8, Party, Tell, Say, Shout, Yell, Alliance, Novice Network and more
- Custom filters with folders, drag-and-drop ordering, unread badges and per-filter send channels
- Tell mode with pinning and recent direct-message history
- Optional send queue with a short countdown so you can cancel an accidental send
- Emote picker with search and recently used, plus an FFXIV symbol picker
- Rich link previews for trusted sites (Lodestone, YouTube, Twitter/X, Twitch and more)
- Passcode authentication for browser access

### AI assistant

- Works with any OpenAI-compatible Chat Completions provider (OpenAI, DeepSeek, Z.ai, Ollama, LM Studio, …)
- Multiple custom agents, each with its own system prompt, model override, temperature and token limit
- Per-channel trigger rules with cooldowns and per-minute rate limits
- Three reply modes:
  - **Auto reply** — send the generated reply immediately
  - **Multiple options** — offer several clickable candidate replies
  - **Reply suggestion** — edit one suggestion before sending
- The assistant is a plain chat model — no tool calling, no hidden actions
- Suggestions and activity appear in both the browser panel and the in-game `/linkpearl ai` window

### One interface, two languages

- The browser and the in-game windows share the same language setting
- **Chinese is the default**, English can be selected at any time
- The choice is stored in the plugin configuration and synced live between both UIs

## Screenshots

| Filters & channel selection | Appearance & language |
|---|---|
| ![Filter editor](images/readme-filter.png) | ![Appearance settings](images/readme-appearance.png) |

<p align="center">
  <img src="images/readme-mobile.png" alt="Linkpearl on a phone" width="260">
</p>

## Installation

### Custom Dalamud repository

Linkpearl is distributed through its own plugin repository:

```
https://raw.githubusercontent.com/yjkz/Linkpearl/main/pluginmaster.json
```

1. In game, run `/xlsettings` to open Dalamud settings.
2. Open the **Experimental** tab.
3. Add the URL above to **Custom Plugin Repositories** (one URL per line), then save.
4. Open the **Dalamud Plugin Installer**, search for **Linkpearl**, and install it.

The installer entry includes the full description, icon and screenshots.

### Local development

For local development, skip the repository and add the build output folder (for example `bin\Debug`) to **Dev Plugin Locations** in Dalamud's Experimental settings. The folder must contain `Linkpearl.dll`, `Linkpearl.json` and the `dist\` frontend folder.

## Setup

1. Open the plugin settings with `/linkpearl`.
2. Under **Security**, set a 4–8 digit numeric passcode.
3. Start the web server if it is not already running, then open the shown URL in your browser:

   ```
   http://localhost:3000
   ```

4. Enter the passcode. Chat messages stream into the page immediately, and replies are sent back to the game.

To use another device on the same network, open `http://<your-local-ip>:3000` instead. The plugin settings window lists the available IPv4 addresses.

## AI configuration

1. Open `/linkpearl ai` or the AI panel in the browser.
2. Open **Settings → AI Assistant**.
3. Set the **API Base URL**, **API Key** and default model.
4. Create one or more **Agents** and give each one a system prompt.
5. Create a **Trigger Rule**, bind it to an agent, pick the channels and choose a reply mode.
6. Save. The **Test Connection** button saves the current settings and verifies the provider.

Auto replies are rate-limited per rule (cooldown + maximum per minute), and messages sent by your own character are ignored to prevent loops. The API key is stored locally in the plugin configuration and is never sent to the browser.

## Commands

| Command | Action |
|---|---|
| `/linkpearl` | Toggle the settings window |
| `/linkpearl ai` | Toggle the AI assistant window |
| `/linkpearl start` | Start the web server |
| `/linkpearl stop` | Stop the web server |
| `/linkpearl help` | Show command help |

## Building from source

Requirements:

- .NET 10 SDK (the project uses `Dalamud.NET.Sdk/15.0.0`)
- Node.js 20+ and npm
- Dalamud reference assemblies (`DALAMUD_HOME` or a local XIVLauncher installation)

```bash
# Plugin (also builds the frontend and copies dist/ into the output)
dotnet build Linkpearl.sln

# Frontend only
cd Http/Frontend
npm install
npm run build
npm test
npm run lint
```

The build produces a loadable plugin in `bin\Debug` (or `bin\Release`), including `Linkpearl.json` and `dist\`.

## Releasing

1. Bump `<Version>` in `Linkpearl.csproj` and commit to `main`.
2. Push a tag matching the version, e.g. `vX.Y.Z`.
3. The `Release` workflow lints/tests/builds the frontend, builds the plugin, publishes `Linkpearl.zip` as a GitHub Release asset and updates `pluginmaster.json`.

## Acknowledgements

Linkpearl is an independent derivative of the original [ChatAnywhere](https://github.com/twelvehouse/ChatAnywhere) project by twelvehouse. The chat relay, browser UI and plugin foundation started there, and we are grateful for that work.

Linkpearl would not exist without these projects:

| Project | Role |
|---|---|
| [ChatAnywhere](https://github.com/twelvehouse/ChatAnywhere) | Original project Linkpearl is based on |
| [ChatTwo](https://github.com/Infiziert90/ChatTwo) | Original inspiration for chat handling and UI approach |
| [Dalamud](https://github.com/goatcorp/Dalamud) | Plugin framework and FFXIV game API |
| [NetStone](https://github.com/xivapi/NetStone) | Lodestone character avatar lookups |
| [OpenGraph-Net](https://github.com/ghorsey/OpenGraph-Net) | URL Open Graph preview metadata |
| [Watson.Lite](https://github.com/jchristn/WatsonWebserver) | Embedded HTTP server and SSE support |
| [React](https://react.dev/) + [React Compiler](https://react.dev/learn/react-compiler) | Frontend framework with automatic memoization |
| [Vite](https://vitejs.dev/) | Build tool and dev server |
| [TanStack Query](https://tanstack.com/query) | Server-state caching and request deduplication |
| [Zustand](https://github.com/pmndrs/zustand) | Client-state management |
| [@dnd-kit](https://dndkit.com/) | Drag-and-drop filter and folder reordering |
| [react-error-boundary](https://github.com/bvaughn/react-error-boundary) | Error boundary for the message list |
