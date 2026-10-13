#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Statecraft.Game.Map.Kits
{
    /// <summary>The ma'abara kit (issue #32): the player's transit camps (MapActions.TransitCamp, from 1950) drawn as the camps
    /// of 1950-51 looked, rows of white and khaki canvas tents (ridge and bell tents) on a regular grid with a few tin or
    /// canvas huts, a long communal hut (showers, kitchen) and, in some, a water tower. Built by PlacementLayer when a camp is
    /// placed, never at map build (the boot budget, #321). Seeded by the camp's id; presentation only, no sim state.</summary>
    public static partial class SettlementKits
    {
        /// <summary>The share of a ma'abara's plots that hold a hut rather than a tent: 1950-51, mostly tents. A parameter so a
        /// staged look (more huts as the camps aged) only has to pass another share.</summary>
        public const float MaabaraHutShare = 0.2f;

        /// <summary>The plot grid (columns along the rows, rows across), its pitch (real m, before the house scale) and the
        /// radius of the lot each plot claims: the largest tent or hut (6.5 x 4.5) with a little to spare. The 1948 Haifa
        /// transit-camp photographs (Nationaal Archief, Van de Poll) show tents 2-3 m apart along a row and 5-6 m lanes
        /// between rows. The lot is the same whatever stands on it, so the hut share changes what a plot holds, never which
        /// plots are used.</summary>
        const int MaabaraCols = 5, MaabaraRows = 4;
        const float MaabaraPitchU = 8f, MaabaraPitchV = 9f, MaabaraPlotR = 4f;

        /// <summary>How far (house-scale metres) a ma'abara reaches from its centre: the communal hut past the last column
        /// (30 along, 9 to a side) and the rows' half width, rounded up.</summary>
        public const float MaabaraReach = 36f;

        /// <summary>How far from its hex's centre, in hexes, a camp looks for free ground. Camps go on or next to a town's
        /// hex (rules map_action_max_distance), and a big town's plots cover its hex, so the camp stands at the town's edge,
        /// as the ma'abarot did.</summary>
        const float MaabaraSearchHexes = 3f;

        /// <summary>Builds one ma'abara per entry of <paramref name="camps"/> (seed id, the centre of its hex in world x, z), in
        /// order, on one lot list so no two overlap: a mesh in world space (null when no free ground was found) and the tents
        /// and huts it holds (the communal hut and the water tower not counted).</summary>
        public static List<(Mesh? mesh, int tents, int huts)> Maabarot(IReadOnlyList<(string id, Vector2 at)> camps, KitGround ground,
            float hutShare = MaabaraHutShare)
        {
            var lots = new Lots();
            var result = new List<(Mesh?, int, int)>();
            foreach (var (id, at) in camps)
            {
                var km = new KitMesh();
                var (tents, huts) = Maabara(id, at, ground, km, lots, hutShare);
                result.Add((km.VertexCount > 0 ? km.ToMesh(id + "_kit") : null, tents, huts));
            }
            return result;
        }

        /// <summary>As Maabarot, also listing everything placed (<see cref="Placed"/>: "tent", "hut", "communal_hut",
        /// "water_tower"), in placement order.</summary>
        public static List<(Mesh? mesh, int tents, int huts)> Maabarot(IReadOnlyList<(string id, Vector2 at)> camps, KitGround ground,
            out List<Placed> placed, float hutShare = MaabaraHutShare)
        {
            census = placed = new List<Placed>();
            try { return Maabarot(camps, ground, hutShare); }
            finally { census = null; }
        }

        static (int tents, int huts) Maabara(string id, Vector2 hexCentre, KitGround ground, KitMesh km, Lots lots, float hutShare)
        {
            float m = KitScale.Houses;
            if (!MaabaraSpot(hexCentre, MaabaraReach * m, ground, lots, out var c)) return (0, 0);
            var rnd = new KitRandom(id, 1);
            float yaw = rnd.Range(0f, 90f), a = yaw * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 At(float u, float v) => c + new Vector2(u * ca - v * sa, u * sa + v * ca);
            // As the Bedouin camps' tents (Encampments): off the routes and the units' clearings, the unit tokens, the hex
            // grid's edge marks and steep ground.
            bool Free(Vector2 p, float r) => ground.Inside(p.x, p.y) && !ground.Water(p.x, p.y) && OffTowns(p, r, ground)
                && !ground.OnRoute(p, r) && !ground.OnToken(p, r) && !ground.OnGridMark(p, r) && ground.Slope(p.x, p.y, 600f) <= 0.12f
                && lots.Free(p, r, id, 0);
            // Huts at an even spread over the plots (a golden-ratio sequence from a seeded phase), not a coin per plot: the
            // camp shows the share asked for, not a lucky draw of it.
            float phase = rnd.Value();
            int tents = 0, huts = 0, plot = 0;
            float plotR = MaabaraPlotR * m;
            for (int j = 0; j < MaabaraRows; j++)
                for (int i = 0; i < MaabaraCols; i++, plot++)
                {
                    bool hut = Mathf.Repeat(phase + plot * 0.618034f, 1f) < hutShare;
                    var p = At((i - (MaabaraCols - 1) * 0.5f) * MaabaraPitchU * m, (j - (MaabaraRows - 1) * 0.5f) * MaabaraPitchV * m);
                    float pyaw = yaw + rnd.Range(-3f, 3f);   // pitched by hand: straight rows, not ruled ones
                    if (!Free(p, plotR)) continue;
                    lots.Add(new Lot { c = p, r = plotR, id = id, group = 0 });
                    if (hut)
                    {
                        // Corrugated tin (pale grey) or canvas on a wooden frame, under a low gable.
                        float w = rnd.Range(5.5f, 6.5f) * m, d = rnd.Range(3.8f, 4.5f) * m;
                        bool tin = rnd.Chance(0.6f);
                        var wall = tin ? Jitter(C(0.72f, 0.74f, 0.75f), ref rnd, 0.03f) : Jitter(C(0.70f, 0.64f, 0.50f), ref rnd, 0.05f);
                        var roof = tin ? Jitter(C(0.63f, 0.65f, 0.66f), ref rnd, 0.03f) : Jitter(C(0.58f, 0.53f, 0.42f), ref rnd, 0.05f);
                        GabledBox(km, p, pyaw, w, d, 2.2f, 0.7f, wall, roof, ground, m);
                        Record(id, "hut", p, pyaw, w, d, 2.2f * m, wall);
                        huts++;
                        continue;
                    }
                    // Canvas, white or khaki: the Bedouin camps' tent (low walls under a peaked roof) in army canvas, or a
                    // round bell tent.
                    var canvas = rnd.Chance(0.6f) ? Jitter(C(0.88f, 0.86f, 0.79f), ref rnd, 0.04f) : Jitter(C(0.72f, 0.67f, 0.52f), ref rnd, 0.05f);
                    if (rnd.Chance(0.15f))
                    {
                        float r = rnd.Range(2.3f, 2.7f) * m;
                        var (bottom, top) = GroundSpan(p, 0f, 2f * r, 2f * r, ground, m);
                        km.Shaft(new Vector3(p.x, bottom, p.y), new Vector3(p.x, top + 0.9f * m, p.y), r, r, canvas, 8);
                        km.Cone(new Vector3(p.x, top + 0.9f * m, p.y), r * 1.05f, 2.2f * m, canvas, 8);
                        Record(id, "tent", p, 0f, 2f * r, 2f * r, 0.9f * m, canvas);
                    }
                    else
                    {
                        float w = rnd.Range(5f, 6f) * m, d = rnd.Range(3.6f, 4.2f) * m;
                        GabledBox(km, p, pyaw, w, d, 1f, 1.6f, canvas, canvas, ground, m);
                        Record(id, "tent", p, pyaw, w, d, 1f * m, canvas);
                    }
                    tents++;
                }
            if (tents + huts == 0) return (0, 0);
            // The communal hut past the last column, across the rows (a plot of the grid's own lattice: clear of its tents by
            // construction).
            var hall = At(((MaabaraCols - 1) * 0.5f * MaabaraPitchU + 14f) * m, 0f);
            float hallR = 0.5f * Mathf.Sqrt(18f * 18f + 6f * 6f) * m;
            if (Free(hall, hallR))
            {
                lots.Add(new Lot { c = hall, r = hallR, id = id, group = 0 });
                var tin = Jitter(C(0.72f, 0.74f, 0.75f), ref rnd, 0.03f);
                GabledBox(km, hall, yaw + 90f, 18f * m, 6f * m, 2.6f, 1f, tin, Jitter(C(0.63f, 0.65f, 0.66f), ref rnd, 0.03f), ground, m);
                Record(id, "communal_hut", hall, yaw + 90f, 18f * m, 6f * m, 2.6f * m, tin);
            }
            // A water tower at the other end in some camps (the moshavot's, SettlementKits.Landmarks.cs). Its spot moves up to
            // 20 m (house scale) from the one asked for, so that whole reach must be off the towns.
            var tower = At(-((MaabaraCols - 1) * 0.5f * MaabaraPitchU + 10f) * m, 0f);
            if (rnd.Chance(0.5f) && OffTowns(tower, 20f * m, ground) && !ground.OnToken(tower, 20f * m)) WaterTower(id, tower, ground, km, lots);
            return (tents, huts);
        }

        /// <summary>A tent or hut: walls of <paramref name="wallM"/> under a gable <paramref name="riseM"/> high (real m),
        /// both times the house scale <paramref name="m"/>, as the Bedouin camps pitch theirs (Encampments).</summary>
        static void GabledBox(KitMesh km, Vector2 p, float yaw, float w, float d, float wallM, float riseM, Color32 wall, Color32 roof,
            KitGround ground, float m)
        {
            var (bottom, top) = GroundSpan(p, yaw, w, d, ground, m);
            km.Box(p.x, p.y, yaw, w, d, bottom, top, wallM * m, wall, roof, Facade.Plain, m, roofed: true);
            km.Gable(p.x, p.y, yaw, w, d, top + wallM * m, riseM * m, roof, wall);
        }

        /// <summary>The camp's centre: the dry spot nearest <paramref name="want"/> (rings of half a reach, out to
        /// <see cref="MaabaraSearchHexes"/> hexes) whose inner half stays off every town's plots, that is gentle and whose
        /// whole reach is clear of the unit tokens (Encampments' tests) and of the camps built before it. False when there is
        /// none.</summary>
        static bool MaabaraSpot(Vector2 want, float reach, KitGround ground, Lots lots, out Vector2 at)
        {
            float step = reach * 0.5f, max = ground.HexSizeM * MaabaraSearchHexes;
            for (int ring = 0; ring * step <= max; ring++)
            {
                int around = ring == 0 ? 1 : 6 * ring;
                for (int k = 0; k < around; k++)
                {
                    float a = 2f * Mathf.PI * k / around;
                    var p = want + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring * step);
                    if (!ground.Inside(p.x, p.y) || ground.Water(p.x, p.y) || !OffTowns(p, step, ground) || ground.Slope(p.x, p.y, 600f) > 0.12f
                        || ground.OnToken(p, reach) || !lots.Free(p, reach)) continue;
                    at = p; return true;
                }
            }
            at = want; return false;
        }

        /// <summary>No kit footprint (a town's, a 1948 locality's or a Bedouin camp's, out to <see cref="PlotReach"/> of its
        /// radius, where a town's plots end) reaches a disc of radius <paramref name="r"/> at <paramref name="p"/>. A linear
        /// scan: some 350 footprints, and only when a camp is placed.</summary>
        static bool OffTowns(Vector2 p, float r, KitGround ground)
        {
            foreach (var f in ground.Footprints)
            {
                float d = f.radius * PlotReach + r;
                if ((p - f.centre).sqrMagnitude < d * d) return false;
            }
            return true;
        }
    }
}
