using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

namespace DeskVault.Application.Tests;

public sealed class DocumentArtifactReconciliationResultTests
{
    [Theory]
    [InlineData(
        DocumentArtifactReconciliationStatus.Matched,
        DocumentArtifactReconciliationRecoveryAction.None)]

    [InlineData(
        DocumentArtifactReconciliationStatus.MissingArtifact,
        DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery)]

    [InlineData(
        DocumentArtifactReconciliationStatus.OrphanedArtifact,
        DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact)]

    [InlineData(
        DocumentArtifactReconciliationStatus.UnreadableArtifact,
        DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery)]

    [InlineData(
        DocumentArtifactReconciliationStatus.PathMismatch,
        DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery)]
    public void RecoveryAction_ReturnsExpectedAction(
        DocumentArtifactReconciliationStatus status,
        DocumentArtifactReconciliationRecoveryAction expectedAction)
    {
        // Arrange
        var result =
            new DocumentArtifactReconciliationResult(
                status,
                Guid.NewGuid(),
                @"C:\DeskVault\Documents\artifact.dvault",
                "Test reconciliation finding.");

        // Act
        DocumentArtifactReconciliationRecoveryAction actualAction =
            result.RecoveryAction;

        // Assert
        Assert.Equal(
            expectedAction,
            actualAction);
    }

    [Fact]
    public void RecoveryAction_WhenOrphanedArtifactHasNoDocumentIdentity_PreservesForRecovery()
    {
        // Arrange
        var result =
            new DocumentArtifactReconciliationResult(
                DocumentArtifactReconciliationStatus.OrphanedArtifact,
                null,
                @"C:\DeskVault\Documents\not-a-document-id.dvault",
                "Test reconciliation finding.");

        // Act
        DocumentArtifactReconciliationRecoveryAction actualAction =
            result.RecoveryAction;

        // Assert
        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            actualAction);
    }
}
