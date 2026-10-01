// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

using AgentKit.Tests;

/// <summary>Builds one real <see cref="AgentEngine"/> with a scripted loop and the evaluation runner bound to it.</summary>
internal sealed class EvaluationHarness: IAsyncDisposable
{
    private EvaluationHarness(AgentEngine engine, GatedAgentLoop loop, InMemoryTestSessionCoordinator sessions, FakeTimeProvider time)
    {
        Engine = engine;
        Loop = loop;
        Sessions = sessions;
        Time = time;
    }

    /// <summary>Gets the engine every case runs through.</summary>
    public AgentEngine Engine { get; }

    /// <summary>Gets the scripted loop that stands in for the model-driven agent loop.</summary>
    public GatedAgentLoop Loop { get; }

    /// <summary>Gets the session coordinator double that records created sessions.</summary>
    public InMemoryTestSessionCoordinator Sessions { get; }

    /// <summary>Gets the clock shared by the engine and the runner.</summary>
    public FakeTimeProvider Time { get; }

    /// <summary>Gets the runner bound to <see cref="Engine"/>.</summary>
    public IEvaluationRunner Runner => Engine.Services.GetRequiredService<IEvaluationRunner>();

    /// <summary>Creates the harness.</summary>
    /// <param name="configure">Registers evaluators, stores, exporters, or options before the engine is built.</param>
    /// <param name="loop">The scripted loop, or <see langword="null"/> for a default one.</param>
    /// <param name="definitions">The agent definitions the engine hosts; the default agent when none is given.</param>
    /// <returns>The built harness.</returns>
    public static async Task<EvaluationHarness> CreateAsync(
        Action<IServiceCollection>? configure = null,
        GatedAgentLoop? loop = null,
        params AgentDefinition[] definitions)
    {
        var scripted = loop ?? new GatedAgentLoop();
        var sessions = new InMemoryTestSessionCoordinator();
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddDays(1));
        var builder = CompositionTestData.SendableBuilder(scripted, sessions, SessionBusyBehavior.Reject, null, definitions);
        CompositionTestData.UseFirstPartyIo(builder.Services);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));
        _ = builder.Services.AddAgentEvaluation();
        configure?.Invoke(builder.Services);
        var engine = builder.Build();
        _ = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        return new EvaluationHarness(engine, scripted, sessions, time);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Engine.DisposeAsync();
}
