// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit;

/// <summary>Verifies ContextSourceReference behavior and contracts.</summary>
public sealed class ContextSourceReferenceTests
{
    [Fact]
    public void ContextSourceReference_Constructor_WhenNamespaceDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContextSourceReference(default, Key(), Version()));
        exception.ParamName.ShouldBe("sourceNamespace");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContextSourceReference(Namespace(), default, Version()));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContextSourceReference(Namespace(), Key(), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenValid_RetainsExactFieldsEqualityHashAndCopy()
    {
        var reference = new ContextSourceReference(Namespace(), Key(), Version());
        var same = new ContextSourceReference(Namespace(), Key(), Version());
        var copy = reference with
        {
        };
        reference.Namespace.ShouldBe(Namespace());
        reference.Key.ShouldBe(Key());
        reference.Version.ShouldBe(Version());
        reference.ShouldBe(same);
        reference.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(reference);
        copy.ShouldNotBeSameAs(reference);
    }

    [Fact]
    public void ContextSourceReference_Equality_WhenAnyFieldDiffers_IsNotEqual()
    {
        var reference = new ContextSourceReference(Namespace(), Key(), Version());
        reference.ShouldNotBe(new ContextSourceReference(new("other"), Key(), Version()));
        reference.ShouldNotBe(new ContextSourceReference(Namespace(), new("other"), Version()));
        reference.ShouldNotBe(new ContextSourceReference(Namespace(), Key(), new("other")));
    }

    private static ContextSourceNamespace Namespace() => new("agentkit.context");
    private static ContextSourceKey Key() => new("instructions");
    private static ContextSourceVersion Version() => new("1.0");
}
