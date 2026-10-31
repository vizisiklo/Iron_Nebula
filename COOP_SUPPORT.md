# Online / LAN co-op (playable alpha)

Iron Nebula now includes a host-authoritative co-op mode for **2–6 players on
separate Windows PCs**: one host plus up to five guests. It runs in the existing
MonoGame game, with all three sectors, independent player ships and weapons,
replicated enemies/projectiles/pickups, shared progression and a party HUD.

## Play

1. All players build/use the same version, including the normal game assets.
2. From the main menu, choose **ONLINE / LAN CO-OP**.
3. One player chooses **HOST NEW PARTY**. The lobby displays the host's IPv4
   address(es), port and temporary room code, in `address:port/code` form.
4. Guests click the host-address field, enter the displayed connection string,
   then choose **JOIN HOST ADDRESS**. Backspace edits the field. DNS names and
   an optional public port are supported; the default TCP port is **28771**.
5. After at least one guest joins, the host presses Confirm (Enter / Xbox A /
   PlayStation Cross) to launch. Up to six pilots, including the host, can play.

For LAN play, use the host's address on the same network. For Internet play, the
host must be reachable through the selected TCP port, for example through manual
router port forwarding or a VPN connecting the PCs. This build has no matchmaking,
relay or automatic NAT traversal and does not modify firewall/router rules.
The temporary room code controls admission; this is not an encrypted transport.
Use it with trusted players; a private VPN is appropriate for Internet sessions.

## Controls and session rules

- Existing keyboard, mouse, Xbox and DualSense mappings apply to each PC's own
  pilot. Controller button icons stay local. Tab/Q or RB/LB / R1/L1 cycle shared
  unlocked weapons; firing, precision mode, beams and drones are independent.
- Each pilot has personal health, shield, lives, pickups and firing state. Ships
  have distinct colors and P1–P6 labels. No friendly fire.
- The host simulates movement, spawning, enemy targeting, collisions, damage,
  pickups, rewards and sector transitions. Guest input is bounded and sequenced;
  clients cannot submit damage or credits. Enemy hull scales with party size.
- The team shares credits and unlocked weapons. **The host controls the shop**;
  purchased capacity/engine/power/drone/magnet upgrade levels propagate to guests.
  Personal repairs/pickups apply to the receiving pilot.
- At the shop, **every connected player toggles READY** with C / Menu / Options
  (or the host's Ready button). The next sector/victory waits for all players.
- Losing focus or opening a local menu neutralizes only that pilot's input and
  stops local feedback. The host keeps simulating; the party is not globally paused.
- Esc / B / Circle opens a local menu with Return, Settings and Disconnect.
  Leaving as host closes the session for guests. Host migration is not implemented.
- Individual deaths spend personal lives; eliminated pilots spectate until the
  next sector, where they redeploy with one life. A full-party wipe ends the run.
- New connections are accepted in the lobby, not during an active run. A dropped
  guest is removed from the party. Rejoining currently requires returning to the
  lobby; there is no mid-run reconnect/late-join restoration.
- Co-op runs do **not overwrite solo saves or solo high scores**. Persistent co-op
  campaign save/resume is not implemented in this first playable alpha.

## Implementation

The transport is versioned, length-prefixed JSON over TCP with `NoDelay`, a
bounded latest-state output queue, capped messages, admission code, six-player
limit, handshake/idle/write deadlines, input-rate limits and sequence checks.
The host sends complete snapshots at approximately 20 Hz while the normal fixed
game update runs at 60 Hz. Client movement uses local prediction/reconciliation;
remote ship positions are smoothed. Full snapshots rebuild parent links by index,
so clients do not deserialize host object references or run collision simulation.
This favors a small understandable LAN implementation over a claim of competitive
low-latency Internet networking. High RTT/loss can expose TCP head-of-line stalls.

Core files: [transport and wire state](Source/CoopNetwork.cs),
[authoritative gameplay and local controls](Source/CoopGame.cs),
[lobby, party HUD and remote ships](Source/CoopUi.cs).

## Validation

- Release build: zero compiler warnings/errors.
- Existing 1,031 synthetic controller checks retained.
- 32 real-loopback transport checks cover host + five guests, unique player IDs,
  shared state round-trip, input sequencing, seventh-player rejection, oversized
  packets, disconnection, late-join policy and wrong/correct room codes.
- A six-process game smoke test runs the actual MonoGame host, one actual rendered
  game client, and four independent bot clients. It exercises 1,200 simulation
  ticks, six moving/firing pilots, all five weapons, positive damage for each
  player, all three sectors, Rift/Inferno Guardians, the all-ready shop barrier,
  replicated victory and host shutdown. The solo-save hash remains unchanged.
- The rendered client screenshot was inspected and shows all six ships, distinct
  player labels and the per-player HUD. The smoke uses scripted sector transitions
  and test targets; it is not a complete human campaign playthrough.

Commands from the project root:

```powershell
dotnet build IronNebula.csproj -c Release
dotnet run --project Tools/CoopChecks/CoopChecks.csproj -c Release
dotnet run --project Tools/ControllerChecks/ControllerChecks.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Invoke-CoopSmoke.ps1 -VisualClient
```

The game smoke exports JSON evidence and PNGs under a new `bin/coop-smoke-*`
directory. It starts only its own helper processes and terminates those helpers
on timeout. `--coop-smoke`, `--coop-bot` and `--coop-visual-client` are explicit
verification modes; normal launches do not enable scripted input or test targets.

Not yet verified: multiple physical PCs, public Internet/router/firewall behavior,
lossy/high-latency networks, six physical controllers, long-session performance,
all boss phases and a full three-sector human playthrough. Physical DualSense
adaptive-trigger/haptic feel remains a separate hardware check.
