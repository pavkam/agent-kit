// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPreprocessingManifest behavior and contracts.</summary>
public sealed class InputPreprocessingManifestTests
{
    public static IEnumerable<object?[]> InvalidConstructorCases()
    {
        yield return new object?[]
        {
            () => new InputPreprocessingManifest(default, new InputFingerprint("original"), new InputFingerprint("effective")),
            typeof(ArgumentOutOfRangeException),
            "configurationVersion"
        };
        yield return new object?[]
        {
            () => new InputPreprocessingManifest(new ConfigurationVersion(1), default, new InputFingerprint("effective")),
            typeof(ArgumentOutOfRangeException),
            "originalFingerprint"
        };
        yield return new object?[]
        {
            () => new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("original"), default),
            typeof(ArgumentOutOfRangeException),
            "effectiveFingerprint"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidConstructorCases))]
    public void Constructor_WhenDocumentedArgumentIsInvalid_ThrowsExactException(Func<InputPreprocessingManifest> construct, Type exceptionType, string parameterName)
    {
        var exception = Should.Throw<Exception>(() => _ = construct());
        exception.GetType().ShouldBe(exceptionType);
        ((ArgumentException) exception).ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("original"), new InputFingerprint("effective"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
