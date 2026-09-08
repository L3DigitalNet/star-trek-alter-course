using System.Reflection;
using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves faction assignment depends only on the approved own-asset administrative snapshot.</summary>
public sealed class FactionKnowledgeBoundaryTests
{
    private readonly FactionAssignmentProofFixture _fixture = new();

    /// <summary>Confirms real foreign truth, local contacts, and an active NPC scan cannot affect a decision.</summary>
    [Fact]
    public void HiddenWorldAndOwnShipSensorStateDoNotChangeDecisionOrExplanation()
    {
        GameSimulation concealed = _fixture.CreateKnowledgeVariant(false);
        GameSimulation observed = _fixture.CreateKnowledgeVariant(true);
        ShipState observedAsset = observed
            .CaptureState()
            .GetRequiredShip(FactionAssignmentProofFixture.PreferredShipId);

        Assert.NotEmpty(observedAsset.SensorKnowledge.Contacts);
        Assert.NotNull(observedAsset.SensorKnowledge.ActiveScan);
        FactionAssignmentDecisionExplanation concealedDecision = Decide(concealed);
        FactionAssignmentDecisionExplanation observedDecision = Decide(observed);

        Assert.Equal(concealedDecision, observedDecision);
        Assert.Equal(concealedDecision.ActorKnownFacts, observedDecision.ActorKnownFacts);
        Assert.Equal(concealedDecision.Proposal, observedDecision.Proposal);
        Assert.Equal(concealedDecision.Candidates, observedDecision.Candidates);
    }

    /// <summary>Confirms the full public decision graph has no path to runtime ships or sensor knowledge.</summary>
    [Fact]
    public void DecisionGraphRecursivelyExcludesLiveStateAndUsesReadOnlyCollections()
    {
        HashSet<Type> graph = PublicGraph(typeof(FactionAssignmentDecisionExplanation));

        Assert.DoesNotContain(typeof(SimulationState), graph);
        Assert.DoesNotContain(typeof(ShipState), graph);
        Assert.DoesNotContain(typeof(SensorKnowledge), graph);
        Assert.DoesNotContain(graph, type => type.IsArray);
        Assert.Equal(
            typeof(IReadOnlyList<FactionShipAssignmentSnapshot>),
            typeof(FactionAssignmentDecisionInput)
                .GetProperty(nameof(FactionAssignmentDecisionInput.Assets))!
                .PropertyType
        );
        Assert.Equal(
            typeof(IReadOnlyList<FactionKnownRouteSnapshot>),
            typeof(FactionAssignmentDecisionInput)
                .GetProperty(nameof(FactionAssignmentDecisionInput.KnownRoutes))!
                .PropertyType
        );
        Assert.Equal(
            typeof(IReadOnlyList<FactionAssignmentDecisionCandidate>),
            typeof(FactionAssignmentDecisionExplanation)
                .GetProperty(nameof(FactionAssignmentDecisionExplanation.Candidates))!
                .PropertyType
        );
    }

    private static FactionAssignmentDecisionExplanation Decide(GameSimulation game)
    {
        SimulationState state = game.CaptureState();
        FactionState faction = state.GetRequiredFaction(FactionAssignmentProofFixture.FactionA);
        return GameSimulation.DecideFactionAssignment(state, faction, faction.PresenceObjective!);
    }

    private static HashSet<Type> PublicGraph(Type root)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.TryPop(out Type? current))
        {
            if (!visited.Add(current))
                continue;
            foreach (PropertyInfo property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Type propertyType = property.PropertyType;
                pending.Push(propertyType);
                foreach (Type argument in propertyType.GetGenericArguments())
                    pending.Push(argument);
            }
        }
        return visited;
    }
}
