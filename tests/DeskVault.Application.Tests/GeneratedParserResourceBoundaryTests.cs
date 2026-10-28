using System.Text;
using System.Text.Json;
using System.Xml;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Processing;
using YamlDotNet.Core;

namespace DeskVault.Application.Tests;

/// <summary>
/// Deterministic, bounded adversarial regression coverage for parser resource
/// boundaries. The tests use real extractors because the parser boundaries are
/// the system under test; no mocks are required for these in-memory scenarios.
/// </summary>
public sealed class GeneratedParserResourceBoundaryTests
{
    [Fact]
    public async Task GeneratedJsonNestingDepths_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] depths = [10, 12, 16, 24, 32];

        foreach (int depth in depths)
        {
            string json = BuildNestedJson(depth);
            ParserCase parser = CreateJsonParser(
                CreateOptions(maximumDepth: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, json);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "JSON nesting depth");
        }
    }

    [Fact]
    public async Task GeneratedJsonPropertyCounts_AboveNodeLimit_FailExplicitly()
    {
        // Arrange
        int[] propertyCounts = [9, 16, 32, 64];

        foreach (int propertyCount in propertyCounts)
        {
            string properties = string.Join(
                ",",
                Enumerable.Range(0, propertyCount)
                    .Select(index => $"\"key{index}\":{index}"));

            string json = "{" + properties + "}";
            ParserCase parser = CreateJsonParser(
                CreateOptions(maximumNodeCount: 16));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, json);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "JSON structured nodes");
        }
    }

    [Fact]
    public async Task GeneratedMalformedJsonVariants_AreRejected()
    {
        // Arrange
        int[] itemCounts = [1, 3, 8, 16];

        foreach (int itemCount in itemCounts)
        {
            string items = string.Join(
                ",",
                Enumerable.Range(0, itemCount));

            // Intentionally omit the closing JSON delimiters.
            string json = "{\"values\":[" + items;
            ParserCase parser = CreateJsonParser(CreateOptions());

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, json);

            // Assert
            await Assert.ThrowsAnyAsync<JsonException>(
                () => extractionTask);
        }
    }

    [Fact]
    public async Task GeneratedXmlNestingDepths_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] depths = [10, 12, 16, 24, 32];

        foreach (int depth in depths)
        {
            string xml = BuildNestedXml(depth);
            ParserCase parser = CreateXmlParser(
                CreateOptions(maximumDepth: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, xml);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "XML nesting depth");
        }
    }

    [Fact]
    public async Task GeneratedXmlElementCounts_AboveNodeLimit_FailExplicitly()
    {
        // Arrange
        int[] childCounts = [9, 16, 32, 64];

        foreach (int childCount in childCounts)
        {
            var builder = new StringBuilder("<root>");

            for (int index = 0; index < childCount; index++)
            {
                builder.Append("<item id=\"")
                    .Append(index)
                    .Append("\" />");
            }

            builder.Append("</root>");

            ParserCase parser = CreateXmlParser(
                CreateOptions(maximumNodeCount: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, builder.ToString());

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "XML structured nodes");
        }
    }

    [Fact]
    public async Task GeneratedMalformedXmlVariants_AreRejected()
    {
        // Arrange
        int[] childCounts = [1, 3, 8, 16];

        foreach (int childCount in childCounts)
        {
            var builder = new StringBuilder("<root>");

            for (int index = 0; index < childCount; index++)
            {
                builder.Append("<item>")
                    .Append(index)
                    .Append("</item>");
            }

            // Intentionally leave the root closing tag incomplete.
            builder.Append("</root");

            ParserCase parser = CreateXmlParser(CreateOptions());

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, builder.ToString());

            // Assert
            await Assert.ThrowsAsync<XmlException>(
                () => extractionTask);
        }
    }

    [Fact]
    public async Task GeneratedYamlNestingDepths_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] depths = [10, 12, 16, 24, 32];

        foreach (int depth in depths)
        {
            string yaml = BuildNestedYaml(depth);
            ParserCase parser = CreateYamlParser(
                CreateOptions(maximumDepth: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, yaml);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "YAML nesting depth");
        }
    }

    [Fact]
    public async Task GeneratedYamlNodeCounts_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] entryCounts = [9, 16, 32, 64];

        foreach (int entryCount in entryCounts)
        {
            var builder = new StringBuilder();

            for (int index = 0; index < entryCount; index++)
            {
                builder.Append("key")
                    .Append(index)
                    .Append(": value")
                    .Append(index)
                    .AppendLine();
            }

            ParserCase parser = CreateYamlParser(
                CreateOptions(maximumNodeCount: 16));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, builder.ToString());

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "YAML structured nodes");
        }
    }

    [Fact]
    public async Task GeneratedYamlAliasFanOut_AboveExpandedNodeLimit_FailsExplicitly()
    {
        // Arrange
        const string yaml =
            """
        defaults: &defaults
          a: 1
          b: 2
          c: 3
        first: *defaults
        second: *defaults
        """;

        // The parser-event preflight counts the source structure and alias
        // references, while rendering traverses the aliased mapping repeatedly.
        ParserCase parser = CreateYamlParser(
            CreateOptions(maximumNodeCount: 14));

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(parser, yaml);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("YAML expanded nodes", exception.ResourceName);
        Assert.Equal(14L, exception.Limit);
        Assert.Equal(15L, exception.Attempted);
    }

    [Fact]
    public async Task GeneratedMalformedYamlVariants_AreRejected()
    {
        // Arrange
        int[] itemCounts = [1, 3, 8, 16];

        foreach (int itemCount in itemCounts)
        {
            string items = string.Join(
                ", ",
                Enumerable.Range(0, itemCount)
                    .Select(index => $"item{index}"));

            // Intentionally omit the closing flow-sequence delimiter.
            string yaml = "values: [" + items;
            ParserCase parser = CreateYamlParser(CreateOptions());

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, yaml);

            // Assert
            await Assert.ThrowsAnyAsync<YamlException>(
                () => extractionTask);
        }
    }

    [Fact]
    public async Task GeneratedCsvRowCounts_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] rowCounts = [9, 16, 32, 64];

        foreach (int rowCount in rowCounts)
        {
            string rows = string.Join(
                "\n",
                Enumerable.Range(0, rowCount)
                    .Select(index => index.ToString()));

            string csv = "Id\n" + rows;
            ParserCase parser = CreateCsvParser(
                CreateOptions(maximumCsvRows: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, csv);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "CSV data rows");
        }
    }

    [Fact]
    public async Task GeneratedCsvColumnCounts_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] columnCounts = [9, 16, 32, 64];

        foreach (int columnCount in columnCounts)
        {
            string header = string.Join(
                ",",
                Enumerable.Range(0, columnCount)
                    .Select(index => $"column{index}"));

            string row = string.Join(
                ",",
                Enumerable.Repeat("value", columnCount));

            ParserCase parser = CreateCsvParser(
                CreateOptions(maximumCsvColumns: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, header + "\n" + row);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "CSV columns");
        }
    }

    [Fact]
    public async Task GeneratedCsvFieldLengths_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] fieldLengths = [9, 16, 32, 64];

        foreach (int fieldLength in fieldLengths)
        {
            string csv = "Id\n" + new string('x', fieldLength);
            ParserCase parser = CreateCsvParser(
                CreateOptions(maximumCsvFieldLength: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, csv);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "CSV field length");
        }
    }

    [Fact]
    public async Task GeneratedCsvCellCounts_AboveLimit_FailExplicitly()
    {
        // Arrange
        const int columnCount = 4;
        const int dataRowCount = 5;

        string header = string.Join(
            ",",
            Enumerable.Range(0, columnCount)
                .Select(index => $"c{index}"));

        string rows = string.Join(
            "\n",
            Enumerable.Range(0, dataRowCount)
                .Select(rowIndex =>
                    string.Join(
                        ",",
                        Enumerable.Range(0, columnCount)
                            .Select(columnIndex =>
                                $"r{rowIndex}c{columnIndex}"))));

        string csv = header + "\n" + rows;
        ParserCase parser = CreateCsvParser(
            CreateOptions(maximumCsvCells: 20));

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(parser, csv);

        // Assert
        await AssertStructureLimitExceededAsync(
            extractionTask,
            "CSV cells");
    }

    [Fact]
    public async Task GeneratedIniEntryCounts_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] entryCounts = [9, 16, 32, 64];

        foreach (int entryCount in entryCounts)
        {
            var builder = new StringBuilder("[generated]\n");

            for (int index = 0; index < entryCount; index++)
            {
                builder.Append("key")
                    .Append(index)
                    .Append("=value")
                    .Append(index)
                    .AppendLine();
            }

            ParserCase parser = CreateIniParser(
                CreateOptions(maximumIniEntries: 8));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, builder.ToString());

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "INI entries");
        }
    }

    [Fact]
    public async Task GeneratedIniLineLengths_AboveLimit_FailExplicitly()
    {
        // Arrange
        int[] valueLengths = [13, 16, 32, 64];

        foreach (int valueLength in valueLengths)
        {
            string ini = "[generated]\nkey=" +
                new string('x', valueLength);

            ParserCase parser = CreateIniParser(
                CreateOptions(maximumIniLineLength: 16));

            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(parser, ini);

            // Assert
            await AssertStructureLimitExceededAsync(
                extractionTask,
                "INI line length");
        }
    }

    [Fact]
    public async Task GeneratedOversizedSources_AreRejectedBeforeExtractionSucceeds()
    {
        // Arrange
        const int sourceLimitBytes = 64;
        string payload = new string('x', 96);

        (ParserCase Parser, string Content)[] cases =
        [
            (
                CreateJsonParser(
                    CreateOptions(
                        maximumDecryptedDocumentBytes: sourceLimitBytes)),
                "{\"value\":\"" + payload + "\"}"
            ),
            (
                CreateXmlParser(
                    CreateOptions(
                        maximumDecryptedDocumentBytes: sourceLimitBytes)),
                "<root>" + payload + "</root>"
            ),
            (
                CreateYamlParser(
                    CreateOptions(
                        maximumDecryptedDocumentBytes: sourceLimitBytes)),
                "value: " + payload
            ),
            (
                CreateIniParser(
                    CreateOptions(
                        maximumDecryptedDocumentBytes: sourceLimitBytes)),
                "[generated]\nkey=" + payload
            )
        ];

        foreach (var testCase in cases)
        {
            // Act
            Task<DocumentTextExtractionResult> extractionTask =
                ExtractAsync(testCase.Parser, testCase.Content);

            // Assert
            await AssertSourceLimitExceededAsync(extractionTask);
        }
    }

    private static DocumentProcessingOptions CreateOptions(
        long maximumDecryptedDocumentBytes = 4096,
        long maximumProcessedTextBytes = 4096,
        int maximumDepth = 64,
        int maximumNodeCount = 100_000,
        int maximumCsvRows = 100_000,
        int maximumCsvColumns = 1024,
        int maximumCsvFieldLength = 1_048_576,
        int maximumCsvCells = 500_000,
        int maximumIniEntries = 100_000,
        int maximumIniLineLength = 1_048_576)
    {
        return new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = maximumDecryptedDocumentBytes,
            MaxProcessedTextBytes = maximumProcessedTextBytes,
            MaxParserNestingDepth = maximumDepth,
            MaxStructuredNodeCount = maximumNodeCount,
            MaxCsvRows = maximumCsvRows,
            MaxCsvColumns = maximumCsvColumns,
            MaxCsvFieldLength = maximumCsvFieldLength,
            MaxCsvCells = maximumCsvCells,
            MaxIniEntries = maximumIniEntries,
            MaxIniLineLength = maximumIniLineLength
        };
    }

    private static ParserCase CreateJsonParser(
        DocumentProcessingOptions options)
    {
        var extractor = new JsonDocumentTextExtractor(options);

        return new ParserCase("generated.json", extractor.ExtractAsync);
    }

    private static ParserCase CreateXmlParser(
        DocumentProcessingOptions options)
    {
        var extractor = new XmlDocumentTextExtractor(options);

        return new ParserCase("generated.xml", extractor.ExtractAsync);
    }

    private static ParserCase CreateYamlParser(
        DocumentProcessingOptions options)
    {
        var extractor = new YamlDocumentTextExtractor(options);

        return new ParserCase("generated.yaml", extractor.ExtractAsync);
    }

    private static ParserCase CreateCsvParser(
        DocumentProcessingOptions options)
    {
        var extractor = new CsvDocumentTextExtractor(options);

        return new ParserCase("generated.csv", extractor.ExtractAsync);
    }

    private static ParserCase CreateIniParser(
        DocumentProcessingOptions options)
    {
        var extractor = new IniDocumentTextExtractor(options);

        return new ParserCase("generated.ini", extractor.ExtractAsync);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        ParserCase parser,
        string content)
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(content));

        return await parser.ExtractAsync(
            stream,
            parser.FileName,
            TestContext.Current.CancellationToken);
    }

    private static async Task AssertStructureLimitExceededAsync(
        Task<DocumentTextExtractionResult> extractionTask,
        string expectedResourceName)
    {
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal(expectedResourceName, exception.ResourceName);
        Assert.True(exception.Attempted > exception.Limit);
    }

    private static async Task AssertSourceLimitExceededAsync(
        Task<DocumentTextExtractionResult> extractionTask)
    {
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.True(exception.AttemptedBytes > exception.LimitBytes);
    }

    private static string BuildNestedJson(int depth)
    {
        return new string('[', depth) +
            "0" +
            new string(']', depth);
    }

    private static string BuildNestedXml(int depth)
    {
        var builder = new StringBuilder();

        for (int index = 0; index < depth; index++)
        {
            builder.Append("<n").Append(index).Append('>');
        }

        builder.Append("value");

        for (int index = depth - 1; index >= 0; index--)
        {
            builder.Append("</n").Append(index).Append('>');
        }

        return builder.ToString();
    }

    private static string BuildNestedYaml(int depth)
    {
        var builder = new StringBuilder();

        for (int level = 0; level < depth; level++)
        {
            builder.Append(' ', level * 2)
                .Append("level")
                .Append(level)
                .AppendLine(":");
        }

        builder.Append(' ', depth * 2)
            .AppendLine("leaf: value");

        return builder.ToString();
    }

    private sealed record ParserCase(
        string FileName,
        Func<
            Stream,
            string,
            CancellationToken,
            Task<DocumentTextExtractionResult>> ExtractAsync);
}
