namespace VetTV.PlayPC.App;

public partial class App : System.Windows.Application
{
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (OperatingSystem.IsWindows() && MainWindow?.DataContext is MainViewModel vm) vm.Developer.StopLogcat();
        base.OnExit(e);
    }
}
