using System.Collections.Generic;
using System.Linq;
using Statecraft.Sim;

/// <summary>content/projects.yaml (R-PROJ-001): the real kinds load with the owner's proposed design values (Q-PROJ-1,
/// Q-HOUSE-3), and the loader refuses a kind that could not be built, would cost less than nothing, or would move a condition
/// out of its 0 to 1 scale in one step.</summary>
public class ProjectsContentTests
{
    [Fact]
    public void TheRealKindsLoad()
    {
        var c = ContentLoader.Load(RealContentTests.ContentRoot());
        Assert.Equal(new[] { ("housing", 6.0, 6, 5, 0.03, 0), ("transit_camp", 1.5, 1, 5, -0.02, 0), ("industry", 2.0, 6, 0, 0.0, 5) },
                     c.ProjectKinds.Select(k => (k.Id, k.Cost, k.Turns, k.Capacity, k.Condition, k.JobSlots)));
        Assert.Equal((1.0, 0.5), (c.Rules.AttentionCost["start_project"], c.Rules.AttentionCost["cancel_project"]));   // Q-PROJ-5
    }

    [Theory]
    [InlineData("id: housing, name: a housing project", "id: transit_camp, name: a housing project", "projects[transit_camp].id")]   // duplicate id
    [InlineData("name: a housing project", "name: ''", "projects[housing].name")]
    [InlineData("cost: 3, turns: 2", "cost: -1, turns: 2", "projects[housing].cost")]
    [InlineData("cost: 3, turns: 2", "cost: 3, turns: 0", "projects[housing].turns")]
    [InlineData("capacity: 4", "capacity: -1", "projects[housing].capacity")]
    [InlineData("condition: 0.1, job_slots: 0 }\n  - { id: transit_camp", "condition: 1.5, job_slots: 0 }\n  - { id: transit_camp", "projects[housing].condition")]
    [InlineData("job_slots: 3", "job_slots: -3", "projects[industry].job_slots")]
    public void ABadKindIsRefused(string from, string to, string where)
    {
        Assert.Contains(from, Fixture.GddProjects);
        var dir = Fixture.Write(Fixture.Gdd(new Dictionary<string, string> { ["projects.yaml"] = Fixture.GddProjects.Replace(from, to) }));
        var e = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));
        Assert.Contains(where, e.Message);
    }
}
