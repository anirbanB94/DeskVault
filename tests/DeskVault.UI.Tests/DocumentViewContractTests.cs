using DeskVault.UI.Forms;
using DeskVault.UI.Views;
using System.Reflection;

namespace DeskVault.UI.Tests;

public sealed class DocumentViewContractTests
{
    [Fact]
    public void DocumentView_ExposesOnlyDocumentInteractionEvents()
    {
        // Arrange
        string[] expectedEvents =
        [
            nameof(IDocumentView.OpenExternallyRequested),
            nameof(IDocumentView.DocumentInformationRequested),
            nameof(IDocumentView.RemoveDocumentRequested)
        ];

        // Act
        string[] actualEvents =
            typeof(IDocumentView)
                .GetEvents(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Select(documentEvent => documentEvent.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        // Assert
        Assert.Equal(
            expectedEvents.OrderBy(
                name => name,
                StringComparer.Ordinal),
            actualEvents);
    }

    [Fact]
    public void DocumentView_ExposesOnlyRequiredDocumentPresentationOperations()
    {
        // Arrange
        string[] expectedMethods =
        [
            nameof(IDocumentView.ShowDocumentAsync),
            nameof(IDocumentView.ShowUnsupportedPreview),
            nameof(IDocumentView.ShowDocumentInformation),
            nameof(IDocumentView.ConfirmRemoval),
            nameof(IDocumentView.ClearPresentation),
            nameof(IDocumentView.PostToUi),
            nameof(IDocumentView.ShowError)
        ];

        // Act
        string[] actualMethods =
            typeof(IDocumentView)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

        // Assert
        Assert.Equal(
            expectedMethods.OrderBy(
                name => name,
                StringComparer.Ordinal),
            actualMethods);
    }

    [Fact]
    public void DocumentViewForm_ImplementsDocumentOnlyContract()
    {
        // Arrange
        Type documentViewType = typeof(IDocumentView);
        Type documentFormType = typeof(DocumentViewForm);

        // Act
        bool implementsDocumentView =
            documentViewType.IsAssignableFrom(documentFormType);

        // Assert
        Assert.True(implementsDocumentView);
    }
}
