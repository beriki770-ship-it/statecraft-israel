- Deport, GDD Phase 6 PR 8 (#800; R-CTRL-018 to R-CTRL-022), under owner ruling 1, "honest cost, no reward". It stays
  out of play: with `mvp.deport_in_play: false` in the real content, `ResolveCapture(settlement, deport)` is still refused
  as not built yet until the owner has reviewed its cards and dialog (PRs 9 and 10). Under the switch, the units leave as
  in Raid, Social cohesion and International Support fall at once, the place counts in `DisplacedByPlayer` (places, not
  people), International Support drags each turn and has a lower ceiling for the rest of the game, and the place is
  depopulated: no city project or corridor segment on its hex, no timer, no gain. No razing in the MVP.
  Deport also costs the Treasury IL 6.25m at once (owner decision, 22 October 2026, #1327), so `DeportNeverDominates`
  checks Deport is never better than Raid or Occupy on anything, Money included, in every state. The four indicator sizes
  are design values proposed on #800. A depopulated place stays closed to the player's units. No golden moves.
