# STS2 QoL Mods: AutoPass + UnifiedSaves

Two small quality-of-life mods for **Slay the Spire 2** (tested on v0.107.x).

## AutoPass

Automatically ends your turn the moment you have **no possible actions left** —
no playable card in hand and no potion you'd want to drink.

- Uses the game's own `CanPlay()` logic, so energy costs, 0-cost, X-cost,
  star costs, Unplayable, and card-specific restrictions are all respected.
- Never fires until the game is fully idle: no card/potion effect resolving, no
  queued card plays, and no game action still executing — so triggered draws
  like **Unceasing Top** always finish before the turn can end.
- Never fires during other players' extra turns or outside your play phase.
  Multiplayer-safe: only ever ends *your* turn.
- Presses the actual End Turn button code path — nothing homebrew.

### Auto-pick identical selections

Fixed-count card prompts — Survivor/Prepared-style "discard exactly N", or
"choose exactly N" pile picks — resolve instantly with **no selection screen**
when every eligible card is functionally identical (e.g. discarding one of
three unenchanted Strikes). The identity check is strict: same card, upgrade,
enchantment, per-card counters, current cost with modifiers, star cost,
Sly/retain state, and playability. If anything differs, you get the normal
screen. Optional selections (Well-Laid Plans' "retain *up to* N") always show
their screen, since how many you pick still matters. Single-player only.

### F8: live toggle

**F8 toggles AutoPass anywhere — including mid-fight** — with an on-screen
ON/OFF indicator. This is the escape hatch for potion sequencing (e.g.
Fortifier wants to be drunk *after* your last block card): F8 off, play your
cards, drink, F8 on. Toggling on with no actions left passes the turn
immediately.

**Settings** live in the game's own Mods screen (Settings → Mods → select
AutoPass):

| Setting | Options | Default |
|---|---|---|
| Auto-end turn (F8 toggles anywhere) | on / off | on |
| Auto-pick when selection cards are identical | on / off | on |
| Potions block auto-pass | Always / Elites & bosses only / Never | Always |

The default is conservative: any usable potion prevents auto-pass, so the mod
never ends a turn where you could still drink something. Switch to
"Elites & bosses only" to auto-pass through hallway fights while you hoard
potions (elite/boss fights still never end your turn while you hold a usable
potion), or "Never" to ignore potions entirely. With those modes, drink
potions **before** playing your last card — or use F8.

"Elites & bosses" also covers a short list of *honorary elites*: fights the
game data classifies as normal monster rooms but that play at elite stakes.
Currently that's the Mysterious Knight (the Lantern Key guardian). PRs/issues
welcome for others that deserve the treatment.

## UnifiedSaves

Slay the Spire 2 sandboxes modded play into a separate save directory
(`modded/profileN`), so playing with any mod means starting from scratch.
UnifiedSaves patches the single path switch responsible, so modded play uses
your **normal profiles** — unlocks, ascension, history and all.

Because that removes the game's safety sandbox, UnifiedSaves takes a **full
snapshot of your save data on every launch** (into the game user-data folder
under `backups/`, keeping the last 10) *before* the game writes anything.

> ⚠️ Use UnifiedSaves at your own risk alongside big content mods — a buggy mod
> writing into your real saves is exactly what the sandbox normally prevents.
> The launch snapshots are your insurance.

The two mods are independent — install either or both.

## Installation (manual)

Each mod is a folder containing a `.json` manifest and a `.dll`. Drop it into
the game's `mods` folder:

- **Windows:** `<Steam>/steamapps/common/Slay the Spire 2/mods/`
- **macOS:** `<Steam>/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/`
- **Linux:** `<Steam>/steamapps/common/Slay the Spire 2/mods/`

So e.g. `mods/AutoPass/AutoPass.json` + `mods/AutoPass/AutoPass.dll`. Then
launch the game, agree to load mods, and enable them in the Mods menu
(restart required).

Grab prebuilt zips from the [Releases](../../releases) page — the DLLs are
platform-independent (AnyCPU .NET).

## Building from source

Requires the .NET 9 SDK and an installed copy of Slay the Spire 2 (the build
references the game's `sts2.dll`).

```sh
dotnet build AutoPass.csproj -c Release
dotnet build unified-saves/UnifiedSaves.csproj -c Release
```

On macOS with a default Steam install this also copies the built mod straight
into the game's mods folder. On other platforms, pass the paths:

```sh
dotnet build AutoPass.csproj -c Release \
  -p:Sts2DataDir="<game>/data_sts2_windows_x86_64" -p:ModsDir="<game>/mods"
```

## Why not BaseLib?

[BaseLib](https://github.com/Alchyr/BaseLib-StS2) is the excellent de-facto
standard foundation for STS2 *content* mods — if these mods ever grow custom
cards/relics/potions, it's the obvious choice. But AutoPass and UnifiedSaves
add no content: their core is a handful of Harmony patches BaseLib doesn't
abstract, and staying dependency-free means one-click installs and no waiting
on a dependency update after game patches. UnifiedSaves in particular stays
minimal on principle — it's the mod that backs up your saves before anything
else runs, so it should keep working even when other mods break.

## Compatibility

Built and tested against STS2 **v0.107.1** (Major Update 2). Early Access
updates can change internal APIs — if a game update breaks something, open an
issue with the `[AutoPass]`/`[UnifiedSaves]` lines from your game log.

## License

MIT — see [LICENSE](LICENSE).
