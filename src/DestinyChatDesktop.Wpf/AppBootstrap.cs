using System;
using System.IO;
using DestinyChatDesktop.Embedded;

namespace DestinyChatDesktop.Wpf;

internal static class AppBootstrap
{
    internal sealed class StartupContext
    {
        internal required string StorageRoot { get; init; }
        internal required string StatePath { get; init; }
        internal required string SelfTestResultPath { get; init; }
        internal required AppConfig Config { get; init; }
        internal required AppState State { get; init; }
        internal required bool RunSplitSelfTest { get; init; }
        internal required bool RunToolbarSelfTest { get; init; }
        internal required bool RunDualSelfTest { get; init; }
        internal required bool RunBigscreenGeometrySelfTest { get; init; }
        internal required bool RunStreamChatPanelSelfTest { get; init; }
        internal required bool RunEmbedSelfTest { get; init; }
    }

    internal static StartupContext CreateContext()
    {
        string[] args = Environment.GetCommandLineArgs();

        string installRoot = AppDomain.CurrentDomain.BaseDirectory;
        string storageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DestinyChatDesktop");
        Directory.CreateDirectory(storageRoot);

        string configPath = Path.Combine(installRoot, "appsettings.json");
        string statePath = Path.Combine(storageRoot, "state.json");
        string selfTestResultPath = Path.Combine(storageRoot, "split-self-test.json");

        AppConfig config = AppConfig.Load(configPath);
        AppState state = AppState.Load(statePath);
        bool runSplitSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-split", StringComparison.OrdinalIgnoreCase));
        bool runToolbarSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-toolbar", StringComparison.OrdinalIgnoreCase));
        bool runDualSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-dual", StringComparison.OrdinalIgnoreCase));
        bool runBigscreenGeometrySelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-bigscreen-geometry", StringComparison.OrdinalIgnoreCase));
        bool runStreamChatPanelSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-stream-chat-panel", StringComparison.OrdinalIgnoreCase));
        bool runEmbedSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-embeds", StringComparison.OrdinalIgnoreCase));

        if ((runSplitSelfTest || runDualSelfTest || runBigscreenGeometrySelfTest || runStreamChatPanelSelfTest || runEmbedSelfTest) &&
            File.Exists(selfTestResultPath))
        {
            File.Delete(selfTestResultPath);
        }

        return new StartupContext
        {
            StorageRoot = storageRoot,
            StatePath = statePath,
            SelfTestResultPath = selfTestResultPath,
            Config = config,
            State = state,
            RunSplitSelfTest = runSplitSelfTest,
            RunToolbarSelfTest = runToolbarSelfTest,
            RunDualSelfTest = runDualSelfTest,
            RunBigscreenGeometrySelfTest = runBigscreenGeometrySelfTest,
            RunStreamChatPanelSelfTest = runStreamChatPanelSelfTest,
            RunEmbedSelfTest = runEmbedSelfTest
        };
    }
}
