using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Provenance;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class DocumentSourceLocationMappingTests
{
    [Theory]
    [InlineData(
        "document.csv",
        """
        name,purpose
        DeskVault,enterprise knowledge search
        """)]
    [InlineData(
        "document.ini",
        """
        [document]
        name=DeskVault
        purpose=enterprise knowledge search
        """)]
    [InlineData(
        "document.config",
        """
        [document]
        name=DeskVault
        purpose=enterprise knowledge search
        """)]
    [InlineData(
        "document.json",
        """
        {
          "name": "DeskVault",
          "purpose": "enterprise knowledge search"
        }
        """)]
    [InlineData(
        "document.xml",
        """
        <document>
          <name>DeskVault</name>
          <purpose>enterprise knowledge search</purpose>
        </document>
        """)]
    [InlineData(
        "document.yaml",
        """
        name: DeskVault
        purpose: enterprise knowledge search
        """)]
    [InlineData(
        "document.yml",
        """
        name: DeskVault
        purpose: enterprise knowledge search
        """)]
    public async Task ExtractAsync_WhenExtractorTransformsSource_DoesNotClaimDirectSourceLocation(
        string fileName,
        string sourceText)
    {
        IDocumentTextExtractor extractor =
            CreateExtractor(fileName);

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    sourceText));

        DocumentTextExtractionResult result =
            await extractor.ExtractAsync(
                stream,
                fileName);

        Assert.Equal(
            DocumentSourceLocationMappingKind.Unknown,
            result.SourceLocationMappingKind);
    }

    private static IDocumentTextExtractor CreateExtractor(
        string fileName)
    {
        string extension =
            Path.GetExtension(fileName);

        return extension.ToLowerInvariant() switch
        {
            ".csv" =>
                new CsvDocumentTextExtractor(),

            ".ini" or ".config" =>
                new IniDocumentTextExtractor(),

            ".json" =>
                new JsonDocumentTextExtractor(),

            ".xml" =>
                new XmlDocumentTextExtractor(),

            ".yaml" or ".yml" =>
                new YamlDocumentTextExtractor(),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(fileName),
                    fileName,
                    "No transformed document extractor is registered for this test.")
        };
    }
}
