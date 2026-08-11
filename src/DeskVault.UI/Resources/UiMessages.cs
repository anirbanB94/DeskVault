namespace DeskVault.UI.Resources;

public static class UiMessages
{
    public const string RemoveDocumentTitle =
        "Remove Document";

    public static string ConfirmRemoveDocument(
        string fileName)
    {
        return
            $"Are you sure you want to remove '{fileName}'?\n\n" +
            "This will permanently delete the encrypted local copy " +
            "from DeskVault. This action cannot be undone.";
    }

    public const string DocumentRemovedTitle =
        "Document Removed";

    public const string RemoveFailedTitle =
        "Remove Failed";

    public const string UnableToRemoveDocument =
        "An unexpected error occurred while removing the document.";
}
