# Deadball Districts — Day 1

Greybox + Local Versus, per GDD §21. Capsules only, no art. The Day 1 gate is that this has to be
fun before anything from Synty gets imported.

## Getting into a match

1. Open `Assets/_Deadball/Scenes/Arena_Greybox.unity`
2. Press Play

The join manager picks the best setup your machine can support, automatically:

| Connected | P1 | P2 |
|---|---|---|
| Two gamepads | pad (press any button to claim) | pad (press any button to claim) |
| One gamepad + keyboard | **the gamepad** | keyboard, WASD set |
| Keyboard only | WASD set | arrows set |

The match starts the moment both slots are claimed — round card, then fight. To force a specific
setup, set **Join Mode** on `Systems > FighterJoinManager` instead of leaving it on `Auto`.

| Action | Gamepad | Keyboard P1 | Keyboard P2 |
|---|---|---|---|
| Move / face | Left stick | WASD | Arrows |
| Charge & throw | RT (hold/release) | Space | Numpad 0 |
| Dodge roll | A / South | Left Shift | Numpad 1 |
| Catch | LT or B | J | Numpad 2 |

## Where to tune

`Assets/_Deadball/Data/MatchConfig.asset` — every number from GDD §9, tabbed by system.
Start with **Catch → Lockout On Miss**. The design is explicit that it is the most important value
in the game and should be tuned before anything else.

`Assets/_Deadball/Data/FighterPalette.asset` — the cyan/orange slot colours (§11.2). Body, ground
ring, held-ball tint and trail all read from here.

If the catch feels bad, adjust in this order, one at a time (§8.7): widen the active window, push
the flash earlier, shorten the lockout. Never slow the ball down.

## Changing the arena

The arena, the fighter prefab and the greybox materials are committed assets. There is no editor
setup script that regenerates them — the scene is the source of truth. Edit it in the editor, or
drive the editor from the Unity CLI:

```bash
unity command create_gameobject -- --name Prop_Crate --primitive cube
unity command set_transform -- --target Prop_Crate --position "[3,0.5,-4]" --scale "[2,1,2]"
unity command set_layer -- --target Prop_Crate --layer DB_Arena
unity command save_scene
```

Vector parameters take a JSON array in quotes; a bare `-4` is parsed as a flag. `unity list` names
every command, and `unity list --format json` prints their full parameter schemas.

## Tests

18 PlayMode tests cover the Day 1 loop: grab, carry, charge, root, throw, catch on the flash cue,
lockout, dodge, knocks, KO, self-hit immunity, round flow, the Bo3 tally and the comeback handicap.

```bash
unity test "D:/Unity/Zanga/Club Jam" --mode PlayMode
```

They drive real fighters through the real ball in the real generated arena; the only substitution
is the input source.

## Shape of the code

The ball is the only systemic object, and it talks to fighters through `IBallTarget` — never to
`Fighter` directly. Fighters are a facade over four parts that own one rule each (`FighterMotor`,
`FighterThrower`, `FighterCatcher`, `FighterKnocks`), driven by an `IFighterInput`.

That input interface is the Day 2 seam: an AI fighter is another implementation of it on the same
prefab, with no changes to any part. `IFighterRoster` is the matching seam for Solo mode — the match
director asks a roster for its fighters and does not care whether slot 1 is a human or a bot.

Cross-system reactions go over the EventBus (`Deadball.Events`); presentation subscribes and never
polls, so the rules can be re-tuned without touching visuals and the visuals can be rebuilt on Day 3
without risking the rules.

## Known Day 1 notes

- **Ball gravity is 0.9, not real gravity.** At anything near 9.8 a min-charge throw hits the floor
  around 11 m and never reaches the far wall, which kills the ricochets that generate most of the
  game's chaos. This is the "kinematic-ish" flight §6.3 asks for.
- **The dodge saves you by moving, not by i-frames.** On the GDD's own numbers the i-frames (0.20s)
  are shorter than the flash lead (0.35s), so rolling *sideways* is what avoids the ball — rolling
  straight down the barrel still eats it. Worth deciding tonight whether that is the intent.
- **A held ball must never have rigidbody interpolation on.** Unity writes an interpolated
  world-space pose into the transform every frame, which overrides parent-relative placement - the
  ball drifts metres behind the carrier. `BallController` now turns it off for HELD and LOOSE and
  back on only for FLYING, and `HeldBall_StaysInTheHandWhileTheFighterMoves` guards it.
- **Throw loft is an absolute m/s, not a fraction of throw speed.** Scaling it with charge made
  max-charge balls climb for their whole flight and sail over the props instead of hitting them.
  Absolute loft also means a hard throw is flatter than a soft one, which reads better.
- **A loose ball never rests inside geometry.** `GoLoose` used to slam the ball to rest height
  wherever it stopped, so a ball that ended up over a crate was teleported *inside* it - frozen,
  kinematic and unreachable. `ResolveRestingPosition` now spirals outward for a clear spot and falls
  back to the arena centre, per the failsafe idea in §23.
- Layer collision rules are applied in code by `DeadballPhysicsBootstrap` rather than in the project
  collision matrix, which this project shares with TopDownEngine.
- Audio slots on `MatchAudioCues` are wired but empty — Day 2 evening per §21.

## Not built yet

Day 2: AI (`HUNT / AIM / EVADE / REACT`, one `catchChance` float), Synty art, audio.
Day 3: juice checklist, five UI screens, district dressing.
Not started: Split Ball hook (§16), Solo mode menus.
