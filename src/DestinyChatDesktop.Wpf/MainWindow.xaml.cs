using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
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
            // Persisted bounds are pixel coordinates from WinForms; convert to DIP for WPF.
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            double dipW = ctx.State.Width / dpi.DpiScaleX;
            double dipH = ctx.State.Height / dpi.DpiScaleY;
            Width = dipW;
            Height = dipH;

            // Only restore position if the saved bounds land on an active screen.
            // If the monitor is disconnected or this is a first run (X=0, Y=0 defaults),
            // center on the primary work area instead.
            var pixelBounds = new Rectangle(ctx.State.X, ctx.State.Y, ctx.State.Width, ctx.State.Height);
            bool onScreen = ctx.State.LoadedFromDisk &&
                Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(pixelBounds));
            if (onScreen)
            {
                Left = ctx.State.X / dpi.DpiScaleX;
                Top = ctx.State.Y / dpi.DpiScaleY;
            }
            else
            {
                Left = (SystemParameters.WorkArea.Width - dipW) / 2;
                Top = (SystemParameters.WorkArea.Height - dipH) / 2;
            }
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
