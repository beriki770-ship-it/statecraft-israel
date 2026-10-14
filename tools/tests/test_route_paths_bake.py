"""The route paths' runtime form (content/map/route_paths_1948.tsv, issue #321) is the bake of the shipped YAML.

The player reads the runtime file at boot instead of parsing route_paths_1948.yaml, and refuses it (reading the YAML, with
a warning: slower, never wrong) when its version or its check of the YAML does not match (RoutePaths.ParseRuntime). CI
runs no Unity, so this checks here:
- the committed file is a fresh bake of the YAML (tools/route_paths_bake.py --check), so the YAML and the runtime file
  never drift in a merged tree;
- the bake's VERSION is RoutePaths.RuntimeVersion;
- a fingerprint of the C# that reads the runtime file and the YAML (comments and whitespace left out), so a change to
  either reader fails here until someone checks the two still agree (Unity's RoutePathsRuntimeTests compares every
  route and point), bumps the version if the layout changed, re-bakes and re-pins. The bake checks the file's data; it
  cannot check the C# that reads it (review of #529, S1).
"""
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import route_paths_bake as bake  # noqa: E402

SCRIPTS = ROOT / "unity/Assets/Scripts/Map/Routes"
REBAKE = "run uv run tools/route_paths_bake.py"

# The fingerprint of the readers (code_fingerprint). Re-pin only after RoutePathsRuntimeTests passes in Unity, with
# RoutePaths.RuntimeVersion and route_paths_bake.VERSION bumped together if the runtime layout changed.
PINNED_CODE = "a7cc9cf147bca7ec7bcf3a769fd86550bd4a92bbcfeea98ab0f74e60908fca00"


def member(text: str, signature: str) -> str:
    """The text of the member starting at signature, through its closing brace."""
    i = text.index(signature)
    depth = 0
    for j in range(i, len(text)):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                return text[i:j + 1]
    raise AssertionError(f"{signature}: no end")


def code_fingerprint() -> str:
    paths = (SCRIPTS / "RoutePaths.cs").read_text()
    ribbon = (SCRIPTS / "RouteRibbonFile.cs").read_text()
    parts = [
        member(paths, "public sealed class RoutePath\n"),
        member(paths, "public static List<RoutePath> ReadRuntimeOrYaml("),
        member(paths, "public static List<RoutePath> ParseRuntime("),
        member(paths, "static RoutePathFile RuntimeDoc("),
        member(paths, "public static int BodyCheck("),
        member(paths, "static (int version, int check, List<RoutePath> routes) RuntimeRows("),
        member(paths, "public static List<RoutePath> Parse("),
        member(paths, "static List<RoutePath> Validate("),
        member(ribbon, "public static int Check("),
    ]
    code = "\n".join(re.sub(r"\s+", " ", re.sub(r"//[^\n]*", "", p)).strip() for p in parts)
    return hashlib.sha256(code.encode()).hexdigest()


def test_the_runtime_file_is_the_bake_of_the_yaml():
    assert bake.RUNTIME.read_text(encoding="utf-8") == bake.bake(bake.SOURCE.read_bytes()), f"stale: {REBAKE}"


def test_the_bake_and_the_reader_share_a_version():
    m = re.search(r"\bRuntimeVersion\s*=\s*(\d+)\b", (SCRIPTS / "RoutePaths.cs").read_text())
    assert m and int(m.group(1)) == bake.VERSION, "RoutePaths.RuntimeVersion and route_paths_bake.VERSION differ"


def test_the_readers_are_the_ones_the_runtime_file_was_checked_against():
    assert code_fingerprint() == PINNED_CODE, (
        f"the C# that reads route_paths_1948.yaml or its runtime form changed (fingerprint {code_fingerprint()}): run "
        "RoutePathsRuntimeTests in Unity, bump the version on both sides if the layout changed, re-bake, then re-pin PINNED_CODE")


def test_the_check_is_the_ribbon_bakes():
    """The runtime file's yaml_check is the same FNV-1a the ribbon bake's header holds for the YAML."""
    from test_route_ribbon_bake import text_check
    assert bake.yaml_check(bake.SOURCE.read_bytes()) == text_check(bake.SOURCE)


def test_the_file_ends_with_its_body_check():
    """The last row is body_check over the route rows (review of #547, S1): a file cut anywhere, even inside a number,
    loses it or no longer matches it, and RoutePaths.RuntimeRows refuses it."""
    lines = bake.RUNTIME.read_text(encoding="utf-8").rstrip("\n").split("\n")
    kind, name, value = lines[-1].split("\t")
    assert (kind, name) == ("meta", "body_check")
    rows = [line for line in lines if line.startswith("route\t")]
    assert int(value) == bake.body_check(rows)
    cut = rows[-1][:-1]                                       # the last number a digit short
    assert bake.body_check(rows[:-1] + [cut]) != int(value)
