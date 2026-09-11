// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolServiceRegistrationTests
{
    [Fact]
    public void RegisterProvider_WhenDescriptorInvalid_RejectsBeforeMutationOrActivation()
    {
        var services = new ServiceCollection();
        var source = new ToolSourceId("source");
        var valid = ServiceDescriptor.KeyedSingleton<IToolProvider, CallbackToolProvider>(source);
        Exact<ArgumentNullException>(() => ToolServiceRegistration.RegisterProvider(null!, source, valid, false), "services");
        Exact<ArgumentOutOfRangeException>(() => ToolServiceRegistration.RegisterProvider(services, default, valid, false), "sourceId");
        Exact<ArgumentNullException>(() => ToolServiceRegistration.RegisterProvider(services, source, null!, false), "descriptor");
        ServiceDescriptor[] invalid =
        [
            ServiceDescriptor.Singleton<IToolProvider>(_ => throw new InvalidOperationException("unkeyed")),
            ServiceDescriptor.KeyedSingleton<IToolProvider>("source", (_, _) => throw new InvalidOperationException("string-keyed")),
            ServiceDescriptor.KeyedSingleton<IToolProvider>(new ToolSourceId("other"), (_, _) => throw new InvalidOperationException("other")),
            ServiceDescriptor.KeyedScoped<IToolProvider, CallbackToolProvider>(source),
            ServiceDescriptor.KeyedTransient<IToolProvider, CallbackToolProvider>(source),
            ServiceDescriptor.KeyedSingleton<CallbackToolProvider, CallbackToolProvider>(source),
        ];
        foreach (var descriptor in invalid)
        {
            Exact<ArgumentException>(() => ToolServiceRegistration.RegisterProvider(services, source, descriptor, true), "descriptor");
        }
        services.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisterProvider_WhenMetadataRequiresActivation_RejectsBeforeMutation(bool replace)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ToolProviderRegistration>(_ => throw new InvalidOperationException("must not activate"));
        ServiceDescriptor[] before = [.. services];
        var source = new ToolSourceId("source");
        Exact<ArgumentException>(() => ToolServiceRegistration.RegisterProvider(services, source,
            ServiceDescriptor.KeyedSingleton<IToolProvider, CallbackToolProvider>(source), replace), "services");
        services.ShouldBe(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisterToolset_WhenMetadataRequiresActivation_RejectsBeforeMutation(bool replace)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ToolsetRegistration>(_ => throw new InvalidOperationException("must not activate"));
        ServiceDescriptor[] before = [.. services];
        Exact<ArgumentException>(() => ToolServiceRegistration.RegisterToolset(services, ToolCatalogMergeTestData.Toolset("tools", [], []), replace), "services");
        services.ShouldBe(before);
    }

    [Fact]
    public void RegisterToolset_WhenRequiredInputNull_RejectsBeforeMutation()
    {
        var services = new ServiceCollection();
        Exact<ArgumentNullException>(() => ToolServiceRegistration.RegisterToolset(null!, ToolCatalogMergeTestData.Toolset("tools", [], []), false), "services");
        Exact<ArgumentNullException>(() => ToolServiceRegistration.RegisterToolset(services, null!, true), "publication");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void CreateRegistrationCatalog_WhenProviderNull_RejectsExactParameter() =>
        Exact<ArgumentNullException>(() => ToolServiceRegistration.CreateRegistrationCatalog(null!), "provider");

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

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
