using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Statecraft.Sim
{
    /// <summary>One unit a Historical game starts with (R-UNIT-006): the units_1948.yaml row it comes from, the anchor it
    /// stands at, its type, side and strength in population units, and the anchor's hex.</summary>
    public sealed class StartingForce
    {
        public string Row { get; set; } = "";
        /// <summary>The anchor's place, as the row names it.</summary>
        public string Anchor { get; set; } = "";
        public string Type { get; set; } = "";
        public string Side { get; set; } = "";
        public int Strength { get; set; }
        public int Q { get; set; }
        public int R { get; set; }
        /// <summary>The row's review status (draft, to_verify, fact-checked); the release gate reads it (Q-START-3).</summary>
        public string Status { get; set; } = "";
    }

    /// <summary>The sourced starting forces, both sides (GDD Phase 5 PR 4; docs/rules/mvp.md R-UNIT-006, R-UNIT-007).
    /// The sim reads map/units_1948.yaml (Q-START-1, owner, 15 October 2026), so the file is in the content hash. Only the
    /// keys the start needs are read, untyped, as for settlements_1948.yaml: the map's reader (Unity's UnitData) and
    /// tools/tests/test_units_1948.py own the rest of the row.</summary>
    public static class StartingForces
    {
        public const string FileName = "map/units_1948.yaml";
        /// <summary>Every IDF brigade starts as a veteran soldier (#743, owner, 20 October 2026).</summary>
        public const string IsraeliBrigadeType = "veteran_soldier";

        /// <summary>The starting forces in the file's order (R-UNIT-006): every row that is not hidden, is an army row (the
        /// squadron and the ships stay tokens, Q-UNIT-5), has a strength for the start day and an anchor window
        /// [from, to) covering it. The first such anchor places the unit on the hex holding its coordinates, by the map's
        /// projection and rounding (tools/hexgrid.py, Unity's HexPicker). Strength is the row's people in population units,
        /// the nearest unit with a half up and at least 1 (#743). A row it cannot place is a load error.</summary>
        internal static List<StartingForce> Read(Func<string, string?> readFile, Content c, double lon0, double lat0, double lat1, double hexSizeKm)
        {
            string? text;
            try { text = readFile(FileName); }
            catch (System.IO.IOException e) { throw new ContentException(FileName, "(file)", e.Message); }
            catch (UnauthorizedAccessException e) { throw new ContentException(FileName, "(file)", e.Message); }
            if (text == null) throw new ContentException(FileName, "(file)", "missing file");
            Dictionary<string, List<Dictionary<object, object>>>? file;
            try { file = new DeserializerBuilder().Build().Deserialize<Dictionary<string, List<Dictionary<object, object>>>>(text); }
            catch (YamlException e) { throw new ContentException(FileName, $"line {e.Start.Line}", e.InnerException?.Message ?? e.Message); }
            if (file == null || !file.TryGetValue("units", out var rows) || rows == null) throw new ContentException(FileName, "units", "required");
            var start = c.Rules.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var ppu = c.RulesAt(c.Rules.Start).Mvp!.PeoplePerUnit;   // the start day's rules, as Cities.Init reads them
            var list = new List<StartingForce>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i] ?? throw new ContentException(FileName, $"units[{i}]", "empty row");
                var id = Text(row, "id");
                if (id == "") throw new ContentException(FileName, $"units[{i}].id", "required");
                var at = $"units[{id}]";
                if (row.ContainsKey("hidden") || Text(row, "service") != "army") continue;
                if (!(row.TryGetValue("strength", out var so) && so is Dictionary<object, object> strength) || Text(strength, "as_of") != start) continue;
                if (!(row.TryGetValue("anchors", out var ao) && ao is List<object> anchors)) continue;
                int k = anchors.FindIndex(a => a is Dictionary<object, object> m
                    && string.CompareOrdinal(Text(m, "from"), start) <= 0 && string.CompareOrdinal(start, Text(m, "to")) < 0);
                if (k < 0) continue;
                var anchor = (Dictionary<object, object>)anchors[k];
                var side = Text(row, "side");
                var people = Number(strength, "value", $"{at}.strength.value");
                if (people < 0 || people != decimal.Truncate(people)) throw new ContentException(FileName, $"{at}.strength.value", "must be a whole number >= 0");
                var units = Cities.StartUnits(people, ppu);   // the nearest unit, a half up, at least 1 for a force with people
                if (units == 0) continue;                     // a strength of nobody starts nothing
                var (q, r) = HexOf(Coordinate(anchor, "lon", $"{at}.anchors[{k}].lon"), Coordinate(anchor, "lat", $"{at}.anchors[{k}].lat"), lon0, lat0, lat1, hexSizeKm);
                var hex = c.HexAt(q, r);
                if (hex == null) throw new ContentException(FileName, $"{at}.anchors[{k}]", FormattableString.Invariant($"hex q={q} r={r} is not on the map ({ContentLoader.HexesFile})"));
                if (!hex.Land) throw new ContentException(FileName, $"{at}.anchors[{k}]", FormattableString.Invariant($"hex q={q} r={r} is not a land hex in {ContentLoader.HexesFile}"));
                list.Add(new StartingForce
                {
                    Row = id, Anchor = Text(anchor, "place"), Type = TypeFor(c, side, at), Side = side,
                    Strength = units, Q = q, R = r, Status = Text(row, "status"),
                });
            }
            return list;
        }

        /// <summary>Adds the starting forces to a new game, in the content's order, taking the next ids: the unit with id i
        /// of a new game is <c>c.StartingForces[i]</c>.</summary>
        public static void Init(Content c, GameState s)
        {
            foreach (var f in c.StartingForces)
            {
                var id = s.NextId++;
                s.Units.Add(id, new Unit(id, f.Type, f.Q, f.R, side: f.Side, strength: f.Strength));
            }
        }

        /// <summary>A starting row's unit type: Israel's brigade type, or the one military type of the row's side (one
        /// labelled design-value infantry type per Arab side with a row; #743, owner, 20 October 2026).</summary>
        static string TypeFor(Content c, string side, string at)
        {
            var types = side == "israel"
                ? c.UnitTypes.Where(t => t.Id == IsraeliBrigadeType && t.Side == side).ToList()
                : c.UnitTypes.Where(t => t.Side == side && side != "").ToList();
            if (types.Count != 1)
                throw new ContentException(FileName, at + ".side", types.Count == 0
                    ? $"no military type of side '{side}' in unit_types.yaml" + (side == "israel" ? $" (the brigades start as {IsraeliBrigadeType})" : "")
                    : $"side '{side}' has several military types in unit_types.yaml ({string.Join(", ", types.Select(t => t.Id))}); a starting row needs one");
            return types[0].Id;
        }

        /// <summary>lon, lat to the axial hex holding it: tools/hexgrid.py's lonlat_to_km and point_to_hex (cube rounding),
        /// as Unity's HexPicker.FromLonLat does it, so the sim's hex is the one the map computes for the same point.</summary>
        static (int q, int r) HexOf(double lon, double lat, double lon0, double lat0, double lat1, double size)
        {
            const double KmPerDegLat = 110.57, KmPerDegLonEq = 111.32;   // tools/geo.py
            var x = (lon - lon0) * (KmPerDegLonEq * Math.Cos((lat0 + lat1) / 2 * (Math.PI / 180)));
            var y = (lat1 - lat) * KmPerDegLat;
            var qf = (Math.Sqrt(3) / 3 * x - y / 3) / size;
            var rf = (2.0 / 3 * y) / size;
            var sf = -qf - rf;
            double q = Math.Round(qf, MidpointRounding.ToEven), r = Math.Round(rf, MidpointRounding.ToEven), s = Math.Round(sf, MidpointRounding.ToEven);
            double dq = Math.Abs(q - qf), dr = Math.Abs(r - rf), ds = Math.Abs(s - sf);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return ((int)q, (int)r);
        }

        static string Text(Dictionary<object, object> m, string key) => m.TryGetValue(key, out var v) && v is string s ? s : "";

        static decimal Number(Dictionary<object, object> m, string key, string field) =>
            decimal.TryParse(Text(m, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d : throw new ContentException(FileName, field, "required, a number");

        /// <summary>A coordinate as a double, parsed as the map's reader parses it, so both round the same point.</summary>
        static double Coordinate(Dictionary<object, object> m, string key, string field) =>
            double.TryParse(Text(m, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN(d) && !double.IsInfinity(d)
                ? d : throw new ContentException(FileName, field, "required, a number");
    }
}
