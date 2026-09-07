// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;

public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfDefault_WhenArrayIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<int> array = default;

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefault(array));

        exception.ParamName.ShouldBe("array");
    }

    [Fact]
    public void ThrowIfDefault_WhenArrayIsEmpty_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfDefault(ImmutableArray<int>.Empty));

    [Fact]
    public void ThrowIfDefault_WhenArrayIsPopulated_DoesNotThrow()
    {
        ImmutableArray<int> array = [1, 2, 3];

        Should.NotThrow(() => ArgumentException.ThrowIfDefault(array));
    }

    [Fact]
    public void ThrowIfDefault_WhenParamNameSuppliedExplicitly_UsesSuppliedName()
    {
        ImmutableArray<int> array = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDefault(array, "customParam"));

        exception.ParamName.ShouldBe("customParam");
    }

    [Fact]
    public void ThrowIfDefault_WhenUsedByExtensionValue_CoversProductionCallSite()
    {
        // AgentKit.Extensibility.ExtensionValue is a production call site for
        // this guard; this proves it is actually wired up end to end rather
        // than only exercised directly.
        var exception = Should.Throw<ArgumentException>(() => new ExtensionValue(default));

        exception.ParamName.ShouldBe("canonicalJson");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsDefault_ThrowsWithInferredParamName()
    {
        ImmutableArray<int> values = default;

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefaultOrEmpty(values));

        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDefaultOrEmpty(ImmutableArray<int>.Empty, "items"));

        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenArrayIsPopulated_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfDefaultOrEmpty(ImmutableArray.Create(1)));

    [Fact]
    public void ThrowIfDefaultOrEmpty_WhenUsedBySecurityRequest_CoversProductionCallSite()
    {
        var grant = SecurityTestData.Grant();

        var exception = Should.Throw<ArgumentException>(() => new SecurityRequest(
            grant.RequestId,
            grant.Scope,
            null,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            [],
            grant.InputFingerprint,
            grant.ExpiresAt));

        exception.ParamName.ShouldBe("resources");
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsAbsolute_DoesNotThrow()
    {
        var uri = new Uri("https://api.example.test/v1/");

        Should.NotThrow(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsRelative_ThrowsArgumentExceptionWithInferredParamName()
    {
        var uri = new Uri("v1/chat/completions", UriKind.Relative);

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));

        exception.ParamName.ShouldBe("uri");
    }

    [Fact]
    public void ThrowIfNotAbsoluteUri_WhenUriIsNull_ThrowsArgumentNullException()
    {
        Uri uri = null!;

        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotAbsoluteUri(uri));

        exception.ParamName.ShouldBe("uri");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://evil.example.test/chat")]
    [InlineData("/rooted/chat")]
    [InlineData("//evil.example.test/chat")]
    [InlineData("\\evil.example.test\\chat")]
    public void ThrowIfNotRelativeUriPath_WhenPathCanEscapeBaseAddress_ThrowsArgumentException(string path)
    {
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNotRelativeUriPath(path));

        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void ThrowIfNotRelativeUriPath_WhenPathIsNull_ThrowsArgumentNullException()
    {
        string path = null!;

        var exception = Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIfNotRelativeUriPath(path));

        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void ThrowIfNotRelativeUriPath_WhenPathIsRelative_DoesNotThrow()
    {
        const string path = "v1/chat/completions";

        Should.NotThrow(() => ArgumentException.ThrowIfNotRelativeUriPath(path));
    }

    [Fact]
    public void ThrowIfContainsNull_WhenArrayContainsNull_ThrowsWithInferredParameter()
    {
        ImmutableArray<string> values = ["one", null!];

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfContainsNull(values));

        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void ThrowIfContainsNull_WhenArrayContainsOnlyValues_DoesNotThrow()
    {
        ImmutableArray<string> values = ["one", "two"];

        Should.NotThrow(() => ArgumentException.ThrowIfContainsNull(values));
    }

    [Fact]
    public void ThrowIfContainsNul_WhenValueContainsNul_ThrowsWithInferredParameter()
    {
        const string value = "before\0after";

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfContainsNul(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ThrowIfContainsNul_WhenValueContainsNoNul_DoesNotThrow() =>
        Should.NotThrow(() => ArgumentException.ThrowIfContainsNul("safe"));

    [Fact]
    public void ThrowIfContainsNul_WhenUsedByProcessEnvironment_CoversProductionCallSite()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessEnvironmentVariable("NAME", "bad\0value"));

        exception.ParamName.ShouldBe("value");
    }
}
