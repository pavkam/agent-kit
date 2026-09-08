// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

public sealed class DefaultOutputProcessorTests
{
    [Fact]
    public void Constructor_WhenValidatorsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultOutputProcessor(null!, new StructuralOutputSchemaEngine(), DefaultOptions()));

        exception.ParamName.ShouldBe("validators");
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultOutputProcessor([], new StructuralOutputSchemaEngine(), null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenDuplicateValidatorNames_ThrowsArgumentException()
    {
        var validators = new IOutputValidator[]
        {
            new FakeOutputValidator("dup", static _ => OutputValidationPassed.Instance),
            new FakeOutputValidator("dup", static _ => OutputValidationPassed.Instance),
        };

        _ = Should.Throw<ArgumentException>(() => new DefaultOutputProcessor(validators, new StructuralOutputSchemaEngine(), DefaultOptions()));
    }

    [Fact]
    public async Task ProcessAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var processor = CreateProcessor();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => processor.ProcessAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Theory]
    [InlineData(OutputMode.SyntheticTool)]
    [InlineData(OutputMode.Media)]
    [InlineData(OutputMode.Union)]
    public async Task ProcessAsync_WhenModeIsUnsupported_ReturnsRejectedWithUnsupportedMode(OutputMode mode)
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(mode);
        var request = TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("anything"));

        var result = await processor.ProcessAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.UnsupportedMode);
    }

    [Fact]
    public async Task ProcessAsync_TextMode_ReturnsAcceptedWithConcatenatedText()
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(OutputMode.Text);
        var response = TestFactory.Response(
            [
                new TextPart("hello ", TextSemantics.Plain, ExtensionData.Empty),
                new TextPart("world", TextSemantics.Plain, ExtensionData.Empty),
            ]);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<OutputAccepted>();
        accepted.Output.Mode.ShouldBe(OutputMode.Text);
        accepted.Output.Text.ShouldBe("hello world");
    }

    [Fact]
    public async Task ProcessAsync_WhenTextModeDeclaresSchema_ReturnsConfigurationRejectedBeforeExtraction()
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(OutputMode.Text, schema: TestFactory.Schema("false"));
        var request = TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("plain text"));

        var result = await processor.ProcessAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputConfigurationRejected>();
        rejected.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.MalformedSchema);
        rejected.Failure.SafeMessage.ShouldBe("A JSON schema cannot be applied to plain-text output.");
    }

    [Fact]
    public async Task ProcessAsync_NativeSchemaMode_WhenSchemaMissingAndRequired_ReturnsConfigurationRejected()
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("{}")), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputConfigurationRejected>();
        rejected.Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.MalformedSchema);
    }

    [Fact]
    public async Task ProcessAsync_NativeSchemaMode_WhenSchemaMissingButNotRequired_FallsBackToTextParsing()
    {
        var processor = CreateProcessor(options => options.RequireSchemaForStructuredModes = false);
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null);
        var response = TestFactory.TextResponse(/*lang=json,strict*/"""{"ok":true}""");

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<OutputAccepted>();
        _ = accepted.Output.Json.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaIsOptional_AppliesConfiguredCandidateDepth()
    {
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateDepth = 2;
        });
        var definition = TestFactory.Definition(OutputMode.Prompted, schema: null, retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse(/*lang=json,strict*/"""{"a":{"b":1}}""")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaIsOptional_AppliesCandidateNodeLimit()
    {
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateNodes = 2;
        });
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"a":1,"b":2}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_WhenWideStructuredCandidateExceedsNodeLimit_RejectsBeforeTraversingRemainingChildren(
        bool isObject)
    {
        var validator = new FakeOutputValidator("semantic", static _ => OutputValidationPassed.Instance);
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateNodes = 1;
        }, [validator]);
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema,
            schema: null,
            validators: [new OutputValidatorReference("semantic")],
            retryPolicy: OutputRetryPolicy.None);
        var members = string.Join(',', Enumerable.Range(0, 8_192).Select(static index => $"\"property-{index}\":0"));
        var json = isObject ? $"{{{members}}}" : $"[{string.Join(',', Enumerable.Repeat("0", 8_192))}]";
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(json));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
        validator.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhenEscapedStructuredStringSerializesWithinByteLimit_ReturnsAccepted()
    {
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateBytes = 3;
        });
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson("\"\\u0061\""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputAccepted>().Output.Json!.Value.GetString().ShouldBe("a");
    }

    [Fact]
    public async Task ProcessAsync_WhenRuntimeCandidateContainsLargeRetainedWhitespace_DeserializesFromBoundedCanonicalBytes()
    {
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateBytes = 16;
        });
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema,
            schema: null,
            runtimeType: typeof(WhitespaceRuntimeValue));
        var response = TestFactory.StructuredResponse(
            TestFactory.ParseJson($"{{{new string(' ', 1_000_000)}\"value\":1}}"));
        var request = TestFactory.ProcessingRequest(definition, response);

        _ = await processor.ProcessAsync(request, TestContext.Current.CancellationToken);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var operation = processor.ProcessAsync(request, TestContext.Current.CancellationToken);
        operation.IsCompletedSuccessfully.ShouldBeTrue();
        var result = await operation;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var accepted = result.ShouldBeOfType<OutputAccepted>();
        accepted.Output.Value.ShouldBeOfType<WhitespaceRuntimeValue>().Value.ShouldBe(1);
        allocatedBytes.ShouldBeLessThan(8_192L);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("property-name")]
    public async Task ProcessAsync_WhenMaterializedTokenExceedsByteLimit_BoundsCurrentThreadAllocation(string tokenKind)
    {
        const int maximumCandidateBytes = 8;
        var processor = CreateProcessor(options =>
        {
            options.RequireSchemaForStructuredModes = false;
            options.MaximumCandidateBytes = maximumCandidateBytes;
            options.MaximumCandidateNodes = 2;
        });
        var token = new string(tokenKind == "number" ? '1' : 'a', 1_000_000);
        var json = tokenKind switch
        {
            "string" => $"\"{token}\"",
            "number" => token,
            "property-name" => $"{{\"{token}\":0}}",
            _ => throw new UnreachableException($"Unknown token kind '{tokenKind}'."),
        };
        var request = TestFactory.ProcessingRequest(
            TestFactory.Definition(OutputMode.NativeSchema, schema: null, retryPolicy: OutputRetryPolicy.None),
            TestFactory.StructuredResponse(TestFactory.ParseJson(json)));

        var warmup = processor.ProcessAsync(request, TestContext.Current.CancellationToken);
        warmup.IsCompletedSuccessfully.ShouldBeTrue();
        _ = (await warmup).ShouldBeOfType<OutputRejected>();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var operation = processor.ProcessAsync(request, TestContext.Current.CancellationToken);
        operation.IsCompletedSuccessfully.ShouldBeTrue();
        var result = await operation;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
        allocatedBytes.ShouldBeLessThan(8_192L);
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaIsOptional_RejectsDuplicateCandidateMembers()
    {
        var processor = CreateProcessor(options => options.RequireSchemaForStructuredModes = false);
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: null, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"same":1,"same":2}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.MalformedJson);
    }

    [Fact]
    public async Task ProcessAsync_NativeSchemaMode_WhenStructuredDataPartPresent_ValidatesAndAccepts()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: schema);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<OutputAccepted>();
        accepted.Output.Mode.ShouldBe(OutputMode.NativeSchema);
        _ = accepted.Output.Json.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenTextIsNotValidJsonForStructuredMode_ReturnsMalformedJsonFailure()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}""");
        var definition = TestFactory.Definition(OutputMode.Prompted, schema: schema, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.TextResponse("not json at all {{{");

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.MalformedJson);
    }

    [Fact]
    public async Task ProcessAsync_WhenNoContentAvailableForStructuredMode_ReturnsMalformedJsonFailure()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}""");
        var definition = TestFactory.Definition(OutputMode.Prompted, schema: schema, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.Response([]);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.MalformedJson);
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaValidationFails_ReturnsSchemaValidationFailedFailure()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: schema, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"other":1}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.SchemaValidationFailed);
        rejected.Failure.Issues.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaPreflightFails_ReturnsConfigurationRejectedBeforeSemanticValidation()
    {
        var validator = new FakeOutputValidator("semantic", static _ => OutputValidationPassed.Instance);
        var processor = CreateProcessor(validators: [validator]);
        var definition = TestFactory.Definition(
            OutputMode.Prompted,
            schema: TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"unsupported"}"""),
            validators: [new OutputValidatorReference("semantic")],
            retryPolicy: new OutputRetryPolicy(2));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("not json")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputConfigurationRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.UnsupportedVocabulary);
        validator.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhenUnsupportedModeHasInvalidAlternativeSchema_ReturnsConfigurationRejectedFirst()
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(OutputMode.Union) with
        {
            Alternatives =
            [
                new OutputAlternative(
                    "invalid",
                    TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"unsupported"}""")),
            ],
        };

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("anything")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputConfigurationRejected>().Failure.Kind.ShouldBe(OutputSchemaConfigurationFailureKind.UnsupportedVocabulary);
    }

    [Fact]
    public async Task ProcessAsync_WhenSchemaFailureExceedsDefinitionLimit_BoundsResultAndRepair()
    {
        var processor = CreateProcessor(options => options.MaximumValidationIssues = 5);
        var schema = TestFactory.Schema(
            /*lang=json,strict*/"""{"type":"object","required":["first","second","third"]}""");
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema,
            schema: schema,
            validationPolicy: new OutputValidationPolicy(OutputValidationFailureMode.CollectAllFailures, 1),
            retryPolicy: new OutputRetryPolicy(1));
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var retry = result.ShouldBeOfType<OutputRetryRequired>();
        retry.Failure.Issues.Length.ShouldBe(1);
        retry.Failure.Issues[0].Code.ShouldBe("required-property-missing");
        retry.Repair.SafeMessage.ShouldNotContain("first");
        retry.Repair.SafeMessage.ShouldNotContain("second");
        retry.Repair.SafeMessage.ShouldNotContain("third");
    }

    private sealed record TestPayload(string Name);

    [Fact]
    public async Task ProcessAsync_WhenRuntimeTypeDeclared_DeserializesIntoValue()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}""");
        var definition = TestFactory.Definition(OutputMode.NativeSchema, schema: schema, runtimeType: typeof(TestPayload));
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var accepted = result.ShouldBeOfType<OutputAccepted>();
        var payload = accepted.Output.Value.ShouldBeOfType<TestPayload>();
        payload.Name.ShouldBe("agent");
    }

    [Fact]
    public async Task ProcessAsync_WhenDeserializationFails_ReturnsDeserializationFailedFailure()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}""");
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema, schema: schema, runtimeType: typeof(int), retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{"name":"agent"}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.DeserializationFailed);
    }

    [Fact]
    public async Task ProcessAsync_WhenCandidateExceedsMaximumBytes_ReturnsOversizedCandidateFailure()
    {
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 4);
        var definition = TestFactory.Definition(OutputMode.Text, retryPolicy: OutputRetryPolicy.None);
        var response = TestFactory.TextResponse("this text is definitely too long");

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
    }

    [Theory]
    [InlineData(/*lang=json,strict*/"""{"value":true}""")]
    [InlineData("{{{{{{{{{{")]
    public async Task ProcessAsync_WhenStructuredTextExceedsMaximumBytes_ReturnsOversizedBeforeParsingOrValidation(
        string text)
    {
        var validator = new FakeOutputValidator("semantic", static _ => OutputValidationPassed.Instance);
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 4, [validator]);
        var definition = TestFactory.Definition(
            OutputMode.Prompted,
            schema: TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}"""),
            validators: [new OutputValidatorReference("semantic")],
            retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse(text)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
        validator.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhenMultibyteTextEqualsMaximumBytes_ReturnsAccepted()
    {
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 2);
        var definition = TestFactory.Definition(OutputMode.Text);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("é")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputAccepted>().Output.Text.ShouldBe("é");
    }

    [Fact]
    public async Task ProcessAsync_WhenMultibyteTextExceedsMaximumBytes_ReturnsOversizedCandidate()
    {
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 1);
        var definition = TestFactory.Definition(OutputMode.Text, retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("é")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.OversizedCandidate);
    }

    [Fact]
    public async Task ProcessAsync_WhenSurrogatePairSpansTextParts_CountsCombinedUtf8Bytes()
    {
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 4);
        var definition = TestFactory.Definition(OutputMode.Text);
        var response = TestFactory.Response(
            [
                new TextPart("\uD83D", TextSemantics.Plain, ExtensionData.Empty),
                new TextPart("\uDE00", TextSemantics.Plain, ExtensionData.Empty),
            ]);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputAccepted>().Output.Text.ShouldBe("😀");
    }

    [Fact]
    public async Task ProcessAsync_WhenAllowedStructuredTextIsMalformed_ReturnsMalformedJson()
    {
        var processor = CreateProcessor(options => options.MaximumCandidateBytes = 16);
        var definition = TestFactory.Definition(
            OutputMode.Prompted,
            schema: TestFactory.Schema(/*lang=json,strict*/"""{"type":"object"}"""),
            retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("{{")),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<OutputRejected>().Failure.Kind.ShouldBe(OutputValidationFailureKind.MalformedJson);
    }

    [Fact]
    public async Task ProcessAsync_WhenNamedValidatorNotRegistered_ReturnsValidatorNotFoundFailure()
    {
        var processor = CreateProcessor();
        var definition = TestFactory.Definition(
            OutputMode.Text, validators: [new OutputValidatorReference("missing")], retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.ValidatorNotFound);
    }

    [Fact]
    public async Task ProcessAsync_WhenNamedValidatorReportsIssues_ReturnsValidatorFailedFailure()
    {
        var validator = new FakeOutputValidator(
            "checker",
            static _ => new OutputValidationIssuesFound([new OutputValidationIssue("bad", "was bad", null)]));
        var processor = CreateProcessor(validators: [validator]);
        var definition = TestFactory.Definition(
            OutputMode.Text, validators: [new OutputValidatorReference("checker")], retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Kind.ShouldBe(OutputValidationFailureKind.ValidatorFailed);
        validator.ReceivedRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_WhenNamedValidatorPasses_ReturnsAccepted()
    {
        var validator = new FakeOutputValidator("checker", static _ => OutputValidationPassed.Instance);
        var processor = CreateProcessor(validators: [validator]);
        var definition = TestFactory.Definition(OutputMode.Text, validators: [new OutputValidatorReference("checker")]);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputAccepted>();
    }

    [Fact]
    public async Task ProcessAsync_WhenValidationPolicyRejectsOnFirstFailure_StopsAfterFirstFailingValidator()
    {
        var first = new FakeOutputValidator(
            "first", static _ => new OutputValidationIssuesFound([new OutputValidationIssue("a", "a", null)]));
        var second = new FakeOutputValidator(
            "second", static _ => new OutputValidationIssuesFound([new OutputValidationIssue("b", "b", null)]));
        var processor = CreateProcessor(validators: [first, second]);
        var definition = TestFactory.Definition(
            OutputMode.Text,
            validators: [new OutputValidatorReference("first"), new OutputValidatorReference("second")],
            validationPolicy: OutputValidationPolicy.RejectOnFirstFailure);

        _ = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")), TestContext.Current.CancellationToken);

        first.ReceivedRequests.Count.ShouldBe(1);
        second.ReceivedRequests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAsync_WhenValidationPolicyCollectsAllFailures_RunsEveryValidatorAndAggregatesIssues()
    {
        var first = new FakeOutputValidator(
            "first", static _ => new OutputValidationIssuesFound([new OutputValidationIssue("a", "a", null)]));
        var second = new FakeOutputValidator(
            "second", static _ => new OutputValidationIssuesFound([new OutputValidationIssue("b", "b", null)]));
        var processor = CreateProcessor(validators: [first, second]);
        var definition = TestFactory.Definition(
            OutputMode.Text,
            validators: [new OutputValidatorReference("first"), new OutputValidatorReference("second")],
            validationPolicy: new OutputValidationPolicy(OutputValidationFailureMode.CollectAllFailures, 10),
            retryPolicy: OutputRetryPolicy.None);

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<OutputRejected>();
        rejected.Failure.Issues.Length.ShouldBe(2);
        first.ReceivedRequests.Count.ShouldBe(1);
        second.ReceivedRequests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    public async Task ProcessAsync_WhenValidatorIssuesReachEffectiveLimit_BoundsResultAndRepairAndStopsValidation(
        int processorMaximumIssues,
        int definitionMaximumIssues)
    {
        var first = new FakeOutputValidator(
            "first",
            static _ => new OutputValidationIssuesFound(
                [
                    new OutputValidationIssue("first-a", "included first issue", null),
                    new OutputValidationIssue("first-b", "included second issue", null),
                    new OutputValidationIssue("first-c", "omitted issue", null),
                ]));
        var second = new FakeOutputValidator(
            "second",
            static _ => new OutputValidationIssuesFound(
                [new OutputValidationIssue("second", "validator should not run", null)]));
        var processor = CreateProcessor(
            options => options.MaximumValidationIssues = processorMaximumIssues,
            [first, second]);
        var definition = TestFactory.Definition(
            OutputMode.Text,
            validators: [new OutputValidatorReference("first"), new OutputValidatorReference("second")],
            validationPolicy: new OutputValidationPolicy(
                OutputValidationFailureMode.CollectAllFailures,
                definitionMaximumIssues),
            retryPolicy: new OutputRetryPolicy(1));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, TestFactory.TextResponse("hi")),
            TestContext.Current.CancellationToken);

        var retry = result.ShouldBeOfType<OutputRetryRequired>();
        retry.Failure.Issues.Select(static issue => issue.Code).ShouldBe(["first-a", "first-b"]);
        retry.Repair.SafeMessage.ShouldContain("included first issue");
        retry.Repair.SafeMessage.ShouldContain("included second issue");
        retry.Repair.SafeMessage.ShouldNotContain("omitted issue");
        retry.Repair.SafeMessage.ShouldNotContain("validator should not run");
        first.ReceivedRequests.Count.ShouldBe(1);
        second.ReceivedRequests.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAsync_WhenRetryAttemptsRemain_ReturnsOutputRetryRequired()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema, schema: schema, retryPolicy: new OutputRetryPolicy(2));
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response, attempt: 1), TestContext.Current.CancellationToken);

        var retry = result.ShouldBeOfType<OutputRetryRequired>();
        retry.Failure.Kind.ShouldBe(OutputValidationFailureKind.SchemaValidationFailed);
        retry.Repair.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessAsync_WhenRetryAttemptsExhausted_ReturnsOutputRejected()
    {
        var processor = CreateProcessor();
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema, schema: schema, retryPolicy: new OutputRetryPolicy(1));
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response, attempt: 2), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputRejected>();
    }

    [Fact]
    public async Task ProcessAsync_WhenProcessorOptionCapsBelowDefinitionRetryPolicy_UsesLesserValue()
    {
        var processor = CreateProcessor(options => options.MaximumRepairAttempts = 0);
        var schema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"object","required":["name"]}""");
        var definition = TestFactory.Definition(
            OutputMode.NativeSchema, schema: schema, retryPolicy: new OutputRetryPolicy(5));
        var response = TestFactory.StructuredResponse(TestFactory.ParseJson(/*lang=json,strict*/"""{}"""));

        var result = await processor.ProcessAsync(
            TestFactory.ProcessingRequest(definition, response, attempt: 1), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<OutputRejected>();
    }

    private static AgentOutputOptionsSnapshot DefaultOptions(Action<AgentOutputOptions>? configure = null)
    {
        var options = new AgentOutputOptions();
        configure?.Invoke(options);
        return new AgentOutputOptionsSnapshot(
            options.MaximumCandidateBytes,
            options.MaximumSchemaBytes,
            options.MaximumSchemaDepth,
            options.MaximumSchemaNodes,
            options.MaximumCandidateDepth,
            options.MaximumCandidateNodes,
            options.MaximumValidationIssues,
            options.MaximumRepairAttempts,
            options.RequireSchemaForStructuredModes,
            options.AllowProviderModeDowngrade);
    }

    private static DefaultOutputProcessor CreateProcessor(
        Action<AgentOutputOptions>? configure = null, IEnumerable<IOutputValidator>? validators = null) =>
        new(validators ?? [], new StructuralOutputSchemaEngine(), DefaultOptions(configure));

    /// <summary>Represents the runtime value used to verify bounded structured-output deserialization.</summary>
    private sealed class WhitespaceRuntimeValue
    {
        /// <summary>Gets or sets the value recovered from the canonical candidate JSON.</summary>
        public int Value { get; set; }
    }
}
