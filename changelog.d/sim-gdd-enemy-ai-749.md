- GDD Phase 5 PR 10, part 1: the enemy AI (refs #749, #385). Resolver step 1 now gives every Arab unit its orders from
  the state alone, through the player's own move, attack and fortify checks with the sides swapped (R-ENEMY-001 to
  R-ENEMY-004). In Historical mode the Egyptian force, the Arab Legion, the Syrian and the Iraqi forces march from
  their 15 May 1948 start on their 11 June positions in `map/units_1948.yaml` (Isdud, Latrun, the Mishmar HaYarden
  bridgehead, Jenin), taking only anchors with two sources that are not Wikipedia (research note arab-forces-1948.md,
  section 10), on the turns before 8 July 1948. Otherwise a unit holds its hex fortified and attacks a neighbouring
  Israeli unit at 2:1 or better (`mvp.enemy_attack_ratio_percent`, a new labelled design value). No RNG, no enemy
  supply, no reinforcements. An enemy unit may not enter a locality that is not on `mvp.enemy_held_localities`, so no
  enemy move ends on an Israeli locality before enemy capture exists. Every golden moves (re-pinned, docs/determinism.md).
