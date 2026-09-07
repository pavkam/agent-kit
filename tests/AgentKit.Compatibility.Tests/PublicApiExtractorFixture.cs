// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public abstract class PublicApiExtractorFixture<T>
    where T : class?
{
    protected abstract T? Transform(T? value = default);
}
