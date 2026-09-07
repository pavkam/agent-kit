// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddLanguageTool_WhenCalledTwice_RegistersOneToolDescriptor()
    {
        var services = new ServiceCollection();

        _ = services.AddLanguageTool();
        _ = services.AddLanguageTool();

        services.Count(descriptor => descriptor.ServiceType == typeof(ITool)
            && descriptor.ImplementationType == typeof(LanguageTool)).ShouldBe(1);
    }

    [Fact]
    public void LanguageTool_WhenMaximumTextCharactersInvalid_ThrowsExactParameter()
    {
        var options = new LanguageToolOptions { MaximumTextCharacters = 0 };

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageTool(
            new RecordingLanguageService(),
            new RecordingSecurityAuthority(),
            new FixedSecurityRequestIdGenerator(),
            new FixedLanguageQueryIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options)));

        exception.ParamName.ShouldBe("MaximumTextCharacters");
    }
}
