using DeskVault.Application.Interfaces;

namespace DeskVault.UI.Forms;

public partial class MainForm : Form
{
    private readonly IApplicationInfoService _applicationInfo;

    public MainForm(IApplicationInfoService applicationInfo)
    {
        InitializeComponent();

        _applicationInfo = applicationInfo;

        Text = $"{_applicationInfo.ApplicationName} v{_applicationInfo.Version}";
    }
}
