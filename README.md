# Dedicated Simulation

Dedicated Simulation continues [Serverside Simulations](https://github.com/ddormer/valheim-serverside) by ddormer and contributors, which is no longer maintained, and is updated for current versions of Valheim.

**Why the new name?** The owner of the original repository asked that anyone releasing their own version of the mod use a different name. As requested, this version has been renamed from Serverside Simulations to Dedicated Simulation. It is not affiliated with or endorsed by the original authors, and any issues should be reported [here](https://github.com/liekos47/valheim-dedicated-simulation/issues), not on the original repository.

[![Build Plugin](https://github.com/liekos47/valheim-dedicated-simulation/actions/workflows/build-plugin.yml/badge.svg)](https://github.com/liekos47/valheim-dedicated-simulation/actions/workflows/build-plugin.yml)

Run world and monster simulations on a **dedicated server**.

Updated for patch: 1.0.12

### Features
- Server simulates world and AI physics.
- Client FPS improvements
- Ships are simulated by the driver to improve the steering experience with high latency.

### Installation

 1. Install BepInEx (optionally installing "Better Networking" on both clients and the server is recommended)
 2. Copy `DedicatedSimulation.dll` into the BepInEx/plugins/ directory on your dedicated server.
 3. You're done! No client-side changes are needed.

Upgrading from Serverside Simulations: delete `Serverside_Simulations.dll` from BepInEx/plugins/ first, so the two don't load together. Settings are now read from `BepInEx/config/liekos47.DedicatedSimulation.cfg`; copy any values you changed over from `MVP.Valheim_Serverside_Simulations.cfg`.

_It's recommended to also install the mod "BetterNetworking", it works very well with Dedicated Simulation._

### Configuration

Settings are in `BepInEx/config/liekos47.DedicatedSimulation.cfg`.

| Setting | Default | What it does |
|---|---|---|
| `MaxObjectsPerFrame.MaxObjects` | 100 | Objects the server may create per frame. Higher loads areas faster, at the cost of CPU. |
| `ServerSimulationDistance.Level` | 0 | Simulation distance the server uses around each player, and the cap for joining clients. 0 = classic (3x3 zones simulated, 5x5 loaded), the distance this mod is designed for. 1-5 are larger and cost much more CPU. -1 leaves the server's own value. |
| `Performance.CreateDestroyIntervalMs` | 0 | Minimum time between runs of the per-player object create/destroy sweep. 0 = every frame. A small value (50-100) spreads server load; clients are unaffected. |
| `Performance.SkipDistantObjects` | false | Don't create far-ring objects on the server, which only needs the near ring it simulates. Experimental. |
| `Profiler.Enabled` / `IntervalSeconds` | false / 60 | Logs how the server's main-thread time is split (zone loading, object sweep, ZDO sync, AI, character updates). Small overhead while on. |

### Performance

Measured on 2026-09-13 on a live server: Valheim l-1.0.12, BepInExPack 5.4.2333 with Better Networking, Linux in Docker, Intel i7-9700 (4.3-4.5 GHz), 3-8 players. These are development builds of this version, so treat the numbers as a guide for similar hardware.

Valheim runs the simulation on a single main thread, so that thread is the limit. Load on it, as % of one core:

| Build | 3 players | 4 players | 5 players | 6 players | 7 players |
|---|---|---|---|---|---|
| Server simulation distance unset (0x0) | 60-70 | 89, pinned | - | - | - |
| Classic simulation distance | 45 | 51-54 | 73, pinned in fights | 73-75 | 75 |
| Optimised (current) | 45 | 50 | 53 | 63-84 (dungeons) | 69-86 |
| Better Networking only, no Dedicated Simulation | - | 26-31 | - | 25 | 29 (8 players) |

Frame time with the optimised build (a 30 fps server tick is 33 ms):

- 35-39 ms with up to 7 players, outside fights
- 45-55 ms in fights
- 60-93 ms with 6-7 players running dungeons

What the profiler showed:

- **Creature count decides frame time.** 116 simulated creatures gave 42 ms, 148 gave 49 ms, 177 gave 66 ms. On this CPU about 120 creatures is where the 33 ms budget runs out.
- **Dungeons pile up creatures.** Each one spawns 20-40, and earlier dungeons' creatures stay alive nearby. Six players running crypts reached about 170.
- **The object sweep was the biggest cost the mod controls.** `ZNetScene.CreateDestroyObjects` took 21% of busy time before optimisation and 9-13% after. Monster AI was under 3%, character physics 3-10%.
- **Under heavy load the rest is Unity.** Most of the remaining time is physics and animation work the main thread waits for, which a mod can't reach.

On similar hardware, expect it to be comfortable for about five players outside dungeons. Beyond that you hit the single-thread ceiling, not the mod. Setting `CreateDestroyIntervalMs = 100` and leaving the profiler off is the suggested starting point; that combination hasn't been measured yet.

### Valheim 1.0 fixes

Valheim 1.0 added code that assumes a local player exists wherever the game runs. On a dedicated server there isn't one, so this version fixes:

- **Berry and item pickup** (`Pickable.RPC_Pick`): pickups showed +1 but gave nothing, with thousands of NullReferenceExceptions.
- **The same null-player problem** in traps, containers, ship sails, saddles, sound effects, cooking stations, build-piece stats, ward access, the Leviathan and music volumes.
- **Unset server simulation distance:** the server was simulating only the zone each player stood in, so monsters froze at zone borders and objects repeatedly loaded and unloaded at the edges. It's now pinned to classic (`ServerSimulationDistance`).

### Caveats

- Only runs on dedicated servers.
- The mod dramatically increases server resource usage and running it on a weak CPU or limited RAM may lead to a poor gameplay experience.
- The mod should be disabled when using the "optterrain" command. 
- Tree chopping and mining stats (and the achievements that use them) don't count. The game credits them to the player on whichever machine owns the object, which is now the server. Fixing this needs a client-side mod. Kill stats still work.
- Some harmless messages show up in the server log because the server now owns objects clients normally own:
  - `Failed to find rpc method 327122920` is Valheim's own "discovered" RPC name mismatch.
  - `Double ZNetview when initializing object vfx_RockHit` comes from the server creating a hit effect.
  - `EndOfStreamException` bursts in `Smelter.RPC_AddOre` come from an out-of-date client-side kiln/smelter auto-feed mod on a player's PC.
- This mod does not prevent cheating or any kind of client manipulation.
- While this mod is quite light on complexity, as with most mods it's possible future Valheim patches will break the mod in unexpected ways. We recommend you back up your characters and worlds and/or consider disabling this mod anytime a new game patch is released.

### Why?

Ordinarily, to keep server resource usage low, the Valheim server will hand off simulation of an area to the first client that enters said area. However, if the player in charge of the area has a poor connection all other players in that area will suffer. This mod is an attempt at improving that specific situation at the cost of increased latency for the client which would ordinarily own the area.

### How?

This dedicated server mod causes terrain, monsters and other objects that are normally created and owned by clients to instead be created on—and thus owned and simulated by—the server.

#### For mod developers - compatibility with Dedicated Simulation

For mod developers interested in maintaining compatibility with Dedicated Simulation:
- If your mod makes changes relating to simulation / behaviour of the world, it will need to be able run on the dedicated server and should take these points into account:
  - Player.m_localPlayer is always `null` on a dedicated server; your code should check for this.
  - On a dedicated server, `ZNet.instance.GetReferencePosition()` returns a position outside of the world and is not related to any player position.
  - Any graphical or hud-related code should probably be behind a `ZNet.instance.IsDedicated()` check, if that code is expected to run on the server.

### Manually compiling

To manually compile, create a file at `src/Environment.props` with the following content, and change the path to point at your Valheim install.

```
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- Needs to be your path to the base Valheim folder -->
    <VALHEIM_DEDI_INSTALL>E:\SteamLibrary\steamapps\common\Valheim dedicated server</VALHEIM_DEDI_INSTALL>
  </PropertyGroup>
</Project>
```
