# Historical baseline audit: Online/LAN co-op

**Superseded by the playable implementation.** This file preserves the assessment
made before co-op was implemented. Its references to missing networking describe
that earlier baseline. For the current game, setup and verified behavior, read
[Online/LAN co-op support](COOP_SUPPORT.md).

Assessment date: 2026-10-31. Requested mode: separate PCs, one locally controlled
pilot per PC. Verdict: feasible as a substantial gameplay/network refactor in the
existing MonoGame project. An engine migration is not a prerequisite. This is a
source audit and implementation proposal, not working or performance-tested co-op.

## Repository and integration state

Live Git fetch and branch/tag discovery found upstream `main` at
`dbc81265b30d961eddbb75be1f1289c1f1c532f1`; no newer upstream branch or tag was
advertised. The existing contribution head before this follow-up was
`58c7ad1ebccaaa352c5482cd0d9137684b33b3ee`, which already descends from that main.
There was nothing newer to merge or rebase. PR #1 was still open and draft.

The contribution includes Xbox four-slot discovery, DualSense HID/feedback,
adaptive triggers, default-off experimental Bluetooth HD haptics, and automatic
controller button icons. This follow-up also brings the locally requested damped
mouse flight into the contribution; the previous contribution still teleported
the ship to the pointer. Local game assets and personal save/settings files are
not replaced or published by this integration.
The existing local wrapped-text spacing correction is also included, keeping the
gameplay/UI source aligned between the playable workspace and contribution.

## Findings from the source

| Area | Current evidence | Required change |
| --- | --- | --- |
| Players | `IronNebulaGame` owns one `_pilot`; `Pilot` contains health, shield, lives, upgrades and shot cooldown | A session with 1–6 `PlayerState` records, stable player IDs and a separately selected local player |
| Weapons | Beam activity/timers, precision state, banking and several firing counters belong to the game instance | Move weapon simulation state into each player, including independent beams, drones and cooldowns |
| Entity ownership | `Projectile.Friendly` distinguishes teams, but no shooter/network ID exists; hit history stores object references | Stable entity IDs, owner player IDs, team rules, one host decision per collision/kill |
| AI and hazards | Enemy aiming, Rift/Inferno attacks and laser lanes refer directly to `_pilot.Position` | Host-selected target among living players; replicate the locked target/attack instead of rerolling on clients |
| World state | Sector director, enemy lists, pickups, damage and random choices run in the local update loop | One authoritative simulation; clients send input and render replicated world state |
| Economy | Credits, weapon-unlock mask, shop selection and checkpoint fields are singular | Explicit shared/personal economy policy, atomic purchases and a ready barrier for sector transitions |
| Saves | `RunSaveData` schema 1 stores one pilot and no live projectiles | Separate versioned co-op save contract; preserve single-player saves and make the host own party progress |
| Pause and focus | Local focus loss pauses the whole current game | Online focus loss neutralizes only the local pilot and feedback; the shared world continues |
| UI and feedback | One local input device is selected; menus/haptics follow it | Keep device input/icons/haptics local; add party HUD, player colors, lobby and disconnect state |
| Networking | No socket transport, lobby, peer identity, state replication or reconnect path was found in gameplay source | Implement and validate all of these before describing the game as network-playable |

Relevant source entry points: [Pilot / Enemy / Projectile](Source/Entities.cs),
[simulation, weapons, targeting, collisions, shop and HUD](Source/IronNebulaGame.cs),
[Inferno attacks](Source/InfernoBosses.cs), [sector spawning](Source/InfernoSector.cs),
[local input and checkpoint mapping](Source/NextSystems.cs),
[save schema](Source/RunSaveStore.cs), [device selection](Source/DualSenseDevice.cs),
[local feedback](Source/GamePadFeedback.cs).

The current code has hundreds of single-pilot references across simulation and
presentation. Adding five extra sprites or duplicating `_pilot` alone would not
create correct co-op. Seeded random generators also do not establish deterministic
cross-machine simulation; frame timing, input order and spawn/drop decisions differ.

## Recommended architecture

Use a listen server: one player hosts and plays, with at most five remote guests
(six total players). The host owns enemy AI, spawning, projectile damage, pickups,
credits, boss phases, checkpoints and sector transitions. Clients send bounded,
sequence-numbered input; they never submit authoritative damage, money or positions.

