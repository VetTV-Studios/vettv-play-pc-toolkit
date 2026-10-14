namespace VetTV.PlayPC.App;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsWindows()) DataContext = new MainViewModel();
    }
}
