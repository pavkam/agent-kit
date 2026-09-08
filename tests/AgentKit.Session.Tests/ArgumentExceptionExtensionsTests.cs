// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfNullSessionStoreDescriptor_WhenDescriptorIsNull_ThrowsArgumentExceptionWithInferredParameterName()
    {
        SessionStoreDescriptor? descriptor = null;

        var exception = Should.Throw<ArgumentException>(() =>
            ArgumentException.ThrowIfNullSessionStoreDescriptor(descriptor));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe(nameof(descriptor));
    }

    [Fact]
    public void ThrowIfNullSessionStoreDescriptor_WhenDescriptorIsPresent_DoesNotThrow()
    {
        var descriptor = new SessionStoreDescriptor(
            new SessionStoreKey("store"),
            SessionStoreCapabilities.None,
            SessionConsistencyModel.Strong,
            durable: false,
            supportsDistributedFencing: false);

        Should.NotThrow(() => ArgumentException.ThrowIfNullSessionStoreDescriptor(descriptor));
    }
}
