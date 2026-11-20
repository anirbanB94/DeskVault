using DeskVault.UI.Resources;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;

namespace DeskVault.UI.Forms;

public partial class WorkspaceViewForm : Form, IWorkspacePresentationView
{
    private readonly Dictionary<Guid, TabPage> _documentTabs = [];
    private readonly ToolStripMenuItem _documentInformationMenuItem = new();
    private readonly ToolStripSeparator _temporaryMenuSeparator = new();

    private bool _synchronizingDocumentSelection;
    private bool _synchronizingNavigationMode;
    private bool _isTemporaryPresentation;
    private Guid? _sidebarDocumentId;
    private readonly int _uiThreadId;
    private readonly SynchronizationContext _uiSynchronizationContext;

    public WorkspaceViewForm()
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
        _uiSynchronizationContext =
            SynchronizationContext.Current ??
            new WindowsFormsSynchronizationContext();

        InitializeComponent();

        Text = UiMessages.DeskVaultWorkspaceTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 700);

        _documentInformationMenuItem.Name =
            "documentInformationMenuItem";

        _documentInformationMenuItem.Text =
            UiMessages.DocumentInformation;

        _temporaryMenuSeparator.Name =
            "temporaryMenuSeparator";

        workspaceContextMenu.Items.Insert(
            0,
            _documentInformationMenuItem);

        _documentInformationMenuItem.Click +=
            documentInformationMenuItem_Click;

        tabsMenuItem.Click += tabsMenuItem_Click;
        sidebarMenuItem.Click += sidebarMenuItem_Click;
        closeWorkspaceMenuItem.Click += closeWorkspaceMenuItem_Click;
        addDocumentsMenuItem.Click += addDocumentsMenuItem_Click;
        removeDocumentsMenuItem.Click += removeDocumentsMenuItem_Click;
        updateWorkspaceDetailsMenuItem.Click += updateWorkspaceDetailsMenuItem_Click;
        deleteWorkspaceMenuItem.Click += deleteWorkspaceMenuItem_Click;
        aiButton.Click += aiButton_Click;

        ConfigurePersistentPresentationMenu();

        ApplyTabsNavigationMode();
    }

    public event EventHandler<DocumentActivatedEventArgs> DocumentActivated = null!;

    public event EventHandler<NavigationModeChangedEventArgs> NavigationModeChanged = null!;

    public event EventHandler AddDocumentsRequested = null!;

    public event EventHandler RemoveDocumentsRequested = null!;

    public event EventHandler RemoveDocumentRequested = null!;

    public event EventHandler UpdateWorkspaceDetailsRequested = null!;

    public event EventHandler SaveAsWorkspaceRequested = null!;

    public event EventHandler DocumentInformationRequested = null!;

    public event EventHandler DeleteWorkspaceRequested = null!;

    public event EventHandler CloseWorkspaceRequested = null!;

    public event EventHandler CloseDocumentRequested = null!;

    public void SetWorkspaceIdentity(
        string workspaceName,
        string? description)
    {
        _isTemporaryPresentation = false;

        workspaceTitleLabel.Text =
            string.IsNullOrWhiteSpace(workspaceName)
                ? UiMessages.Workspace
                : workspaceName;

        workspaceMetadataLabel.Text =
            description ?? string.Empty;

        backButton.Text =
            UiMessages.BackToWorkspaces;

        ConfigurePersistentPresentationMenu();

        tabsNavigationButton.Visible = true;
        sidebarNavigationButton.Visible = true;

        ApplyTabsNavigationMode();
    }

    public void SetTemporaryDocumentPresentation(
        string documentFileName)
    {
        if (string.IsNullOrWhiteSpace(documentFileName))
        {
            throw new ArgumentException(
                "Document file name is required.",
                nameof(documentFileName));
        }

        _isTemporaryPresentation = true;

        workspaceTitleLabel.Text =
            documentFileName;

        workspaceMetadataLabel.Text =
            UiMessages.TemporaryWorkspace;

        backButton.Text =
            UiMessages.BackToDocuments;

        ConfigureTemporaryPresentationMenu();

        tabsNavigationButton.Visible = false;
        sidebarNavigationButton.Visible = false;

        ApplyTabsNavigationMode();
    }

    public void SetWorkspaceDocuments(
        IReadOnlyDictionary<Guid, string> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        documentListBox.Items.Clear();

        foreach ((Guid documentId, string fileName) in documents)
        {
            if (documentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            documentListBox.Items.Add(
                new WorkspaceDocumentListItem(
                    documentId,
                    fileName));
        }

        UpdateEmptyStateVisibility();
    }


    public void PostToUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsDisposed || Disposing)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            action();
            return;
        }

        if (IsHandleCreated)
        {
            try
            {
                BeginInvoke(action);
                return;
            }
            catch (InvalidOperationException) when (IsDisposed || Disposing)
            {
                return;
            }
            catch (InvalidOperationException) when (!IsHandleCreated)
            {
                // Fall back to the synchronization context captured by this form.
            }
        }

        _uiSynchronizationContext.Post(
            _ =>
            {
                if (!IsDisposed && !Disposing)
                {
                    action();
                }
            },
            null);
    }

    public void ClearPresentation()
    {
        _synchronizingDocumentSelection = true;
        _synchronizingNavigationMode = true;

        try
        {
            workspaceContextMenu.Close(
                ToolStripDropDownCloseReason.CloseCalled);

            documentListBox.Items.Clear();

            TabPage[] tabPages =
                [.. documentTabControl.TabPages.Cast<TabPage>()];

            // Detach every hosted document Form from its tab page.
            // These Forms remain owned by their WorkspaceDocumentPresentation.
            foreach (TabPage tabPage in tabPages)
            {
                foreach (Form documentForm in
                         tabPage.Controls
                             .OfType<Form>()
                             .ToArray())
                {
                    tabPage.Controls.Remove(documentForm);
                }
            }

            // Detach the currently sidebar-hosted document Form(s).
            foreach (Form documentForm in
                     documentSidebarContentPanel.Controls
                         .OfType<Form>()
                         .ToArray())
            {
                documentSidebarContentPanel.Controls.Remove(
                    documentForm);
            }

            // All hosted document Forms are now detached.
            // Workspace-owned tab pages can safely be disposed.
            documentTabControl.TabPages.Clear();

            foreach (TabPage tabPage in tabPages)
            {
                tabPage.Dispose();
            }

            // Dispose only remaining workspace-owned sidebar controls.
            foreach (Control control in
                     documentSidebarContentPanel.Controls
                         .Cast<Control>()
                         .ToArray())
            {
                documentSidebarContentPanel.Controls.Remove(
                    control);

                control.Dispose();
            }

            _documentTabs.Clear();
            _sidebarDocumentId = null;

            workspaceTitleLabel.Text =
                UiMessages.Workspace;

            workspaceMetadataLabel.Text =
                string.Empty;

            backButton.Text =
                UiMessages.BackToWorkspaces;

            _isTemporaryPresentation = false;

            aiConversationTextBox.Clear();
            aiInputTextBox.Clear();

            aiPanel.Visible = false;
            aiButton.Text = UiMessages.AiButton;

            ConfigurePersistentPresentationMenu();

            tabsNavigationButton.Visible = true;
            sidebarNavigationButton.Visible = true;

            ApplyTabsNavigationMode();
        }
        finally
        {
            _synchronizingNavigationMode = false;
            _synchronizingDocumentSelection = false;
        }
    }

    public void AddDocumentPresentation(
        Guid documentId,
        string? documentFileName,
        Form documentForm)
    {
        ArgumentNullException.ThrowIfNull(documentForm);

        if (_documentTabs.ContainsKey(documentId))
        {
            ActivateDocumentPresentation(
                documentId);

            return;
        }

        TabPage? tabPage = null;

        try
        {
            PrepareDocumentView(documentForm);

            string fileName =
                documentFileName ??
                UiMessages.DocumentWorkspaceFallbackName;

            tabPage = new TabPage
            {
                Name = $"documentTab_{documentId:N}",
                Text = fileName,
                UseVisualStyleBackColor = true
            };

            tabPage.Controls.Add(documentForm);

            _documentTabs.Add(
                documentId,
                tabPage);

            bool wasSynchronizingDocumentSelection =
                _synchronizingDocumentSelection;

            _synchronizingDocumentSelection = true;

            try
            {
                documentTabControl.TabPages.Add(tabPage);
            }
            finally
            {
                _synchronizingDocumentSelection =
                    wasSynchronizingDocumentSelection;
            }

            UpdateEmptyStateVisibility();
        }
        catch
        {
            // Remove only the registry entry created by this attempt.
            if (tabPage is not null &&
                _documentTabs.TryGetValue(
                    documentId,
                    out TabPage? registeredTabPage) &&
                ReferenceEquals(registeredTabPage, tabPage))
            {
                _documentTabs.Remove(
                    documentId);
            }

            if (tabPage is not null)
            {
                documentTabControl.TabPages.Remove(
                    tabPage);
            }

            // Detach the document Form before disposing its container.
            if (documentForm.Parent is Control parent)
            {
                parent.Controls.Remove(documentForm);
            }

            // Never dispose a container while it still owns the Form.
            if (tabPage is not null &&
                documentForm.Parent is null)
            {
                tabPage.Dispose();
            }

            throw;
        }
    }

    public bool RemoveDocumentPresentation(Guid documentId)
    {
        if (!_documentTabs.Remove(
                documentId,
                out TabPage? tabPage))
        {
            return false;
        }

        Form? documentForm =
            tabPage.Controls
                .OfType<Form>()
                .FirstOrDefault();

        if (_sidebarDocumentId == documentId)
        {
            documentForm =
                documentSidebarContentPanel.Controls
                    .OfType<Form>()
                    .FirstOrDefault();

            _sidebarDocumentId = null;
        }

        // Detach the hosted document Form from its actual parent.
        // WorkspaceViewForm must never dispose the document Form.
        if (documentForm?.Parent is Control parent)
        {
            parent.Controls.Remove(documentForm);
        }

        bool wasSynchronizingDocumentSelection =
            _synchronizingDocumentSelection;

        _synchronizingDocumentSelection = true;

        try
        {
            documentTabControl.TabPages.Remove(
                tabPage);
        }
        finally
        {
            _synchronizingDocumentSelection =
                wasSynchronizingDocumentSelection;
        }

        // Only the workspace-owned tab container is disposed here.
        tabPage.Dispose();

        UpdateEmptyStateVisibility();

        return true;
    }

    public bool ActivateDocumentPresentation(Guid documentId)
    {
        if (!_documentTabs.TryGetValue(
                documentId,
                out TabPage? tabPage))
        {
            return false;
        }

        _synchronizingDocumentSelection = true;

        try
        {
            documentTabControl.SelectedTab = tabPage;

            for (int index = 0;
                 index < documentListBox.Items.Count;
                 index++)
            {
                if (documentListBox.Items[index]
                    is WorkspaceDocumentListItem item &&
                    item.DocumentId == documentId)
                {
                    documentListBox.SelectedIndex = index;
                    break;
                }
            }
        }
        finally
        {
            _synchronizingDocumentSelection = false;
        }

        if (documentSidebarPanel.Visible)
        {
            ShowDocumentInSidebar(
                documentId);
        }
        else
        {
            ShowDocumentInTabs(
                documentId);
        }

        return true;
    }

    public void SetNavigationMode(
        WorkspacePresentationNavigationMode navigationMode)
    {
        if (_isTemporaryPresentation)
        {
            return;
        }

        _synchronizingNavigationMode = true;

        try
        {
            if (navigationMode ==
                WorkspacePresentationNavigationMode.Tabs)
            {
                ApplyTabsNavigationMode();
            }
            else
            {
                ApplySidebarNavigationMode();
            }
        }
        finally
        {
            _synchronizingNavigationMode = false;
        }
    }

    public void ActivateWorkspace()
    {
        Show();
        BringToFront();
        Activate();
    }

    public void CloseWorkspace()
    {
        Hide();
    }

    private void ConfigurePersistentPresentationMenu()
    {
        RestorePersistentMenuOrder();

        _documentInformationMenuItem.Visible = false;

        addDocumentsMenuItem.Visible = true;
        removeDocumentsMenuItem.Visible = true;
        updateWorkspaceDetailsMenuItem.Visible = true;
        deleteWorkspaceMenuItem.Visible = true;

        workspaceMenuSeparator.Visible = true;

        viewMenuItem.Visible = true;
        viewMenuSeparator.Visible = true;

        closeWorkspaceMenuItem.Visible = true;

        removeDocumentsMenuItem.Text =
            UiMessages.RemoveDocumentsFromWorkspace;

        updateWorkspaceDetailsMenuItem.Text =
            UiMessages.UpdateWorkspaceDetailsTitle;

        closeWorkspaceMenuItem.Text =
            UiMessages.CloseWorkspace;
    }

    private void ConfigureTemporaryPresentationMenu()
    {
        RestoreTemporaryMenuOrder();

        _documentInformationMenuItem.Visible = true;

        addDocumentsMenuItem.Visible = false;
        removeDocumentsMenuItem.Visible = true;
        updateWorkspaceDetailsMenuItem.Visible = true;
        deleteWorkspaceMenuItem.Visible = false;

        workspaceMenuSeparator.Visible = false;

        viewMenuItem.Visible = false;
        viewMenuSeparator.Visible = false;

        closeWorkspaceMenuItem.Visible = true;

        removeDocumentsMenuItem.Text =
            UiMessages.RemoveDocument;

        updateWorkspaceDetailsMenuItem.Text =
            UiMessages.SaveAsWorkspace;

        closeWorkspaceMenuItem.Text =
            UiMessages.CloseDocument;
    }

    private void RestorePersistentMenuOrder()
    {
        workspaceContextMenu.Items.Clear();

        workspaceContextMenu.Items.AddRange(
        [
            _documentInformationMenuItem,
            addDocumentsMenuItem,
            removeDocumentsMenuItem,
            updateWorkspaceDetailsMenuItem,
            deleteWorkspaceMenuItem,
            workspaceMenuSeparator,
            viewMenuItem,
            viewMenuSeparator,
            closeWorkspaceMenuItem
        ]);
    }

    private void RestoreTemporaryMenuOrder()
    {
        workspaceContextMenu.Items.Clear();

        workspaceContextMenu.Items.AddRange(
        [
            _documentInformationMenuItem,
            updateWorkspaceDetailsMenuItem,
            _temporaryMenuSeparator,
            removeDocumentsMenuItem,
            closeWorkspaceMenuItem
        ]);
    }

    private static void PrepareDocumentView(
        Form documentForm)
    {
        documentForm.TopLevel = false;
        documentForm.FormBorderStyle = FormBorderStyle.None;
        documentForm.Dock = DockStyle.Fill;
        documentForm.ShowInTaskbar = false;
        documentForm.Visible = false;
        documentForm.Show();
    }

    private void ShowDocumentInTabs(Guid documentId)
    {
        if (!_documentTabs.TryGetValue(
                documentId,
                out TabPage? requestedTab))
        {
            return;
        }

        MoveSidebarDocumentBackToTab();

        Form? requestedDocumentForm =
            requestedTab.Controls
                .OfType<Form>()
                .FirstOrDefault();

        if (requestedDocumentForm is null)
        {
            return;
        }

        foreach (TabPage currentTab in
                 documentTabControl.TabPages)
        {
            foreach (Control control in currentTab.Controls)
            {
                control.Visible =
                    ReferenceEquals(
                        currentTab,
                        requestedTab);
            }
        }

        requestedDocumentForm.Visible = true;
        documentTabControl.SelectedTab = requestedTab;
    }

    private void ShowDocumentInSidebar(Guid documentId)
    {
        if (!_documentTabs.TryGetValue(
                documentId,
                out TabPage? requestedTab))
        {
            return;
        }

        if (_sidebarDocumentId == documentId)
        {
            return;
        }

        MoveSidebarDocumentBackToTab();

        Form? requestedDocumentForm =
            requestedTab.Controls
                .OfType<Form>()
                .FirstOrDefault();

        if (requestedDocumentForm is null)
        {
            return;
        }

        requestedTab.Controls.Remove(
            requestedDocumentForm);

        documentSidebarContentPanel.Controls.Add(
            requestedDocumentForm);

        requestedDocumentForm.Dock =
            DockStyle.Fill;

        requestedDocumentForm.Visible = true;

        _sidebarDocumentId = documentId;

        foreach (TabPage currentTab in
                 documentTabControl.TabPages)
        {
            foreach (Control control in currentTab.Controls)
            {
                control.Visible = false;
            }
        }
    }

    private void MoveSidebarDocumentBackToTab()
    {
        if (_sidebarDocumentId is not Guid sidebarDocumentId)
        {
            return;
        }

        if (!_documentTabs.TryGetValue(
                sidebarDocumentId,
                out TabPage? sidebarTab))
        {
            _sidebarDocumentId = null;
            return;
        }

        Form? sidebarDocumentForm =
            documentSidebarContentPanel.Controls
                .OfType<Form>()
                .FirstOrDefault();

        if (sidebarDocumentForm is null)
        {
            _sidebarDocumentId = null;
            return;
        }

        documentSidebarContentPanel.Controls.Remove(
            sidebarDocumentForm);

        sidebarTab.Controls.Add(
            sidebarDocumentForm);

        sidebarDocumentForm.Dock =
            DockStyle.Fill;

        sidebarDocumentForm.Visible = true;

        _sidebarDocumentId = null;
    }

    private void ApplyTabsNavigationMode()
    {
        MoveSidebarDocumentBackToTab();

        tabsMenuItem.Checked = true;
        sidebarMenuItem.Checked = false;

        documentTabsPanel.Visible = true;
        documentSidebarPanel.Visible = false;

        if (documentTabControl.SelectedTab is TabPage selectedTab)
        {
            foreach (TabPage currentTab in
                     documentTabControl.TabPages)
            {
                foreach (Control control in currentTab.Controls)
                {
                    control.Visible =
                        ReferenceEquals(
                            currentTab,
                            selectedTab);
                }
            }
        }

        UpdateEmptyStateVisibility();
    }

    private void ApplySidebarNavigationMode()
    {
        tabsMenuItem.Checked = false;
        sidebarMenuItem.Checked = true;

        documentTabsPanel.Visible = false;
        documentSidebarPanel.Visible = true;

        if (documentTabControl.SelectedTab is TabPage selectedTab &&
            _documentTabs
                .FirstOrDefault(
                    entry => ReferenceEquals(
                        entry.Value,
                        selectedTab))
                .Key is Guid selectedDocumentId)
        {
            ShowDocumentInSidebar(
                selectedDocumentId);
        }

        UpdateEmptyStateVisibility();
    }

    private void backButton_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            CloseDocumentRequested?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        Hide();
    }

    private void tabsNavigationButton_Click(
        object? sender,
        EventArgs e)
    {
        ApplyTabsNavigationMode();

        if (!_synchronizingNavigationMode)
        {
            NavigationModeChanged?.Invoke(
                this,
                new NavigationModeChangedEventArgs(
                    WorkspacePresentationNavigationMode.Tabs));
        }
    }

    private void sidebarNavigationButton_Click(
        object? sender,
        EventArgs e)
    {
        ApplySidebarNavigationMode();

        if (!_synchronizingNavigationMode)
        {
            NavigationModeChanged?.Invoke(
                this,
                new NavigationModeChangedEventArgs(
                    WorkspacePresentationNavigationMode.Sidebar));
        }
    }

    private void tabsMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            return;
        }

        SetNavigationMode(
            WorkspacePresentationNavigationMode.Tabs);

        NavigationModeChanged?.Invoke(
            this,
            new NavigationModeChangedEventArgs(
                WorkspacePresentationNavigationMode.Tabs));
    }

    private void sidebarMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            return;
        }

        SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        NavigationModeChanged?.Invoke(
            this,
            new NavigationModeChangedEventArgs(
                WorkspacePresentationNavigationMode.Sidebar));
    }

    private void workspaceMenuButton_Click(
        object? sender,
        EventArgs e)
    {
        workspaceContextMenu.Show(
            workspaceMenuButton,
            new Point(
                0,
                workspaceMenuButton.Height));
    }

    private void addDocumentsMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        AddDocumentsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void removeDocumentsMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            RemoveDocumentRequested?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        RemoveDocumentsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void updateWorkspaceDetailsMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            SaveAsWorkspaceRequested?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        UpdateWorkspaceDetailsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void documentInformationMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        DocumentInformationRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void deleteWorkspaceMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        DeleteWorkspaceRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void closeWorkspaceMenuItem_Click(
        object? sender,
        EventArgs e)
    {
        if (_isTemporaryPresentation)
        {
            CloseDocumentRequested?.Invoke(
                this,
                EventArgs.Empty);

            return;
        }

        CloseWorkspaceRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void aiButton_Click(
        object? sender,
        EventArgs e)
    {
        aiPanel.Visible = !aiPanel.Visible;
        aiButton.Text = UiMessages.AiButton;
    }

    private void documentTabControl_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_synchronizingDocumentSelection ||
            documentTabControl.SelectedTab is not TabPage selectedTab)
        {
            return;
        }

        Guid? documentId =
            _documentTabs
                .FirstOrDefault(
                    entry => ReferenceEquals(
                        entry.Value,
                        selectedTab))
                .Key;

        if (documentId is Guid selectedDocumentId)
        {
            ActivateDocumentPresentation(
                selectedDocumentId);

            DocumentActivated?.Invoke(
                this,
                new DocumentActivatedEventArgs(
                    selectedDocumentId));
        }
    }

    private void documentListBox_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_synchronizingDocumentSelection)
        {
            return;
        }

        if (documentListBox.SelectedItem
            is WorkspaceDocumentListItem item)
        {
            ActivateDocumentPresentation(
                item.DocumentId);

            DocumentActivated?.Invoke(
                this,
                new DocumentActivatedEventArgs(
                    item.DocumentId));
        }
    }

    protected override void OnFormClosing(
        FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;

            if (_isTemporaryPresentation)
            {
                CloseDocumentRequested?.Invoke(
                    this,
                    EventArgs.Empty);

                return;
            }

            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private sealed record WorkspaceDocumentListItem(
        Guid DocumentId,
        string FileName)
    {
        public override string ToString() =>
            FileName;
    }

    private void UpdateEmptyStateVisibility()
    {
        bool hasWorkspaceDocuments =
            documentListBox.Items.Count > 0;

        bool hasPresentedDocuments =
            _documentTabs.Count > 0;

        documentTabControl.Visible =
            documentTabsPanel.Visible &&
            hasPresentedDocuments;

        tabsEmptyStateLabel.Visible =
            documentTabsPanel.Visible &&
            !hasPresentedDocuments;

        sidebarEmptyStateLabel.Visible =
            documentSidebarPanel.Visible &&
            !hasWorkspaceDocuments;
    }
}
