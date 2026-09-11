// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolServiceRegistrationTests
{
    [Fact]
    public void RegisterStaticProvider_WhenRequiredInputNull_RejectsBeforeMutation()
    {
        var services = new ServiceCollection();
        var bindings = new ToolProviderBindings(ToolCaptureTestData.Snapshot([]), []);
        var missingServices = Should.Throw<ArgumentNullException>(() => ToolServiceRegistration.RegisterStaticProvider(null!, bindings, false));
        missingServices.GetType().ShouldBe(typeof(ArgumentNullException));
        missingServices.ParamName.ShouldBe("services");
        var missingBindings = Should.Throw<ArgumentNullException>(() => ToolServiceRegistration.RegisterStaticProvider(services, null!, true));
        missingBindings.GetType().ShouldBe(typeof(ArgumentNullException));
        missingBindings.ParamName.ShouldBe("bindings");
        services.ShouldBeEmpty();
    }
}
