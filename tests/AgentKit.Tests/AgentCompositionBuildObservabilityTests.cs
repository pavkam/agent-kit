// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Diagnostics;

using AgentKit.Observability;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class AgentCompositionBuildObservabilityTests
{
    [Fact]
    public void Start_WhenTimeProviderIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionBuildObservability.Start(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void CompleteBuilt_WhenScopeIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionBuildObservability.CompleteBuilt(
                null!, TimeProvider.System, timestamp: null, NullLogger.Instance));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void CompleteBuilt_WhenTimeProviderIsNull_ThrowsExactArgumentNullException()
    {
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild, ActivityKind.Internal);

        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionBuildObservability.CompleteBuilt(
                scope, null!, timestamp: null, NullLogger.Instance));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void CompleteBuilt_WhenLoggerIsNull_ThrowsExactArgumentNullException()
    {
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild, ActivityKind.Internal);

        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionBuildObservability.CompleteBuilt(
                scope, TimeProvider.System, timestamp: null, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("logger");
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("timeProvider")]
    [InlineData("logger")]
    public void CompleteRejected_WhenArgumentIsNull_ThrowsExactArgumentNullException(string parameter)
    {
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild, ActivityKind.Internal);
        var exception = Should.Throw<ArgumentNullException>(parameter switch
        {
            "scope" => () => AgentCompositionBuildObservability.CompleteRejected(
                null!, TimeProvider.System, timestamp: null, NullLogger.Instance),
            "timeProvider" => () => AgentCompositionBuildObservability.CompleteRejected(
                scope, null!, timestamp: null, NullLogger.Instance),
            _ => () => AgentCompositionBuildObservability.CompleteRejected(
                scope, TimeProvider.System, timestamp: null, null!),
        });

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("timeProvider")]
    [InlineData("logger")]
    public void CompleteFailed_WhenBoundaryArgumentIsNull_ThrowsExactArgumentNullException(string parameter)
    {
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild, ActivityKind.Internal);
        var failure = new InvalidOperationException();
        var exception = Should.Throw<ArgumentNullException>(parameter switch
        {
            "scope" => () => AgentCompositionBuildObservability.CompleteFailed(
                null!, TimeProvider.System, timestamp: null, NullLogger.Instance, failure),
            "timeProvider" => () => AgentCompositionBuildObservability.CompleteFailed(
                scope, null!, timestamp: null, NullLogger.Instance, failure),
            _ => () => AgentCompositionBuildObservability.CompleteFailed(
                scope, TimeProvider.System, timestamp: null, null!, failure),
        });

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void CompleteFailed_WhenExceptionIsNull_ThrowsExactArgumentNullException()
    {
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.AgentCompositionBuild, ActivityKind.Internal);

        var exception = Should.Throw<ArgumentNullException>(() =>
            AgentCompositionBuildObservability.CompleteFailed(
                scope, TimeProvider.System, timestamp: null, NullLogger.Instance, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("exception");
    }
}
