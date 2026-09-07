// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

/// <summary>Verifies the repository project graph and its bounded normative direction rules.</summary>
public sealed class ProjectReferenceGraphTests
{
    /// <summary>Verifies the evaluated repository graph is acyclic and respects checked package ranks.</summary>
    [Fact]
    public void Validate_WhenRepositorySourceGraphIsLoaded_AcceptsCheckedDirections()
    {
        var graph = ProjectReferenceGraph.LoadRepository();

        Should.NotThrow(graph.Validate);
    }

    /// <summary>Verifies a directed cycle is rejected.</summary>
    [Fact]
    public void Validate_WhenGraphContainsCycle_RejectsIt()
    {
        var graph = Graph(("a", ["b"]), ("b", ["a"]));

        _ = Should.Throw<InvalidOperationException>(graph.Validate);
    }

    /// <summary>Verifies the facade cannot acquire a concrete runtime dependency.</summary>
    [Fact]
    public void Validate_WhenFacadeReferencesRuntime_RejectsIt()
    {
        var graph = Graph(("AgentKit", ["AgentKit.Loop"]), ("AgentKit.Loop", []));

        _ = Should.Throw<InvalidOperationException>(graph.Validate);
    }

    /// <summary>Verifies the two lowest shared projects cannot acquire outward implementation dependencies.</summary>
    /// <param name="source">The checked foundation project.</param>
    /// <param name="target">The forbidden outward dependency.</param>
    [Theory]
    [InlineData("AgentKit.Abstractions", "AgentKit.Loop")]
    [InlineData("AgentKit.Observability", "AgentKit.Loop")]
    public void Validate_WhenFoundationReferencesRuntime_RejectsIt(string source, string target)
    {
        var graph = Graph((source, [target]), (target, []));

        _ = Should.Throw<InvalidOperationException>(graph.Validate);
    }

    /// <summary>Verifies shared observability is a legal facade and runtime dependency.</summary>
    [Fact]
    public void Validate_WhenFacadeAndRuntimeUseSharedObservability_AcceptsIt()
    {
        var graph = Graph(
            ("AgentKit", ["AgentKit.Abstractions", "AgentKit.Observability"]),
            ("AgentKit.Loop", ["AgentKit.Abstractions", "AgentKit.Observability"]),
            ("AgentKit.Observability", ["AgentKit.Abstractions"]),
            ("AgentKit.Abstractions", []));

        Should.NotThrow(graph.Validate);
    }

    /// <summary>Verifies a behavioral runtime cannot reference a sibling runtime or concrete leaf.</summary>
    /// <param name="source">The checked behavioral runtime.</param>
    /// <param name="target">The forbidden implementation dependency.</param>
    [Theory]
    [InlineData("AgentKit.Loop", "AgentKit.Session")]
    [InlineData("AgentKit.Loop", "AgentKit.Session.InMemory")]
    [InlineData("AgentKit.Durability", "AgentKit.Session")]
    [InlineData("AgentKit.Memory", "AgentKit.Session")]
    public void Validate_WhenBehavioralRuntimeReferencesImplementation_RejectsIt(string source, string target)
    {
        var graph = Graph((source, [target]), (target, []));

        _ = Should.Throw<InvalidOperationException>(graph.Validate);
    }

    /// <summary>Verifies a leaf may reference the runtime surface it adapts.</summary>
    [Fact]
    public void Validate_WhenLeafReferencesRuntime_AcceptsIt()
    {
        var graph = Graph(("AgentKit.Session.InMemory", ["AgentKit.Session"]), ("AgentKit.Session", []));

        Should.NotThrow(graph.Validate);
    }

    /// <summary>Verifies enumerated application and protocol leaves may consume their documented inward surfaces.</summary>
    [Fact]
    public void Validate_WhenDocumentedLeavesConsumeFacadeOrProtocolOwner_AcceptsIt()
    {
        var graph = Graph(
            ("AgentKit.Evaluation", ["AgentKit"]),
            ("AgentKit.Mcp.Client", ["AgentKit.Mcp"]),
            ("AgentKit", []),
            ("AgentKit.Mcp", []));

        Should.NotThrow(graph.Validate);
    }

    /// <summary>Verifies duplicate and missing project identities are rejected before traversal.</summary>
    [Fact]
    public void Create_WhenGraphIdentityIsInvalid_RejectsIt()
    {
        _ = Should.Throw<ArgumentException>(() => Graph(("a", []), ("a", [])));
        _ = Should.Throw<ArgumentException>(() => Graph(("a", ["missing"])));
        _ = Should.Throw<ArgumentException>(() => Graph(("a", ["b", "b"]), ("b", [])));
    }

    /// <summary>Verifies graph construction copies caller-owned collections.</summary>
    [Fact]
    public void Create_WhenCallerMutatesInput_RetainsImmutableSnapshot()
    {
        var targets = new List<string> { "b" };
        var graph = Graph(("a", targets), ("b", []));

        targets.Clear();

        graph.Edges["a"].ShouldBe(["b"]);
    }

    /// <summary>Verifies repository location and evaluation configuration are validated at load entry.</summary>
    [Fact]
    public void Load_WhenRootOrConfigurationIsInvalid_RejectsIt()
    {
        _ = Should.Throw<ArgumentException>(() => ProjectReferenceGraph.Load(" ", "TestConfiguration"));
        _ = Should.Throw<ArgumentException>(() => ProjectReferenceGraph.Load(Path.GetTempPath(), "TestConfiguration"));
        _ = Should.Throw<ArgumentException>(() => ProjectReferenceGraph.Load(Directory.GetCurrentDirectory(), " "));
    }

    private static ProjectReferenceGraph Graph(params (string Source, IEnumerable<string> Targets)[] entries) =>
        ProjectReferenceGraph.Create(entries.Select(entry => KeyValuePair.Create(entry.Source, entry.Targets)));

}
