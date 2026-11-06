#nullable enable

using System.Drawing;
using System.Windows.Forms;

namespace DeskVault.UI.Forms;

partial class SessionBoundMessageDialog
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer? components = null;

    /// <summary>
    /// Releases the resources used by the dialog.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();

            _iconFont?.Dispose();
            _iconFont = null;
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        ClientSize = new Size(460, 170);
        Name = nameof(SessionBoundMessageDialog);
        Text = nameof(SessionBoundMessageDialog);

        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
