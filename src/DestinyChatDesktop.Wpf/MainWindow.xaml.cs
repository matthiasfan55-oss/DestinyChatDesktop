using System.Windows;
using System.Windows.Forms;
using DestinyChatDesktop.Embedded;

namespace DestinyChatDesktop.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppBootstrap.StartupContext ctx = AppBootstrap.CreateContext();

        if (ctx.State.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            Left = ctx.State.X;
            Top = ctx.State.Y;
            Width = ctx.State.Width;
            Height = ctx.State.Height;
        }

        MainForm mainForm = new MainForm(
            ctx.StorageRoot,
            ctx.StatePath,
            ctx.Config,
            ctx.State,
            ctx.RunSplitSelfTest,
            ctx.RunToolbarSelfTest,
            ctx.RunDualSelfTest,
            ctx.RunBigscreenGeometrySelfTest,
            ctx.RunStreamChatPanelSelfTest,
            ctx.RunEmbedSelfTest,
            ctx.SelfTestResultPath,
            hostedInWpfShell: true);

        mainForm.Dock = DockStyle.Fill;
        mainForm.FormClosed += (_, _) =>
        {
            Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        };

        ShellHost.Child = mainForm;
    }
}
