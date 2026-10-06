# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"Lights Out" (working title) — a funny 2D top-down party game for up to 10 players on Android phones over local Wi-Fi.
Somebody cut the power at an office party: every player only sees a small circle around them (Among Us-style vision
blocked by walls); bonk whoever you bump into. Goal: playable with ~10 friends visiting in November 2026, so keep the
scope small and test on real phones early.

Unity **6000.5.3f1**, URP 17 (2D renderer), Netcode for GameObjects **2.13.3** (3.x needs Unity 6000.7), Unity
Transport, Input System 1.19 (Active Input Handling = Input System only), uGUI with the legacy `Text` component and the
built-in `LegacyRuntime.ttf` font.

## Plan

1. ✅ Networking + movement: host/join over Wi-Fi, LAN discovery, joystick, vision mask.
2. Bonk attack (owner sends hit request → host validates distance → knockback), knockout flop, ghost mode, rounds.
3. Powerups (night goggles, glow stick, disco ball), sounds, hats, scoreboard, vision grows per knockout.
4. Polish, balancing, 10-phone stress test.

## Building / running

- Everything game-specific is in `Assets/_Game/`. Scene: `Scenes/Game.unity` (only scene in the build).
- `Assets/_Game/Editor/ProjectSetup.cs` (menu **Lights Out → Rebuild Generated Scene and Prefab**) regenerates layers,
  `Resources/*.mat`, `Prefabs/Player.prefab`, `Scenes/Game.unity`, build scene list and player settings. It
  **overwrites** the scene and prefab. Headless:
  `Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod LightsOut.EditorTools.ProjectSetup.Run`
  It invokes `NetworkObject.OnValidate` by reflection, because batch mode leaves the prefab's `GlobalObjectIdHash` at 0.
- Windows test build: `Unity.exe -batchmode -quit -nographics -projectPath . -buildWindows64Player Builds/Windows/LightsOut.exe -logFile <log>`,
  then grep for `error CS` / `Build Finished, Result`. The Editor must be closed (project lock).
- Android: needs the Android Build Support module (+ SDK/NDK, OpenJDK) for 6000.5.3f1 in Unity Hub. Build with
  `-buildTarget Android` and a build script/profile; IL2CPP, ARM64, package `com.praveenunitydev.lightsout`, landscape.
- Multi-instance test on one PC (all logging `[LightsOut]` lines):
  ```
  LightsOut.exe -batchmode -nographics -lo-host -lo-name Hank -lo-bot -lo-quit 22 -logFile host.log
  LightsOut.exe -batchmode -nographics -lo-autojoin -lo-bot -lo-quit 17 -logFile c1.log
  LightsOut.exe -batchmode -nographics -lo-join 127.0.0.1 -lo-bot -lo-quit 16 -logFile c2.log
  ```
  `-lo-quit` logs a `SUMMARY` with every player's name/colour/position/visibility. `-lo-shot <sec> <file.png>`
  saves a screenshot (run without `-batchmode -nographics`). Flags are parsed in `GameRoot.ParseCommandLine`.

## Code (namespace `LightsOut`, default Assembly-CSharp)

- `Core/GameRoot` — the only scene script besides the camera and NetworkManager. Builds map, vision mask, UI; wires
  `NetworkSession`; switches menu ↔ HUD; command-line flags. `LocalPlayerName` is saved in PlayerPrefs.
- `Net/NetworkSession` — Host / Join / Leave, connection approval (max 10), disconnect reasons. Game port UDP 7777.
- `Net/LanDiscovery` — UDP 47777. Host answers `LIGHTSOUT1?` with `LIGHTSOUT1!|port|players|max|name`. Searchers send
  the query to broadcast **and every address in their /24** (Android often drops incoming broadcasts; unicast gets
  through). Non-blocking sockets polled in Update, no threads. `Net/NetUtil` — local IPv4s/masks, with fallbacks.
- `Player/PlayerAvatar` (NetworkBehaviour) — owner-authoritative movement (`NetworkTransform.AuthorityMode = Owner`,
  Rigidbody2D velocity on the owner, `rb.simulated = false` on remotes). Host assigns `ColorIndex` (also the spawn
  index); `PlayerName` is owner-writable. Remote players fade in only when `VisionMask.CanSee` them.
  `Player/BlobVisual` — procedural blob with googly eyes, wobble and blinking, driven by observed velocity.
- `World/GameMap` — ASCII map (`#` wall = sight + movement blocker on layer Walls, `T` furniture = movement only,
  `S` spawn, `.`/`a`-`f` floor regions). Built at runtime; horizontal runs are merged into one sprite/collider.
  `World/VisionMask` — darkness mesh from raycasts against Walls (soft edge, wall faces revealed); `Radius`/`TargetRadius`.
- `Core/GameInput` — the only input read point (touch joystick + WASD/arrows + gamepad; `BotMode`).
- `UI/UIKit` builds uGUI from code (reference 1920x1080, match height). `MenuScreen`, `HudScreen` (name tags,
  toasts), `VirtualJoystick` (floating, left 45% of the screen).
- `Core/GameAssets` — procedural sprites and the materials in `Resources/` (`SpriteUnlit` — sprites must use it, the
  2D renderer's default is lit; `Darkness` — `Shaders/Darkness.shader`). `Core/Palette` — every colour.

## Conventions

- Layers: Walls 8, Players 9 (players don't collide with each other), Furniture 10.
- Unity 6 APIs (`linearVelocity`, `FindAnyObjectByType`). Never use `UnityEngine.Input`.
- Anything that must match across devices is networked through NGO; visuals stay local and are derived from synced
  state. New networked prefabs must be registered with the NetworkManager (`DefaultNetworkPrefabs.asset`).
- Serialized field names are referenced by the prefab/scene YAML; renaming loses values unless you add
  `[FormerlySerializedAs]`.
