using DeskVault.UI.Resources;

namespace DeskVault.UI.Forms;

public sealed partial class SessionBoundMessageDialog : Form
{
    private Font? _iconFont;

    public SessionBoundMessageDialog(
        string message,
        string title,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        if (buttons is not MessageBoxButtons.OK and
            not MessageBoxButtons.YesNo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buttons),
                "Only OK and Yes/No dialogs are supported.");
        }

        InitializeComponent();

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(430, 150);
        MaximumSize = new Size(700, 700);

        BuildDialogContent(
            message,
            buttons,
            icon);
    }

    private void BuildDialogContent(
        string message,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        TableLayoutPanel layout = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(18),
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Absolute,
                54F));

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        _iconFont = new Font(
            Font.FontFamily,
            25F,
            FontStyle.Bold);

        Label iconLabel = new()
        {
            Name = "dialogIconLabel",
            Text = GetIconGlyph(icon),
            ForeColor = GetIconColor(icon),
            Font = _iconFont,
            AutoSize = true,
            TextAlign = ContentAlignment.TopCenter,
            Margin = new Padding(0, 4, 12, 0)
        };

        Label messageLabel = new()
        {
            Name = "messageLabel",
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 6, 0, 6)
        };

        layout.Controls.Add(iconLabel, 0, 0);
        layout.Controls.Add(messageLabel, 1, 0);

        FlowLayoutPanel buttonPanel = new()
        {
            Name = "buttonPanel",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 16, 0, 0)
        };

        if (buttons == MessageBoxButtons.YesNo)
        {
            Button noButton = CreateButton(
                UiMessages.NoButtonText,
                DialogResult.No);

            Button yesButton = CreateButton(
                UiMessages.YesButtonText,
                DialogResult.Yes);

            buttonPanel.Controls.Add(noButton);
            buttonPanel.Controls.Add(yesButton);

            // A confirmation must never default to Yes.
            AcceptButton = noButton;
            CancelButton = noButton;
        }
        else
        {
            Button okButton = CreateButton(
                UiMessages.OkButtonText,
                DialogResult.OK);

            buttonPanel.Controls.Add(okButton);

            AcceptButton = okButton;
            CancelButton = okButton;
        }

        layout.Controls.Add(buttonPanel, 0, 1);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);
    }

    private static Button CreateButton(
        string text,
        DialogResult dialogResult)
    {
        return new Button
        {
            Text = text,
            DialogResult = dialogResult,
            AutoSize = true,
            MinimumSize = new Size(88, 32),
            Margin = new Padding(6, 0, 0, 0)
        };
    }

    private static string GetIconGlyph(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Information => "i",
            MessageBoxIcon.Warning => "!",
            MessageBoxIcon.Error => "×",
            MessageBoxIcon.Question => "?",
            _ => string.Empty
        };
    }

    private static Color GetIconColor(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Information => Color.RoyalBlue,
            MessageBoxIcon.Warning => Color.DarkGoldenrod,
            MessageBoxIcon.Error => Color.Firebrick,
            MessageBoxIcon.Question => Color.RoyalBlue,
            _ => SystemColors.ControlText
        };
    }
}
