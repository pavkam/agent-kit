// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class JsonEvaluationStoreTargetTests
{
    private static readonly JsonEvaluationStoreInstanceId _id = new(Guid.NewGuid());

    private static string Absolute => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "evaluation-target"));

    [Fact]
    public void Constructor_WhenPathIsInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new JsonEvaluationStoreTarget(null!, _id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonEvaluationStoreTarget(" ", _id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonEvaluationStoreTarget("relative/path", _id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)).ParamName.ShouldBe("directoryPath");
    }

    [Fact]
    public void Constructor_WhenIdentityOrModesAreInvalid_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreTarget(Absolute, default, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreTarget(Absolute, _id, (JsonStoreOpenMode) 9, JsonStoreRecoveryMode.RecoverTornAppends)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreTarget(Absolute, _id, JsonStoreOpenMode.CreateIfMissing, (JsonStoreRecoveryMode) 9)).ParamName.ShouldBe("recoveryMode");
    }

    [Fact]
    public void Constructor_WhenCreationIsCombinedWithExactValidation_IsRefusedAsContradictory() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreTarget(Absolute, _id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("recoveryMode");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_NormalizesThePathAndKeepsTheModes()
    {
        var target = new JsonEvaluationStoreTarget(Absolute + "/./sub/..", _id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        target.DirectoryPath.ShouldBe(Absolute);
        (target.ExpectedInstanceId, target.OpenMode, target.RecoveryMode).ShouldBe((_id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
    }
}
