using System;
using System.Linq;
using Statecraft.Sim;

/// <summary>The D-C5 choices in the GDD loop (Phase 2 plan, PR 6): the cabinet, the coalition votes, the budget and the
/// policies keep running as they do today (Q-GOV-1), but the coalition can no longer end the game, and Attention prices
/// the GDD orders.</summary>
public class GovernmentTests
{
    static GameState CollapsingCoalition(Content c)
    {
        var s = Simulation.NewGame(c, 1, Mode.Historical);
        s.CoalitionStability = 0.1;
        s.Indicators["morale"] = 0.0;   // approval falls, so stability stays under the line through the turn
        return s;
    }

    // R-GOV-001 (D-C5, D-B5: "Coalition collapse is off in the MVP")
    [Fact]
    public void StabilityBelowTheLineDoesNotEndTheGddGame()
    {
        var c = ContentLoader.Load(Fixture.Write(Fixture.Gdd()));
        Assert.True(c.IsGdd);
        var r = Simulation.Step(c, CollapsingCoalition(c), Array.Empty<Command>());
        Assert.True(r.State.CoalitionStability < PoliticsSystem.CollapseBelow, $"stability {r.State.CoalitionStability}");
        Assert.Null(r.State.GameOver);
        Assert.DoesNotContain(r.Why, w => w.Target == "game_over");
    }

    // R-GOV-001: the legacy loop keeps its collapse while the legacy code exists (D-E1).
    [Fact]
    public void StabilityBelowTheLineStillEndsTheLegacyGame()
    {
        var c = ContentLoader.Load(Fixture.Write());
        Assert.False(c.IsGdd);
        var r = Simulation.Step(c, CollapsingCoalition(c), Array.Empty<Command>());
        Assert.Equal(GameOverCodes.CoalitionCollapse, r.State.GameOver);
    }

    // R-GOV-002: every GDD order has a price in the real rules, and the unit orders are free (D-A6).
    [Fact]
    public void EveryGddKindHasACostAndUnitOrdersAreFree()
    {
        var c = ContentLoader.Load(RealContentTests.ContentRoot());
        var gdd = CommandGuardTests.Samples.Values.Where(cmd => AttentionSystem.IsGddKind(cmd.Kind)).ToList();
        Assert.NotEmpty(gdd);
        foreach (var cmd in gdd) Assert.True(c.Rules.AttentionCost.ContainsKey(cmd.Kind), $"{cmd.Kind} has no attention_cost");
        Assert.Equal(0, c.Rules.AttentionCost["pioneer_work"]);
        Assert.Equal(0, c.Rules.AttentionCost["stop_work"]);
    }

    // R-GOV-002: a refused GDD order costs nothing. The samples name a city, a cohort and a unit the GDD fixture does not
    // have, so each is refused. Attention refills at the end of the step, so the end state cannot show a refund: the turn
    // holds only enough Attention for the dearer of the refused order and a policy after it, and the policy still goes
    // through. Without the refund, any priced order would leave too little for the policy.
    [Fact]
    public void ARefusedGddOrderRefundsItsAttention()
    {
        var c = ContentLoader.Load(Fixture.Write(Fixture.Gdd()));
        var policy = new SetPolicy("rationing", 0.5);
        foreach (var cmd in CommandGuardTests.Samples.Values.Where(cmd => AttentionSystem.IsGddKind(cmd.Kind)))
        {
            var s = Simulation.NewGame(c, 1, Mode.Historical);
            s.Attention = Math.Max(AttentionSystem.Cost(c, s, cmd.Kind), AttentionSystem.Cost(c, s, policy.Kind));
            var r = Simulation.Step(c, s, new Command[] { cmd, policy });
            Assert.Same(cmd, Assert.Single(r.Rejected).Command);
        }
    }
}
