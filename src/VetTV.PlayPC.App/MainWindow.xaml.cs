using VetTV.PlayPC.Core;

namespace VetTV.PlayPC.App;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        Footer.Text = ToolkitInfo.Disclaimer;
    }
}
