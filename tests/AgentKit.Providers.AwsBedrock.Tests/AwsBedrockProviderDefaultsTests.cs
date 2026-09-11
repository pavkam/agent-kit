// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;



/// <summary>Verifies AwsBedrockProviderDefaults behavior and contracts.</summary>
public sealed class AwsBedrockProviderDefaultsTests
{
    private static AwsBedrockProviderOptions CreateOptions() => new()
    {
        Region = "us-east-1"
    };
    [Fact]
    public void BuildConverseUri_WhenSimpleModelId_UsesRegionalHostAndModelPath()
    {
        var options = CreateOptions();
        var uri = AwsBedrockProviderDefaults.BuildConverseUri(options, "anthropic.claude-3-haiku-20240307-v1:0");
        uri.ShouldBe(new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-haiku-20240307-v1%3A0/converse"));
    }

    [Fact]
    public void BuildConverseStreamUri_WhenSimpleModelId_UsesConverseStreamPath()
    {
        var options = CreateOptions();
        var uri = AwsBedrockProviderDefaults.BuildConverseStreamUri(options, "anthropic.claude-3-haiku-20240307-v1:0");
        uri.ShouldBe(new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-haiku-20240307-v1%3A0/converse-stream"));
    }

    [Fact]
    public void BuildConverseUri_WhenModelIdIsAnArn_PreservesSlashesLiterally()
    {
        var options = CreateOptions();
        const string arn = "arn:aws:bedrock:us-east-1:123456789012:inference-profile/us.anthropic.claude-3-5-sonnet-20240620-v1:0";
        var uri = AwsBedrockProviderDefaults.BuildConverseUri(options, arn);
        uri.ShouldBe(new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/" + "arn%3Aaws%3Abedrock%3Aus-east-1%3A123456789012%3Ainference-profile/us.anthropic.claude-3-5-sonnet-20240620-v1%3A0/converse"));
    }

    [Fact]
    public void BuildConverseUri_WhenDifferentRegion_UsesThatRegionalHost()
    {
        var options = new AwsBedrockProviderOptions
        {
            Region = "eu-west-1"
        };
        var uri = AwsBedrockProviderDefaults.BuildConverseUri(options, "meta.llama3-70b-instruct-v1:0");
        uri.Authority.ShouldBe("bedrock-runtime.eu-west-1.amazonaws.com");
    }

    [Fact]
    public void BuildBaseAddress_WhenRegionIsNullOrWhitespace_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => AwsBedrockProviderDefaults.BuildBaseAddress(null));
        _ = Should.Throw<ArgumentException>(() => AwsBedrockProviderDefaults.BuildBaseAddress("   "));
    }

    [Fact]
    public void BuildConverseUri_WhenModelIdIsNullOrWhitespace_ThrowsArgumentException()
    {
        var options = CreateOptions();
        _ = Should.Throw<ArgumentException>(() => AwsBedrockProviderDefaults.BuildConverseUri(options, ""));
    }

    [Fact]
    public void ApiFamily_IsStableConverseIdentity() => AwsBedrockProviderDefaults.ApiFamily.ShouldBe(new ApiFamilyId("aws-bedrock-converse"));
    [Fact]
    public void DefaultCapabilities_SupportsToolCallsButNotReasoningOrVision()
    {
        var capabilities = AwsBedrockProviderDefaults.DefaultCapabilities;
        capabilities.SupportsToolCalls.ShouldBeTrue();
        capabilities.SupportsParallelToolCalls.ShouldBeTrue();
        capabilities.SupportsStreaming.ShouldBeTrue();
        capabilities.SupportsReasoning.ShouldBeFalse();
        capabilities.SupportsVisionInput.ShouldBeFalse();
        capabilities.SupportsStructuredOutput.ShouldBeFalse();
    }
}