Separate `WorldSimulation` from local rendering/input/audio before adding the wire
protocol. Each player needs a stable ID and private weapon/drone state. Each enemy,
projectile and pickup needs a stable session-scoped ID. Serialize plain bounded
messages, not C# object graphs or object references.

An initial tuning target is 60 Hz host simulation and roughly 20 Hz world snapshots,
with interpolation for remote objects and prediction/reconciliation for the local
pilot. These are proposed targets, not measured capacity. Keep small critical
events reliable (join/leave, purchases, deaths, sector changes) and time-sensitive
input/snapshots sequenced and disposable when superseded. Choose and pin a transport
after a small two-process networking spike; do not equate opening a TCP/UDP socket
with a complete synchronization layer.

LAN can start with manual host address entry. Internet play additionally needs a
deliberate connectivity solution: reachable host/port forwarding, a relay, or a
platform transport. LAN discovery and an Internet lobby are different features.
Steam Datagram Relay is a possible later integration, subject to Steamworks
availability; no Steam integration or relay service is configured here.

Per-machine controller limits do not cap the party at four: six PCs can each use
their own controller. XInput supports up to four controllers on one Windows PC;
the current six-player proposal does not require six local XInput devices. The
native DualSense worker currently opens one controller, also sufficient for one
local player per PC. Reusing remote damage events must never vibrate every player's
local controller; feedback must be filtered to the locally owned pilot.

## Proposed first-release rules

These are design proposals, not implemented or user-approved gameplay rules:

- Start with a lobby and host-started run; allow joining/rejoining at a lobby or
  checkpoint before supporting arbitrary mid-boss joins.
- Friendly fire off; unique ship colors/outlines and a compact six-player HUD.
- Host chooses AI targets from living players. Preserve attack telegraphs so each
  client sees the same locked target and attack timing.
- Personal hull/shield/weapon state; start with a shared team credit pool and
  host-validated purchases. Require an all-ready barrier before the next sector.
- Distinguish individual death/revive from a full-party wipe. Define checkpoint
  restore once for the party, including pickups and credits, to prevent duplication.
- Brief disconnect grace with neutral input; release or retain the slot according
  to an explicit reconnect policy. First version can end cleanly on host departure;
  host migration is a separate feature.
- Host stores co-op progress separately from `run-save.json`; do not overwrite
  existing solo progress with a new multiplayer schema.

## Work sequence and acceptance gates

1. Extract a headless world and per-player weapon state while retaining solo
   behavior. Verify independently firing/moving players, separate beams and damage.
2. Add stable entity IDs, player ownership and deterministic event application on
   the authoritative host. Check duplicate hits, pickups, kills and purchases.
3. Build a two-process LAN slice: lobby, join, movement, shooting, one enemy and
   death/rejoin. Prove both clients agree on host state; a lobby connection alone
   does not pass this gate.
4. Extend to host plus five guests. Exercise all five weapons, level-7 loadouts,
   drones, every boss and sector transition. Tune effect budgets separately from
   gameplay so reducing smoke cannot alter damage.
5. Add online connectivity, authentication/session admission and reconnect UX.
   Enforce six players including host, protocol/build compatibility, packet-size
   limits, input-rate bounds and stale/duplicate-message rejection.
6. Run end-to-end tests across machines with delay, jitter, packet loss and reordering;
   verify party wipes, focus loss, host exit, save/resume and no duplicated rewards.

Six-player feasibility is not a measured performance guarantee. Projectile count,
beam collision work, per-enemy hit bookkeeping, large FX bursts, audio mixing,
snapshot size and host upload all need measurement under the actual six-player
load. The current single-player build/tests provide no multiplayer FPS, latency,
bandwidth or Internet reachability evidence.

## Validation boundary

This follow-up validates the existing controller/game builds and audits co-op
requirements. It adds no lobby, sockets or multiplayer gameplay, and no ports or
firewall rules are changed. Real controllers and multiple-PC networking remain
unverified. If a Godot port is chosen later, the same authority, entity ownership,
economy and save decisions are still needed; maintain one gameplay implementation
at a time rather than independently refactoring both engines.

## Primary references

- [Microsoft: XInput multiple-controller limits](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput#multiple-controllers).
- [Microsoft: .NET sockets and endpoints](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-services).
- [Valve: Steam Datagram Relay](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay).
