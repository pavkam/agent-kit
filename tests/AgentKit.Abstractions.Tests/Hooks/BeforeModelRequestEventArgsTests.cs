// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

/// <summary>Verifies <see cref="BeforeModelRequestEventArgs"/> argument checks, mutation limits, and isolation state.</summary>
public sealed class BeforeModelRequestEventArgsTests
{
    private static BeforeModelRequestEventArgs Create(LlmRequestSettings? settings = null) => new(
        HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.TurnCorrelation,
        DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), 2, HookPointEventArgsTestData.Request(settings));

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTurnIdentitiesAndTheOriginalSettings()
    {
        var args = Create();

        args.RunId.ShouldBe(HookPointEventArgsTestData.TurnCorrelation.RunId);
        args.TurnId.ShouldBe(HookPointEventArgsTestData.TurnCorrelation.TurnId!.Value);
        args.Turn.ShouldBe(2);
        args.OriginalSettings.ShouldBeSameAs(args.Request.Settings);
        args.Settings.ShouldBeSameAs(args.OriginalSettings);
        args.SettingsChanged.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenCorrelationNamesNoTurn_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new BeforeModelRequestEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), 1, HookPointEventArgsTestData.Request()))
            .ParamName.ShouldBe("correlation");

    [Fact]
    public void Constructor_WhenTurnIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new BeforeModelRequestEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.TurnCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), 0, HookPointEventArgsTestData.Request()))
            .ParamName.ShouldBe("turn");

    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new BeforeModelRequestEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.TurnCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), 1, null!))
            .ParamName.ShouldBe("request");

    [Fact]
    public void Settings_WhenAssignedNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create().Settings = null!);

    [Fact]
    public void Validate_WhenSettingsNarrowTheCap_Passes()
    {
        var args = Create(LlmRequestSettings.Default with { MaxOutputTokens = 100 });

        args.Settings = args.Settings with { MaxOutputTokens = 50, Temperature = 0.1 };

        Should.NotThrow(args.Validate);
        args.SettingsChanged.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenSettingsRaiseTheCap_ThrowsHookValidationException()
    {
        var args = Create(LlmRequestSettings.Default with { MaxOutputTokens = 100 });

        args.Settings = args.Settings with { MaxOutputTokens = 101 };

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void Validate_WhenSettingsRemoveTheCap_ThrowsHookValidationException()
    {
        var args = Create(LlmRequestSettings.Default with { MaxOutputTokens = 100 });

        args.Settings = args.Settings with { MaxOutputTokens = null };

        _ = Should.Throw<HookValidationException>(args.Validate);
    }

    [Fact]
    public void Validate_WhenTheOriginalHadNoCap_AllowsAnyCap()
    {
        var args = Create();

        args.Settings = args.Settings with { MaxOutputTokens = 5000 };

        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void RestoreMutableState_WhenGivenACapturedSnapshot_RestoresTheSettings()
    {
        var args = Create();
        var snapshot = args.CaptureMutableState();
        args.Settings = args.Settings with { Temperature = 0.9 };

        args.RestoreMutableState(snapshot);

        args.Settings.ShouldBeSameAs(args.OriginalSettings);
        args.SettingsChanged.ShouldBeFalse();
    }

    [Fact]
    public void RestoreMutableState_WhenGivenAForeignSnapshot_FallsBackToTheOriginal()
    {
        var args = Create();
        args.Settings = args.Settings with { Temperature = 0.9 };

        args.RestoreMutableState("not settings");

        args.Settings.ShouldBeSameAs(args.OriginalSettings);
    }
}
