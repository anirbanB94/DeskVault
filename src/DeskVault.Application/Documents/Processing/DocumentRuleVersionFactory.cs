using DeskVault.Application.Documents.Chunking;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Application.Documents.Processing;

public static class DocumentRuleVersionFactory
{
    public static DocumentProcessingRuleVersion CreateProcessingRuleVersion(
        string extractorRuleVersion,
        string normalizationRuleVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            extractorRuleVersion);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            normalizationRuleVersion);

        string canonicalDefinition =
            string.Join(
                '\n',
                "processing-rule",
                $"extractor={extractorRuleVersion}",
                $"normalizer={normalizationRuleVersion}");

        return new DocumentProcessingRuleVersion(
            ComputeStableIdentifier(
                canonicalDefinition));
    }

    public static DocumentChunkingRuleVersion CreateChunkingRuleVersion(
        string chunkingAlgorithmVersion,
        int maxChunkSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            chunkingAlgorithmVersion);

        if (maxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkSize),
                "Maximum chunk size must be greater than zero.");
        }

        string canonicalDefinition =
            string.Join(
                '\n',
                "chunking-rule",
                $"algorithm={chunkingAlgorithmVersion}",
                $"maxChunkSize={maxChunkSize.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)}");

        return new DocumentChunkingRuleVersion(
            ComputeStableIdentifier(
                canonicalDefinition));
    }

    private static string ComputeStableIdentifier(
        string canonicalDefinition)
    {
        byte[] definitionBytes =
            Encoding.UTF8.GetBytes(
                canonicalDefinition);

        byte[] hash =
            SHA256.HashData(
                definitionBytes);

        return Convert.ToHexString(
                hash)
            .ToLowerInvariant();
    }
}
