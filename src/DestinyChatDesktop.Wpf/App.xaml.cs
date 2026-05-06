using System;
using System.Collections.Generic;
using System.Windows;
using DestinyChatDesktop.Embedded;

namespace DestinyChatDesktop.Wpf;

public partial class App : System.Windows.Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.ThreadException += delegate(object? uiSender, System.Threading.ThreadExceptionEventArgs args)
        {
            AgentDebugLog.Write(
                "pre-fix",
                "N1",
                "App.xaml.cs:UI thread exception",
                "UI thread exception",
                new Dictionary<string, object>
                {
                    { "error", args.Exception?.Message ?? string.Empty }
                });
        };

        AppDomain.CurrentDomain.UnhandledException += delegate(object? appSender, UnhandledExceptionEventArgs args)
        {
            Exception? ex = args.ExceptionObject as Exception;
            AgentDebugLog.Write(
                "pre-fix",
                "N1",
                "App.xaml.cs:AppDomain unhandled exception",
                "AppDomain unhandled exception",
                new Dictionary<string, object>
                {
                    { "isTerminating", args.IsTerminating },
                    { "error", ex?.Message ?? Convert.ToString(args.ExceptionObject) ?? string.Empty }
                });
        };
    }
}
