using DeskVault.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsContinuationCodec
    : ISearchDocumentsContinuationCodec
{
    private const int CurrentFormatVersion = 1;
    private const int CurrentRankingContractVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNamingPolicy = null
        };

    public SearchDocumentsContinuation Create(
        SearchDocumentsQuery query,
        SearchDocumentsRankingKey rankingKey)
    {
        ArgumentNullException.ThrowIfNull(query);

        string criteriaFingerprint =
            ComputeCriteriaFingerprint(
                query);

        ContinuationPayload payload =
            new(
                CurrentFormatVersion,
                CurrentRankingContractVersion,
                criteriaFingerprint,
                rankingKey.RelevanceScore,
                rankingKey.DisplayName,
                rankingKey.DocumentId);

        string json =
            JsonSerializer.Serialize(
                payload,
                SerializerOptions);

        byte[] bytes =
            Encoding.UTF8.GetBytes(
                json);

        return new SearchDocumentsContinuation(
            Base64UrlEncode(bytes));
    }

    public SearchDocumentsRankingKey Decode(
        SearchDocumentsQuery query,
        SearchDocumentsContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(continuation);

        if (string.IsNullOrWhiteSpace(
                continuation.Value))
        {
            throw new ArgumentException(
                "Continuation value cannot be empty.",
                nameof(continuation));
        }

        try
        {
            byte[] bytes =
                Base64UrlDecode(
                    continuation.Value);

            string json =
                Encoding.UTF8.GetString(
                    bytes);

            ContinuationPayload? payload =
                JsonSerializer.Deserialize<ContinuationPayload>(
                    json,
                    SerializerOptions);

            if (payload is null)
            {
                throw new ArgumentException(
                    "Continuation value is invalid.",
                    nameof(continuation));
            }

            if (payload.FormatVersion !=
                CurrentFormatVersion)
            {
                throw new ArgumentException(
                    "Continuation format version is not supported.",
                    nameof(continuation));
            }

            if (payload.RankingContractVersion !=
                CurrentRankingContractVersion)
            {
                throw new ArgumentException(
                    "Continuation ranking contract version is not supported.",
                    nameof(continuation));
            }

            string expectedCriteriaFingerprint =
                ComputeCriteriaFingerprint(
                    query);

            if (!string.Equals(
                    payload.CriteriaFingerprint,
                    expectedCriteriaFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Continuation does not match the search criteria.",
                    nameof(continuation));
            }

            if (payload.DisplayName is null)
            {
                throw new ArgumentException(
                    "Continuation ranking position is invalid.",
                    nameof(continuation));
            }

            return new SearchDocumentsRankingKey(
                payload.RelevanceScore,
                payload.DisplayName,
                payload.DocumentId);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is FormatException
            || ex is JsonException
            || ex is NotSupportedException
            || ex is DecoderFallbackException)
        {
            throw new ArgumentException(
                "Continuation value is invalid.",
                nameof(continuation),
                ex);
        }
    }

    private static string ComputeCriteriaFingerprint(
        SearchDocumentsQuery query)
    {
        string normalizedSearchText =
            SearchTextCanonicalizer.CanonicalizeSearchText(
                query.SearchText);

        IReadOnlyList<string> normalizedFileTypes =
            NormalizeFileTypes(
                query.FileTypes);

        string canonicalCriteria =
            JsonSerializer.Serialize(
                new
                {
                    SearchText = normalizedSearchText,
                    FileTypes = normalizedFileTypes
                },
                SerializerOptions);

        byte[] hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    canonicalCriteria));

        return Convert.ToHexString(
            hash);
    }

    private static IReadOnlyList<string> NormalizeFileTypes(
        IReadOnlyList<string>? fileTypes)
    {
        if (fileTypes is null ||
            fileTypes.Count == 0)
        {
            return [];
        }

        return fileTypes
            .Where(
                fileType =>
                    !string.IsNullOrWhiteSpace(
                        fileType))
            .Select(
                fileType =>
                {
                    string normalized =
                        fileType.Trim();

                    return normalized.StartsWith(
                        ".",
                        StringComparison.Ordinal)
                        ? normalized.ToLowerInvariant()
                        : $".{normalized.ToLowerInvariant()}";
                })
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                fileType => fileType,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static string Base64UrlEncode(
        byte[] bytes)
    {
        return Convert
            .ToBase64String(bytes)
            .TrimEnd('=')
            .Replace(
                '+',
                '-')
            .Replace(
                '/',
                '_');
    }

    private static byte[] Base64UrlDecode(
        string value)
    {
        string padded =
            value
                .Replace(
                    '-',
                    '+')
                .Replace(
                    '_',
                    '/');

        int remainder =
            padded.Length % 4;

        if (remainder != 0)
        {
            padded =
                padded.PadRight(
                    padded.Length + (4 - remainder),
                    '=');
        }

        return Convert.FromBase64String(
            padded);
    }

    private sealed record ContinuationPayload(
        int FormatVersion,
        int RankingContractVersion,
        string CriteriaFingerprint,
        int RelevanceScore,
        string DisplayName,
        Guid DocumentId);
}
