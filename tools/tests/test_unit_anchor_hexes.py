"""content/map/unit_anchor_hexes.yaml is the committed output of tools/unit_anchor_hexes.py and must stay in sync: the sim
places the starting forces (R-UNIT-006) on these hexes instead of projecting lon/lat itself (docs/determinism.md)."""
import json
from pathlib import Path

import yaml12

import unit_anchor_hexes as uah

ROOT = Path(__file__).resolve().parents[2]
MAP = ROOT / "content" / "map"
HEXES = json.loads((MAP / "hexes.json").read_text())
UNITS = yaml12.load((MAP / "units_1948.yaml").read_text(encoding="utf-8"))["units"]


def test_committed_file_is_up_to_date():
    assert (MAP / "unit_anchor_hexes.yaml").read_text() == uah.render(HEXES, UNITS), "rerun: uv run tools/unit_anchor_hexes.py"


def test_two_known_anchors_are_pinned_literally():
    rows = {(r["id"], r["anchor"]): (r["q"], r["r"]) for r in yaml12.load((MAP / "unit_anchor_hexes.yaml").read_text())["anchors"]}
    assert rows[("carmeli", 0)] == (6, 9)   # Haifa (#743, owner, 20 October 2026), the hex settlement_hexes.yaml gives haifa
    assert rows[("kiryati", 0)] == (-3, 21)   # Tel Aviv, the hex settlement_hexes.yaml gives tel_aviv


def test_an_anchor_on_the_shore_takes_the_land_hex_a_settlement_would():
    """#1183 (4.4 km bake): Haifa's anchor point falls in a sea hex, which the sim refuses (StartingForces). An anchor is
    placed by the settlements' rule (hexgrid.settlement_hex): its own hex if land, else the nearest land neighbour."""
    land = {(h["q"], h["r"]) for h in HEXES["hexes"] if h["land"]}
    rows = yaml12.load(uah.render(HEXES, UNITS))["anchors"]
    assert all((r["q"], r["r"]) in land for r in rows), [r for r in rows if (r["q"], r["r"]) not in land]
