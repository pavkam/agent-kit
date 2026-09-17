// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;



/// <summary>Verifies ActivityObservation behavior and contracts.</summary>
public sealed class ActivityObservationTests
{
    [Fact]
    public void ActivityObservation_WhenOperationNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityObservation(null!, ActivityStatusCode.Ok, FrozenDictionary<string, object?>.Empty));
        exception.ParamName.ShouldBe("operationName");
    }

    [Fact]
    public void ActivityObservation_WhenOperationNameIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ActivityObservation(" ", ActivityStatusCode.Ok, FrozenDictionary<string, object?>.Empty));
        exception.ParamName.ShouldBe("operationName");
    }

    [Fact]
    public void ActivityObservation_WhenStatusIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ActivityObservation("test.operation", (ActivityStatusCode) int.MaxValue, FrozenDictionary<string, object?>.Empty));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void ActivityObservation_WhenTagsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ActivityObservation("test.operation", ActivityStatusCode.Ok, null!));
        exception.ParamName.ShouldBe("tags");
    }

    [Fact]
    public void GetTagItem_WhenKeyIsBlank_ThrowsArgumentException()
    {
        var observation = new ActivityObservation("test.operation", ActivityStatusCode.Ok, FrozenDictionary<string, object?>.Empty);
        var exception = Should.Throw<ArgumentException>(() => observation.GetTagItem(" "));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ActivityObservation_WhenValuesMatch_AreStructurallyEqualAndReturnTheCapturedTag()
    {
        var tags = new Dictionary<string, object?> { ["tag.key"] = "tag.value" }.ToFrozenDictionary();
        var first = new ActivityObservation("test.operation", ActivityStatusCode.Ok, tags);
        var second = new ActivityObservation("test.operation", ActivityStatusCode.Ok, tags);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.GetTagItem("tag.key").ShouldBe("tag.value");
    }

    [Fact]
    public void With_WhenCopyingWithoutChanges_RetainsTheOriginalValues()
    {
        var tags = new Dictionary<string, object?> { ["tag.key"] = "tag.value" }.ToFrozenDictionary();
        var original = new ActivityObservation("test.operation", ActivityStatusCode.Ok, tags);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }
}
