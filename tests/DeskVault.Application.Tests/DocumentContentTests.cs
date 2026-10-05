using DeskVault.Application.Documents.Content;

namespace DeskVault.Application.Tests;

public sealed class DocumentContentTests
{
    [Fact]
    public void TextOnly_NonEmptyText_CreatesSingleTextUnit()
    {
        // Arrange
        const string text = "Hello world";

        // Act
        DocumentContent content =
            DocumentContent.TextOnly(text);

        // Assert
        Assert.Single(content.Units);

        DocumentContentUnit unit =
            content.Units[0];

        Assert.Equal(
            0,
            unit.Order);

        Assert.Equal(
            DocumentContentUnitKind.Text,
            unit.Kind);

        Assert.Equal(
            text,
            unit.Text);

        Assert.Empty(
            unit.Fields);

        Assert.Equal(
            text,
            content.SearchableText);
    }

    [Fact]
    public void TextOnly_EmptyText_CreatesNoUnits()
    {
        // Arrange
        const string text = "";

        // Act
        DocumentContent content =
            DocumentContent.TextOnly(text);

        // Assert
        Assert.Empty(
            content.Units);

        Assert.Equal(
            string.Empty,
            content.SearchableText);
    }

    [Fact]
    public void SearchableText_TextUnits_PreservesUnitOrder()
    {
        // Arrange
        DocumentContent content =
            CreateTextContent(
                "First",
                "Second");

        // Act
        string searchableText =
            content.SearchableText;

        // Assert
        Assert.Equal(
            "First\n\nSecond",
            searchableText);
    }

    [Fact]
    public void SearchableText_TableRowUnits_ProjectsFieldsDeterministically()
    {
        // Arrange
        DocumentContent content =
            CreateTableContent(
                CreateField("Id", "1001"),
                CreateField("Name", "Alice Johnson"),
                CreateField("Department", "Engineering"));

        // Act
        string searchableText =
            content.SearchableText;

        // Assert
        Assert.Equal(
            "Id: 1001\nName: Alice Johnson\nDepartment: Engineering",
            searchableText);
    }

    [Fact]
    public void SearchableText_MultipleUnits_UsesExpectedUnitSeparator()
    {
        // Arrange
        DocumentContent content =
            new(
            [
                CreateTableRow(
                    order: 0,
                    CreateField("Id", "1001"),
                    CreateField("Name", "Alice")),

                CreateTableRow(
                    order: 1,
                    CreateField("Id", "1002"),
                    CreateField("Name", "Bob"))
            ]);

        // Act
        string searchableText =
            content.SearchableText;

        // Assert
        Assert.Equal(
            "Id: 1001\nName: Alice\n\nId: 1002\nName: Bob",
            searchableText);
    }

    [Fact]
    public void Warnings_ArePreservedWithoutChangingSearchableProjection()
    {
        // Arrange
        const string warningMessage =
            "The source document contained an uneven row.";

        DocumentContent content =
            CreateTextContent(
                ["Document text"],
                [
                    new DocumentContentWarning(
                        warningMessage)
                ]);

        // Act
        string searchableText =
            content.SearchableText;

        // Assert
        Assert.Single(
            content.Warnings);

        Assert.Equal(
            warningMessage,
            content.Warnings[0].Message);

        Assert.Equal(
            "Document text",
            searchableText);
    }

    [Fact]
    public void Constructor_NonContiguousOrders_AreRejected()
    {
        // Arrange
        DocumentContentUnit[] units =
        [
            CreateTextUnit(
                0,
                "First"),

            CreateTextUnit(
                2,
                "Second")
        ];

        // Act
        Action act =
            () => new DocumentContent(units);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void Constructor_PreservesSuppliedUnitOrder()
    {
        // Arrange
        DocumentContentUnit first =
            CreateTextUnit(
                0,
                "First");

        DocumentContentUnit second =
            CreateTextUnit(
                1,
                "Second");

        DocumentContentUnit[] units =
        [
            first,
            second
        ];

        // Act
        DocumentContent content =
            new(units);

        // Assert
        Assert.Equal(
            first,
            content.Units[0]);

        Assert.Equal(
            second,
            content.Units[1]);
    }

    [Fact]
    public void TableRowUnit_PreservesFieldOrder()
    {
        // Arrange
        DocumentContentUnit unit =
            CreateTableRow(
                order: 0,
                CreateField("First", "1"),
                CreateField("Second", "2"),
                CreateField("Third", "3"));

        // Act
        IReadOnlyList<DocumentContentField> fields =
            unit.Fields;

        // Assert
        Assert.Collection(
            fields,
            field => Assert.Equal(
                "First",
                field.Name),
            field => Assert.Equal(
                "Second",
                field.Name),
            field => Assert.Equal(
                "Third",
                field.Name));
    }

    [Fact]
    public void TextUnit_DoesNotAcceptStructuredFields()
    {
        // Arrange
        DocumentContentField[] fields =
        [
            CreateField(
                "Name",
                "Alice")
        ];

        // Act
        Action act =
            () => new DocumentContentUnit(
                order: 0,
                kind: DocumentContentUnitKind.Text,
                text: "Plain text",
                fields: fields);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void TableRowUnit_DoesNotAcceptPlainText()
    {
        // Arrange
        DocumentContentField[] fields = [];

        // Act
        Action act =
            () => new DocumentContentUnit(
                order: 0,
                kind: DocumentContentUnitKind.TableRow,
                text: "Plain text",
                fields: fields);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    private static DocumentContent CreateTextContent(
        params string[] texts)
    {
        DocumentContentUnit[] units =
            texts
                .Select(
                    (text, index) =>
                        CreateTextUnit(
                            index,
                            text))
                .ToArray();

        return new DocumentContent(
            units);
    }

    private static DocumentContent CreateTextContent(
        IReadOnlyList<string> texts,
        IReadOnlyList<DocumentContentWarning> warnings)
    {
        DocumentContentUnit[] units =
            texts
                .Select(
                    (text, index) =>
                        CreateTextUnit(
                            index,
                            text))
                .ToArray();

        return new DocumentContent(
            units,
            warnings);
    }

    private static DocumentContent CreateTableContent(
        params DocumentContentField[] fields)
    {
        return new DocumentContent(
        [
            CreateTableRow(
                0,
                fields)
        ]);
    }

    private static DocumentContentUnit CreateTextUnit(
        int order,
        string text)
    {
        return new DocumentContentUnit(
            order,
            DocumentContentUnitKind.Text,
            text);
    }

    private static DocumentContentUnit CreateTableRow(
        int order,
        params DocumentContentField[] fields)
    {
        return new DocumentContentUnit(
            order,
            DocumentContentUnitKind.TableRow,
            fields: fields);
    }

    private static DocumentContentField CreateField(
        string name,
        string value)
    {
        return new DocumentContentField(
            name,
            value);
    }
}
