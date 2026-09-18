// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the validated immutable JSON session-store bootstrap target.</summary>
public sealed class JsonSessionStoreTargetTests
{
    /// <summary>Verifies a fully qualified path is normalized and every value is preserved unchanged.</summary>
    [Fact]
    public void Constructor_WhenPathIsFullyQualified_NormalizesAndPreservesEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentkit-target-{Guid.NewGuid():N}", "sessions");
        var instance = new JsonSessionStoreInstanceId(Guid.NewGuid());

        var target = new JsonSessionStoreTarget(
            path, instance, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        target.DirectoryPath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedStoreInstanceId.ShouldBe(instance);
        target.OpenMode.ShouldBe(JsonStoreOpenMode.CreateIfMissing);
        target.RecoveryMode.ShouldBe(JsonStoreRecoveryMode.RecoverTornAppends);
    }

    /// <summary>Verifies a null root directory is rejected before any other member is validated.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new JsonSessionStoreTarget(
                null!, new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends))
            .ParamName.ShouldBe("directoryPath");

    /// <summary>Verifies a blank root directory is rejected as blank rather than resolved to the process directory.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDirectoryPathIsBlank_ThrowsExactParameter(string path) =>
        Should.Throw<ArgumentException>(() => new JsonSessionStoreTarget(
                path, new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends))
            .ParamName.ShouldBe("directoryPath");

    /// <summary>Verifies a relative root is rejected rather than silently resolved against the current directory.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsRelative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonSessionStoreTarget(
            "relative/sessions", new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("directoryPath");
        exception.Message.ShouldContain("fully qualified path");
    }

    /// <summary>Verifies a default store-instance identity is rejected.</summary>
    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions"), default, JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends))
            .ParamName.ShouldBe("expectedStoreInstanceId");

    /// <summary>Verifies an undefined open mode is rejected.</summary>
    [Fact]
    public void Constructor_WhenOpenModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
                (JsonStoreOpenMode) 99, JsonStoreRecoveryMode.RecoverTornAppends))
            .ParamName.ShouldBe("openMode");

    /// <summary>Verifies an undefined recovery mode is rejected.</summary>
    [Fact]
    public void Constructor_WhenRecoveryModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing, (JsonStoreRecoveryMode) 99))
            .ParamName.ShouldBe("recoveryMode");

    /// <summary>Verifies creation combined with validation-only recovery is rejected, since a permitted-create root must also be recoverable.</summary>
    [Fact]
    public void Constructor_WhenCreateIfMissingCombinedWithValidateExact_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonSessionStoreTarget(
                Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact))
            .ParamName.ShouldBe("recoveryMode");

    /// <summary>Verifies opening an existing root combined with strict validation is accepted.</summary>
    [Fact]
    public void Constructor_WhenOpenExistingCombinedWithValidateExact_Accepts()
    {
        var target = new JsonSessionStoreTarget(
            Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        target.OpenMode.ShouldBe(JsonStoreOpenMode.OpenExisting);
        target.RecoveryMode.ShouldBe(JsonStoreRecoveryMode.ValidateExact);
    }

    /// <summary>Verifies two targets built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var path = Path.Combine(Path.GetTempPath(), "sessions");
        var instance = new JsonSessionStoreInstanceId(Guid.NewGuid());
        var left = new JsonSessionStoreTarget(
            path, instance, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
        var right = new JsonSessionStoreTarget(
            path, instance, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        left.ShouldBe(right);
    }

    /// <summary>Verifies a differing instance identity breaks equality.</summary>
    [Fact]
    public void Equals_WhenInstanceIdDiffers_IsNotEqual()
    {
        var path = Path.Combine(Path.GetTempPath(), "sessions");
        var left = new JsonSessionStoreTarget(
            path, new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends);
        var right = new JsonSessionStoreTarget(
            path, new JsonSessionStoreInstanceId(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends);

        left.ShouldNotBe(right);
    }

    /// <summary>Verifies the record's textual representation carries the significant field names.</summary>
    [Fact]
    public void ToString_WhenCalled_IncludesCapturedFieldNames()
    {
        var target = new JsonSessionStoreTarget(
            Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        var text = target.ToString();

        text.ShouldContain(nameof(JsonSessionStoreTarget.DirectoryPath));
        text.ShouldContain(nameof(JsonSessionStoreTarget.OpenMode));
    }

    /// <summary>Verifies an unmodified structural clone equals the original.</summary>
    [Fact]
    public void With_WhenCalledWithoutChanges_ClonesEveryField()
    {
        var target = new JsonSessionStoreTarget(
            Path.Combine(Path.GetTempPath(), "sessions"), new JsonSessionStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.RecoverTornAppends);

        var cloned = target with { };

        cloned.ShouldNotBeSameAs(target);
        cloned.ShouldBe(target);
    }
}
