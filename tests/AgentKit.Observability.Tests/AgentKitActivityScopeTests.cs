// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

[Collection(ActivityScopeObservationGroup.Name)]
public sealed class AgentKitActivityScopeTests
{
    [Fact]
    public void Start_WhenNameIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => AgentKitActivityScope.Start(null!, ActivityKind.Internal));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_WhenNameIsBlank_ThrowsExactArgumentException(string name)
    {
        var exception = Should.Throw<ArgumentException>(() => AgentKitActivityScope.Start(name, ActivityKind.Internal));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("name");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Start_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException(int kind)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            AgentKitActivityScope.Start("test.scope", (ActivityKind) kind));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Start_WhenSampled_UsesRequestedNameKindAndAmbientParent()
    {
        using var parent = new Activity("parent").Start();
        Activity? stopped = null;
        using var listener = Listener(
            sample: SampleAll,
            stopped: activity => stopped = activity);
        ActivitySource.AddActivityListener(listener);

        using (var scope = AgentKitActivityScope.Start("test.scope", ActivityKind.Consumer))
        {
            scope.Activity.ShouldNotBeNull().OperationName.ShouldBe("test.scope");
            scope.Activity.Kind.ShouldBe(ActivityKind.Consumer);
            scope.Activity.ParentSpanId.ShouldBe(parent.SpanId);
            Activity.Current.ShouldBeSameAs(scope.Activity);
        }

        Activity.Current.ShouldBeSameAs(parent);
        stopped.ShouldNotBeNull().OperationName.ShouldBe("test.scope");
    }

    [Fact]
    public void Start_WhenSampleCallbackThrows_ReturnsInertScopeAndRestoresParent()
    {
        using var parent = new Activity("parent").Start();
        using var listener = Listener(sample: ThrowingSample);
        ActivitySource.AddActivityListener(listener);

        using var scope = AgentKitActivityScope.Start("test.sample-failure", ActivityKind.Internal);

        scope.Activity.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Start_WhenStartedCallbackThrows_ReturnsInertScopeAndRestoresParent()
    {
        using var parent = new Activity("parent").Start();
        using var listener = Listener(sample: SampleAll, started: static _ => throw new InvalidOperationException("observer"));
        ActivitySource.AddActivityListener(listener);

        using var scope = AgentKitActivityScope.Start("test.start-failure", ActivityKind.Internal);

        scope.Activity.ShouldBeNull();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Dispose_WhenStoppedCallbackThrows_ContainsFailureAndRestoresParent()
    {
        using var parent = new Activity("parent").Start();
        using var listener = Listener(sample: SampleAll, stopped: static _ => throw new InvalidOperationException("observer"));
        ActivitySource.AddActivityListener(listener);
        var scope = AgentKitActivityScope.Start("test.stop-failure", ActivityKind.Internal);

        Should.NotThrow(scope.Dispose);

        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Start_WhenAmbientCurrentChangedCallbackThrows_ReturnsInertScopeAndRestoresParent()
    {
        using var parent = new Activity("parent").Start();
        using var listener = Listener(sample: SampleAll);
        ActivitySource.AddActivityListener(listener);
        Activity.CurrentChanged += ThrowOnCurrentChanged;
        AgentKitActivityScope? scope = null;
        try
        {
            scope = AgentKitActivityScope.Start("test.current-failure", ActivityKind.Internal);

            scope.Activity.ShouldBeNull();
            Activity.Current.ShouldBeSameAs(parent);
        }
        finally
        {
            Activity.CurrentChanged -= ThrowOnCurrentChanged;
            scope?.Dispose();
        }

        static void ThrowOnCurrentChanged(object? sender, ActivityChangedEventArgs args) =>
            throw new InvalidOperationException("observer");
    }

    [Fact]
    public void Dispose_WhenNestedScopesDisposedLifoAndRepeated_RestoresEachParentOnce()
    {
        using var listener = Listener(sample: SampleAll);
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("parent").Start();
        var outer = AgentKitActivityScope.Start("test.outer", ActivityKind.Internal);
        var inner = AgentKitActivityScope.Start("test.inner", ActivityKind.Internal);
        Activity.Current.ShouldBeSameAs(inner.Activity);

        inner.Dispose();
        inner.Dispose();

        Activity.Current.ShouldBeSameAs(outer.Activity);
        outer.Dispose();
        outer.Dispose();
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static ActivityListener Listener(
        SampleActivity<ActivityContext>? sample = null,
        Action<Activity>? started = null,
        Action<Activity>? stopped = null) =>
        new()
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = sample,
            ActivityStarted = started,
            ActivityStopped = stopped,
        };

    private static ActivitySamplingResult SampleAll(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private static ActivitySamplingResult ThrowingSample(ref ActivityCreationOptions<ActivityContext> _) =>
        throw new InvalidOperationException("observer");
}
