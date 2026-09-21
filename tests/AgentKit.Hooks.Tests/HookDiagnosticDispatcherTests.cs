// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class HookDiagnosticDispatcherTests
{
    [Fact]
    public async Task PublishAsync_WhenSinkThrows_IsolatesFaultWithoutThrowing()
    {
        var diagnostic = CreateDiagnostic();
        var dispatcher = new HookDiagnosticDispatcher([new FaultingSink(), new RecordingSink(diagnostic)]);

        await dispatcher.PublishAsync(diagnostic, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WhenDiagnosticPublished_CarriesNoProtectedContent()
    {
        var recording = new RecordingSink(null);
        var dispatcher = new HookDiagnosticDispatcher([recording]);
        var diagnostic = CreateDiagnostic();

        await dispatcher.PublishAsync(diagnostic, TestContext.Current.CancellationToken);

        var published = recording.Last.ShouldNotBeNull();
        published.Invocation.RegistrationId.ShouldBe(diagnostic.Invocation.RegistrationId);
        published.Outcome.ShouldBe(HookInvocationOutcome.Succeeded);
        published.ToString().ShouldNotContain("secret");
    }

    private static HookInvocationDiagnostic CreateDiagnostic()
    {
        var invocation = new HookInvocationContext(
            HookRegistrationIds.FromAuthorHookId(new HookId("observer")),
            new HookInvocationId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            new HookDispatchId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            depth: 1);
        return new HookInvocationDiagnostic(
            AgentHookPoints.RunStarted,
            invocation,
            HookInvocationOutcome.Succeeded,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(5));
    }

    private sealed class FaultingSink: IHookDiagnosticSink
    {
        public ValueTask PublishAsync(HookInvocationDiagnostic diagnostic, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sink fault");
    }

    private sealed class RecordingSink(HookInvocationDiagnostic? expected): IHookDiagnosticSink
    {
        public HookInvocationDiagnostic? Last { get; private set; }

        public ValueTask PublishAsync(HookInvocationDiagnostic diagnostic, CancellationToken cancellationToken)
        {
            Last = diagnostic;
            if (expected is not null)
            {
                diagnostic.Invocation.RegistrationId.ShouldBe(expected.Invocation.RegistrationId);
            }

            return ValueTask.CompletedTask;
        }
    }
}
