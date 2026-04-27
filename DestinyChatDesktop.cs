using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DestinyChatDesktop
{
    internal static class AgentDebugLog
    {
        private static readonly object Sync = new object();
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly bool Enabled = IsEnabled();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DestinyChatDesktop",
            "debug-a0e27b.log");

        public static void Write(string runId, string hypothesisId, string location, string message, Dictionary<string, object> data)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "sessionId", "a0e27b" },
                    { "runId", runId ?? string.Empty },
                    { "hypothesisId", hypothesisId ?? string.Empty },
                    { "location", location ?? string.Empty },
                    { "message", message ?? string.Empty },
                    { "data", data ?? new Dictionary<string, object>() },
                    { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { "id", Guid.NewGuid().ToString("N") }
                };

                string line = Serializer.Serialize(payload);
                lock (Sync)
                {
                    string dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                try { Debug.WriteLine("AgentDebugLog.Write failed: " + ex); } catch { }
            }
        }

        private static bool IsEnabled()
        {
            string env = Environment.GetEnvironmentVariable("DESTINY_CHAT_DEBUG_LOG");
            if (!string.IsNullOrWhiteSpace(env) &&
                (env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                 env.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(args[i]) &&
                    args[i].StartsWith("--self-test", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N1",
                    "DestinyChatDesktop.cs:57",
                    "UI thread exception",
                    new Dictionary<string, object>
                    {
                        { "error", e != null && e.Exception != null ? e.Exception.Message : string.Empty }
                    });
                // #endregion
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e != null ? e.ExceptionObject as Exception : null;
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N1",
                    "DestinyChatDesktop.cs:72",
                    "AppDomain unhandled exception",
                    new Dictionary<string, object>
                    {
                        { "isTerminating", e != null && e.IsTerminating },
                        { "error", ex != null ? ex.Message : Convert.ToString(e != null ? e.ExceptionObject : null) }
                    });
                // #endregion
            };

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
            bool runPopoutPauseSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-popout-pause", StringComparison.OrdinalIgnoreCase));
            bool runToolbarSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-toolbar", StringComparison.OrdinalIgnoreCase));
            bool runDualSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-dual", StringComparison.OrdinalIgnoreCase));
            bool runBigscreenGeometrySelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-bigscreen-geometry", StringComparison.OrdinalIgnoreCase));
            bool runStreamChatPanelSelfTest = Array.Exists(args, arg => string.Equals(arg, "--self-test-stream-chat-panel", StringComparison.OrdinalIgnoreCase));

            if ((runSplitSelfTest || runDualSelfTest || runBigscreenGeometrySelfTest || runStreamChatPanelSelfTest) && File.Exists(selfTestResultPath))
            {
                File.Delete(selfTestResultPath);
            }

            Application.Run(new MainForm(storageRoot, statePath, config, state, runSplitSelfTest, runPopoutPauseSelfTest, runToolbarSelfTest, runDualSelfTest, runBigscreenGeometrySelfTest, selfTestResultPath));
        }
    }

    public sealed class AppConfig
    {
        public string Title { get; set; }
        public string AppVersion { get; set; }
        public string HomeUrl { get; set; }
        public string SiteUrl { get; set; }
        public string[] AllowedHostSuffixes { get; set; }
        public bool StartPinned { get; set; }
        public bool StartMuted { get; set; }
        public double StartZoomFactor { get; set; }
        public bool CheckForUpdatesOnStartup { get; set; }
        public bool AllowPrereleaseUpdates { get; set; }
        public string UpdateRepoOwner { get; set; }
        public string UpdateRepoName { get; set; }
        public string UpdateAssetName { get; set; }

        public AppConfig()
        {
            Title = "Destiny.gg Chat Desktop";
            AppVersion = "0.1.0";
            HomeUrl = "https://www.destiny.gg/embed/chat";
            SiteUrl = "https://www.destiny.gg/";
            AllowedHostSuffixes = new[] { "destiny.gg" };
            StartPinned = false;
            StartMuted = false;
            StartZoomFactor = 1.20;
            CheckForUpdatesOnStartup = true;
            AllowPrereleaseUpdates = false;
            UpdateRepoOwner = "matthiasfan55-oss";
            UpdateRepoName = "DestinyChatDesktop";
            UpdateAssetName = "DestinyChatDesktop-portable.zip";
        }

        public static AppConfig Load(string path)
        {
            AppConfig defaults = new AppConfig();

            try
            {
                if (!File.Exists(path))
                {
                    return defaults;
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                AppConfig config = serializer.Deserialize<AppConfig>(File.ReadAllText(path));
                if (config == null)
                {
                    return defaults;
                }

                if (string.IsNullOrWhiteSpace(config.Title))
                {
                    config.Title = defaults.Title;
                }

                if (string.IsNullOrWhiteSpace(config.AppVersion))
                {
                    config.AppVersion = defaults.AppVersion;
                }

                if (string.IsNullOrWhiteSpace(config.HomeUrl))
                {
                    config.HomeUrl = defaults.HomeUrl;
                }

                if (string.IsNullOrWhiteSpace(config.SiteUrl))
                {
                    config.SiteUrl = defaults.SiteUrl;
                }

                if (config.AllowedHostSuffixes == null || config.AllowedHostSuffixes.Length == 0)
                {
                    config.AllowedHostSuffixes = defaults.AllowedHostSuffixes;
                }

                if (string.IsNullOrWhiteSpace(config.UpdateRepoOwner))
                {
                    config.UpdateRepoOwner = defaults.UpdateRepoOwner;
                }

                if (string.IsNullOrWhiteSpace(config.UpdateRepoName))
                {
                    config.UpdateRepoName = defaults.UpdateRepoName;
                }

                if (string.IsNullOrWhiteSpace(config.UpdateAssetName))
                {
                    config.UpdateAssetName = defaults.UpdateAssetName;
                }

                config.StartZoomFactor = MainForm.ClampZoom(config.StartZoomFactor);

                return config;
            }
            catch
            {
                return defaults;
            }
        }
    }

    public sealed class AppState
    {
        public bool LoadedFromDisk { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMaximized { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool Muted { get; set; }
        public string LastUrl { get; set; }
        public double ZoomFactor { get; set; }

        public AppState()
        {
            LoadedFromDisk = false;
            X = 0;
            Y = 0;
            Width = 1280;
            Height = 900;
            IsMaximized = false;
            AlwaysOnTop = false;
            Muted = false;
            LastUrl = string.Empty;
            ZoomFactor = 1.0;
        }

        public static AppState Load(string path)
        {
            AppState defaults = new AppState();

            try
            {
                if (!File.Exists(path))
                {
                    return defaults;
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                AppState state = serializer.Deserialize<AppState>(File.ReadAllText(path));
                if (state == null)
                {
                    return defaults;
                }

                state.LoadedFromDisk = true;
                if (state.Width < 900)
                {
                    state.Width = defaults.Width;
                }

                if (state.Height < 640)
                {
                    state.Height = defaults.Height;
                }

                state.ZoomFactor = MainForm.ClampZoom(state.ZoomFactor);
                if (state.LastUrl == null)
                {
                    state.LastUrl = string.Empty;
                }

                return state;
            }
            catch
            {
                return defaults;
            }
        }

        public void Save(string path)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, serializer.Serialize(this));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AppState.Save failed: " + ex);
            }
        }
    }

    public sealed class CookieState
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public bool IsHttpOnly { get; set; }
        public bool IsSecure { get; set; }
        public bool IsSession { get; set; }
        public long ExpiresUtcTicks { get; set; }
        public string SameSite { get; set; }
    }

    public sealed class BigscreenEmbedState
    {
        public string Url { get; set; }
        public string Platform { get; set; }
        public string MediaId { get; set; }
        public string DisplayText { get; set; }
        public string TooltipText { get; set; }
        public bool IsSelected { get; set; }
    }

    public sealed class MediaPopoutTarget
    {
        public string PlayerUrl { get; set; }
        public bool UseHostedVideoElement { get; set; }
    }

    public sealed class UpdateReleaseInfo
    {
        public string VersionTag { get; set; }
        public string AssetName { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleasePageUrl { get; set; }
    }

    public sealed class MainForm : Form
    {
        private const string BigscreenBarUrl = "https://www.destiny.gg/bigscreen";
        private const string DefaultTwitchParent = "www.destiny.gg";

        private readonly string _storageRoot;
        private readonly string _statePath;
        private readonly string _cookiePath;
        private readonly AppConfig _config;
        private readonly AppState _state;

        private readonly ToolStrip _toolStrip;
        private readonly ToolStripButton _backButton;
        private readonly ToolStripButton _forwardButton;
        private readonly ToolStripButton _homeButton;
        private readonly ToolStripButton _bigscreenButton;
        private readonly ToolStripButton _mediaPopoutButton;
        private readonly ToolStripButton _loginButton;
        private readonly ToolStripButton _reloadButton;
        private readonly ToolStripButton _updateButton;
        private readonly SpringToolStripTextBox _addressBox;
        private readonly ToolStripButton _goButton;
        private readonly ToolStripButton _muteButton;
        private readonly ToolStripButton _pinButton;
        private readonly ToolStripButton _zoomOutButton;
        private readonly ToolStripButton _zoomResetButton;
        private readonly ToolStripButton _zoomInButton;
        private readonly ToolStripButton _browserButton;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly Panel _bigscreenBarPanel;
        private readonly Panel _bigscreenControlsPanel;
        private readonly Button _bigscreenRefreshButton;
        private readonly Button _bigscreenCinemaButton;
        private readonly EmbedViewportPanel _bigscreenEmbedsViewport;
        private readonly WebView2 _bigscreenEmbedsUiView;
        private readonly FlowLayoutPanel _bigscreenEmbedsPanel;
        private readonly ToolTip _bigscreenToolTip;
        private readonly WebView2 _bigscreenBarView;
        private readonly WebView2 _webView;
        private readonly Panel _dualChatPanel;
        private readonly WebView2 _dualChatView;
        private readonly Label _dualChatEmptyLabel;
        private readonly Timer _sessionPersistTimer;

        private bool _browserReady;
        private bool _isFullScreen;
        private bool _isSavingCookies;
        private bool _bigscreenBarReady;
        private int _bigscreenEmbedScrollOffset;
        private List<BigscreenEmbedState> _latestBigscreenEmbeds;
        private FormBorderStyle _savedBorderStyle;
        private FormWindowState _savedWindowState;
        private Rectangle _savedBounds;
        private string _pendingNavigation;
        private CoreWebView2Environment _webViewEnvironment;
        private readonly bool _runSplitSelfTest;
        private readonly bool _runPopoutPauseSelfTest;
        private readonly bool _runToolbarSelfTest;
        private readonly bool _runDualSelfTest;
        private readonly bool _runBigscreenGeometrySelfTest;
        private readonly string _selfTestResultPath;
        private bool _splitSelfTestStarted;
        private bool _popoutPauseSelfTestStarted;
        private bool _toolbarSelfTestStarted;
        private bool _dualSelfTestStarted;
        private bool _bigscreenGeometrySelfTestStarted;
        private bool _streamChatPanelSelfTestStarted;
        private bool _suppressPageEmbedsState;
        private readonly bool _runStreamChatPanelSelfTest;
        private System.Threading.Tasks.Task _pendingSplitSelfTestTask;
        private bool _dualChatHostEnabled;
        private bool _dualChatHostAvailable;
        private string _dualChatRequestedUrl;
        private bool _dualChatNavigationInFlight;
        private string _dualChatEmptyMessage;
        private int _dualChatInputTop;
        private int _dualChatPaneLeft;
        private int _dualChatPaneTop;
        private int _dualChatPaneWidth;
        private int _dualChatPaneHeight;
        private bool _dualChatLayoutActive;
        private string _lastStreamChatSourceJson;
        private string _lastDualChatLayoutFingerprint;
        private string _activeStreamPlayerUrl;
        private string _preferredChatEmbedUrl;
        private int _bigscreenChatTopOffset;
        private int _bigscreenViewportWidth;
        private int _bigscreenViewportHeight;
        private int _embedChatLayoutViewportWidth;
        private int _embedChatLayoutViewportHeight;
        private bool _isCheckingForUpdates;
        private bool _startupUpdateCheckQueued;
        private string _lastSnipRequestId;
        private DateTime _lastSnipRequestUtc;
        private bool _treatSnipAsProbe;

        public MainForm(string storageRoot, string statePath, AppConfig config, AppState state, bool runSplitSelfTest, bool runPopoutPauseSelfTest, bool runToolbarSelfTest, bool runDualSelfTest, bool runBigscreenGeometrySelfTest, string selfTestResultPath)
        {
            _storageRoot = storageRoot;
            _statePath = statePath;
            _cookiePath = Path.Combine(_storageRoot, "cookies.json");
            _config = config;
            _state = state;
            _runSplitSelfTest = runSplitSelfTest;
            _runPopoutPauseSelfTest = runPopoutPauseSelfTest;
            _runToolbarSelfTest = runToolbarSelfTest;
            _runDualSelfTest = runDualSelfTest;
            _runBigscreenGeometrySelfTest = runBigscreenGeometrySelfTest;
            _runStreamChatPanelSelfTest = Array.Exists(
                Environment.GetCommandLineArgs(),
                arg => string.Equals(arg, "--self-test-stream-chat-panel", StringComparison.OrdinalIgnoreCase));
            _selfTestResultPath = selfTestResultPath ?? string.Empty;
            _pendingNavigation = (runSplitSelfTest || runPopoutPauseSelfTest || runToolbarSelfTest || runDualSelfTest || runBigscreenGeometrySelfTest || _runStreamChatPanelSelfTest) ? _config.HomeUrl : GetStartupUrl();
            _latestBigscreenEmbeds = new List<BigscreenEmbedState>();
            _dualChatInputTop = 0;
            _dualChatPaneLeft = 0;
            _dualChatPaneTop = 0;
            _dualChatPaneWidth = 0;
            _dualChatPaneHeight = 0;
            _dualChatLayoutActive = false;
            _dualChatNavigationInFlight = false;

            Text = _config.Title;
            BackColor = Color.FromArgb(18, 18, 18);
            MinimumSize = new Size(900, 640);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            bool startPinned = _state.LoadedFromDisk ? _state.AlwaysOnTop : _config.StartPinned;
            bool startMuted = _state.LoadedFromDisk ? _state.Muted : _config.StartMuted;

            ApplyInitialWindowBounds();
            TopMost = startPinned;

            _toolStrip = new ToolStrip();
            _toolStrip.Dock = DockStyle.Top;
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _toolStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            _toolStrip.Renderer = new BorderlessToolStripRenderer();
            _toolStrip.BackColor = Color.FromArgb(30, 30, 30);
            _toolStrip.ForeColor = Color.White;
            _toolStrip.Padding = new Padding(6, 4, 6, 4);
            _toolStrip.AutoSize = false;
            _toolStrip.Height = 40;

            _backButton = CreateGlyphButton("◀", "Back", OnBackClicked);
            _forwardButton = CreateGlyphButton("▶", "Forward", OnForwardClicked);
            _homeButton = CreateButton("Chat", OnHomeClicked);
            _bigscreenButton = CreateButton("Bigscreen", OnBigscreenClicked);
            _mediaPopoutButton = CreateGlyphButton("⧉", "Popout media player", OnMediaPopoutClicked);
            _loginButton = CreateButton("Login", OnLoginClicked);
            _reloadButton = CreateButton("Reload", OnReloadClicked);
            _updateButton = CreateButton("Update", OnCheckForUpdatesClicked);
            _addressBox = new SpringToolStripTextBox();
            _addressBox.Text = _pendingNavigation;
            _addressBox.ToolTipText = "Type a Destiny.gg URL or path, then press Enter.";
            _addressBox.KeyDown += OnAddressBoxKeyDown;
            _goButton = CreateButton("Go", OnGoClicked);
            _muteButton = CreateToggleButton("Mute", startMuted, OnMuteClicked);
            _pinButton = CreateToggleButton("Pin", startPinned, OnPinClicked);
            _zoomOutButton = CreateButton("-", OnZoomOutClicked);
            _zoomResetButton = CreateButton("100%", OnZoomResetClicked);
            _zoomInButton = CreateButton("+", OnZoomInClicked);
            _browserButton = CreateButton("Browser", OnBrowserClicked);

            _toolStrip.Items.Add(_backButton);
            _toolStrip.Items.Add(_forwardButton);
            _toolStrip.Items.Add(_homeButton);
            _toolStrip.Items.Add(_bigscreenButton);
            _toolStrip.Items.Add(_mediaPopoutButton);
            _toolStrip.Items.Add(_reloadButton);
            _toolStrip.Items.Add(_browserButton);
            _toolStrip.Items.Add(_pinButton);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_zoomOutButton);
            _toolStrip.Items.Add(_zoomResetButton);
            _toolStrip.Items.Add(_zoomInButton);

            _statusStrip = new StatusStrip();
            _statusStrip.BackColor = Color.FromArgb(30, 30, 30);
            _statusStrip.ForeColor = Color.White;
            _statusStrip.SizingGrip = false;
            _statusStrip.Visible = false;
            _statusLabel = new ToolStripStatusLabel();
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.Text = "Initializing browser shell...";
            _statusStrip.Items.Add(_statusLabel);

            _bigscreenBarPanel = new Panel();
            _bigscreenBarPanel.Dock = DockStyle.Top;
            _bigscreenBarPanel.Height = 40;
            _bigscreenBarPanel.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenBarPanel.Visible = false;
            _bigscreenBarPanel.Resize += OnBigscreenBarPanelResize;

            _bigscreenControlsPanel = new Panel();
            _bigscreenControlsPanel.Dock = DockStyle.None;
            _bigscreenControlsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _bigscreenControlsPanel.Height = 0;
            _bigscreenControlsPanel.Margin = new Padding(0);
            _bigscreenControlsPanel.Location = new Point(0, 0);
            _bigscreenControlsPanel.Width = _bigscreenBarPanel.Width;
            _bigscreenControlsPanel.Padding = new Padding(8, 2, 8, 3);
            _bigscreenControlsPanel.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenControlsPanel.Visible = false;

            _bigscreenRefreshButton = CreateBigscreenActionButton("Refresh", OnBigscreenRefreshClicked);
            _bigscreenRefreshButton.Width = 74;
            _bigscreenRefreshButton.Dock = DockStyle.Left;
            _bigscreenRefreshButton.ForeColor = Color.FromArgb(67, 72, 78);

            _bigscreenCinemaButton = CreateBigscreenActionButton("Cinema Mode", OnBigscreenCinemaClicked);
            _bigscreenCinemaButton.Width = 126;
            _bigscreenCinemaButton.Dock = DockStyle.Left;
            _bigscreenCinemaButton.ForeColor = Color.FromArgb(175, 179, 186);

            _bigscreenEmbedsViewport = new EmbedViewportPanel();
            _bigscreenEmbedsViewport.Dock = DockStyle.None;
            _bigscreenEmbedsViewport.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _bigscreenEmbedsViewport.Margin = new Padding(0);
            _bigscreenEmbedsViewport.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenEmbedsViewport.MouseWheel += OnBigscreenEmbedsMouseWheel;
            _bigscreenEmbedsViewport.MouseEnter += OnBigscreenEmbedsMouseEnter;
            _bigscreenEmbedsViewport.Resize += OnBigscreenEmbedsResize;

            _bigscreenEmbedsUiView = new WebView2();
            _bigscreenEmbedsUiView.Dock = DockStyle.Fill;
            _bigscreenEmbedsUiView.BackColor = Color.FromArgb(17, 17, 19);

            _bigscreenEmbedsPanel = new FlowLayoutPanel();
            _bigscreenEmbedsPanel.Dock = DockStyle.None;
            _bigscreenEmbedsPanel.Margin = new Padding(0);
            _bigscreenEmbedsPanel.Padding = new Padding(8, 4, 8, 4);
            _bigscreenEmbedsPanel.WrapContents = false;
            _bigscreenEmbedsPanel.AutoSize = false;
            _bigscreenEmbedsPanel.FlowDirection = FlowDirection.LeftToRight;
            _bigscreenEmbedsPanel.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenEmbedsPanel.Location = new Point(0, 0);

            _bigscreenToolTip = new ToolTip();
            _bigscreenToolTip.AutoPopDelay = 8000;
            _bigscreenToolTip.InitialDelay = 250;
            _bigscreenToolTip.ReshowDelay = 100;

            _bigscreenBarView = new WebView2();
            _bigscreenBarView.Dock = DockStyle.None;
            _bigscreenBarView.Size = new Size(1, 1);
            _bigscreenBarView.Location = new Point(-10000, -10000);
            _bigscreenBarView.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenBarView.Visible = false;

            _bigscreenEmbedsViewport.Controls.Add(_bigscreenEmbedsUiView);
            _bigscreenEmbedsViewport.Controls.Add(_bigscreenEmbedsPanel);
            _bigscreenEmbedsPanel.Visible = false;
            _bigscreenBarPanel.Controls.Add(_bigscreenEmbedsViewport);
            _bigscreenBarPanel.Controls.Add(_bigscreenControlsPanel);
            _bigscreenControlsPanel.BringToFront();
            LayoutBigscreenBarRows();

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.BackColor = Color.Black;

            _dualChatPanel = new Panel();
            _dualChatPanel.Visible = false;
            _dualChatPanel.BackColor = Color.FromArgb(17, 17, 19);

            _dualChatView = new WebView2();
            _dualChatView.Dock = DockStyle.Fill;
            _dualChatView.BackColor = Color.FromArgb(17, 17, 19);
            _dualChatView.Visible = false;

            _dualChatEmptyLabel = new Label();
            _dualChatEmptyLabel.Dock = DockStyle.Fill;
            _dualChatEmptyLabel.Visible = false;
            _dualChatEmptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _dualChatEmptyLabel.ForeColor = Color.FromArgb(208, 212, 216);
            _dualChatEmptyLabel.Font = new Font("Segoe UI", 10.0f, FontStyle.Regular);
            _dualChatEmptyLabel.Padding = new Padding(18);
            _dualChatEmptyLabel.BackColor = Color.FromArgb(17, 17, 19);

            _dualChatPanel.Controls.Add(_dualChatView);
            _dualChatPanel.Controls.Add(_dualChatEmptyLabel);
            _dualChatEmptyLabel.BringToFront();

            _sessionPersistTimer = new Timer();
            _sessionPersistTimer.Interval = 15000;
            _sessionPersistTimer.Tick += OnSessionPersistTimerTick;

            Controls.Add(_webView);
            Controls.Add(_dualChatPanel);
            Controls.Add(_bigscreenBarPanel);
            Controls.Add(_bigscreenBarView);
            Controls.Add(_statusStrip);
            Controls.Add(_toolStrip);

            Resize += OnMainFormResize;
            Shown += OnShown;
            FormClosing += OnFormClosing;

            UpdateNavigationButtons();
            UpdateZoomLabel(_state.ZoomFactor);
        }

        public static double ClampZoom(double zoomFactor)
        {
            if (zoomFactor < 0.50)
            {
                return 0.50;
            }

            if (zoomFactor > 2.50)
            {
                return 2.50;
            }

            if (zoomFactor <= 0.0)
            {
                return 1.0;
            }

            return Math.Round(zoomFactor, 2);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadCurrentPage();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Left))
            {
                NavigateBack();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Right))
            {
                NavigateForward();
                return true;
            }

            if (keyData == Keys.F11)
            {
                ToggleFullScreen();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Add) || keyData == (Keys.Control | Keys.Oemplus))
            {
                AdjustZoom(0.10);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Subtract) || keyData == (Keys.Control | Keys.OemMinus))
            {
                AdjustZoom(-0.10);
                return true;
            }

            if (keyData == (Keys.Control | Keys.D0) || keyData == (Keys.Control | Keys.NumPad0))
            {
                ResetZoom();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ApplyInitialWindowBounds()
        {
            Rectangle desiredBounds = new Rectangle(_state.X, _state.Y, _state.Width, _state.Height);

            if (_state.LoadedFromDisk && BoundsIntersectVisibleArea(desiredBounds))
            {
                StartPosition = FormStartPosition.Manual;
                DesktopBounds = desiredBounds;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(_state.Width, _state.Height);
            }

            if (_state.IsMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
        }

        private static bool BoundsIntersectVisibleArea(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private ToolStripButton CreateButton(string text, EventHandler clickHandler)
        {
            ToolStripButton button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.AutoSize = true;
            button.Margin = new Padding(2, 0, 2, 0);
            button.Click += clickHandler;
            return button;
        }

        private ToolStripButton CreateGlyphButton(string glyph, string toolTipText, EventHandler clickHandler)
        {
            ToolStripButton button = CreateButton(glyph, clickHandler);
            button.AutoSize = false;
            button.Width = 28;
            button.Height = 24;
            button.Margin = new Padding(2, 0, 2, 0);
            button.ToolTipText = toolTipText;
            button.Font = new Font("Segoe UI Symbol", 9.0f, FontStyle.Regular);
            if (glyph == "◀" || glyph == "▶")
            {
                button.Padding = new Padding(0, 0, 0, 2);
            }
            return button;
        }

        private ToolStripButton CreateToggleButton(string text, bool isChecked, EventHandler clickHandler)
        {
            ToolStripButton button = CreateButton(text, clickHandler);
            button.CheckOnClick = true;
            button.Checked = isChecked;
            return button;
        }

        private Button CreateBigscreenActionButton(string text, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Text = text;
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(38, 38, 44);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 32, 36);
            button.ForeColor = Color.FromArgb(224, 226, 230);
            button.BackColor = Color.FromArgb(17, 17, 19);
            button.Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            button.TextAlign = ContentAlignment.TopLeft;
            button.Padding = new Padding(0, 1, 0, 0);
            button.Cursor = Cursors.Hand;
            button.Click += clickHandler;
            return button;
        }

        private string GetStartupUrl()
        {
            Uri startupUri;
            if (!string.IsNullOrWhiteSpace(_state.LastUrl) && TryBuildUri(_state.LastUrl, out startupUri) && IsAllowedUri(startupUri))
            {
                return startupUri.AbsoluteUri;
            }

            return _config.HomeUrl;
        }

        private async void OnShown(object sender, EventArgs e)
        {
            await InitializeBrowserAsync();
        }

        private async System.Threading.Tasks.Task InitializeBrowserAsync()
        {
            try
            {
                string userDataFolder = Path.Combine(_storageRoot, "UserData");
                Directory.CreateDirectory(userDataFolder);

                _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(_webViewEnvironment);
                await _dualChatView.EnsureCoreWebView2Async(_webViewEnvironment);
                await _bigscreenBarView.EnsureCoreWebView2Async(_webViewEnvironment);
                await _bigscreenEmbedsUiView.EnsureCoreWebView2Async(_webViewEnvironment);

                _browserReady = true;
                RestoreCookies();

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.IsMuted = _muteButton.Checked;
                double initialZoom = _state.LoadedFromDisk
                    ? ClampZoom(_state.ZoomFactor)
                    : ClampZoom(_config.StartZoomFactor);
                _webView.ZoomFactor = initialZoom;

                _webView.NavigationStarting += OnNavigationStarting;
                _webView.NavigationCompleted += OnNavigationCompleted;
                _webView.SourceChanged += OnSourceChanged;
                _webView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
                _webView.CoreWebView2.HistoryChanged += OnHistoryChanged;
                _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                _webView.CoreWebView2.WebMessageReceived += OnMainWebViewMessageReceived;
                ConfigureDualChatView();
                ConfigureBigscreenBarView();
                ConfigureBigscreenEmbedsUiView();
                await RegisterChatCustomizationAsync();
                await RegisterChatExtensionsAsync();
                await RegisterBigscreenBarCustomizationAsync();
                await RegisterBigscreenChatLayoutAsync();
                _bigscreenEmbedsUiView.CoreWebView2.NavigateToString(BuildBigscreenEmbedsHtml(null));

                UpdateZoomLabel(initialZoom);
                UpdateNavigationButtons();
                SetStatus("Loading Destiny.gg chat...");
                _sessionPersistTimer.Start();

                NavigateBigscreenBar();
                NavigateToUrl(_pendingNavigation);
                LayoutDualChatPanel();
                BeginStartupUpdateCheck();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowFatalError(
                    "Microsoft Edge WebView2 Runtime is required but was not found.\r\n\r\nInstall it from Microsoft and then run the app again.");
            }
            catch (Exception ex)
            {
                ShowFatalError("The embedded browser could not be initialized.\r\n\r\n" + ex.Message);
            }
        }

        private void ConfigureBigscreenBarView()
        {
            _bigscreenBarView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _bigscreenBarView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _bigscreenBarView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _bigscreenBarView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _bigscreenBarView.CoreWebView2.IsMuted = true;
            _bigscreenBarView.NavigationCompleted += OnBigscreenBarNavigationCompleted;
            _bigscreenBarView.CoreWebView2.NewWindowRequested += OnBigscreenBarNewWindowRequested;
            _bigscreenBarView.CoreWebView2.WebMessageReceived += OnBigscreenBarWebMessageReceived;
        }

        private void ConfigureBigscreenEmbedsUiView()
        {
            _bigscreenEmbedsUiView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _bigscreenEmbedsUiView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _bigscreenEmbedsUiView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _bigscreenEmbedsUiView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _bigscreenEmbedsUiView.CoreWebView2.IsMuted = true;
            _bigscreenEmbedsUiView.CoreWebView2.WebMessageReceived += OnBigscreenEmbedsUiWebMessageReceived;
        }

        private void ConfigureDualChatView()
        {
            _dualChatView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _dualChatView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _dualChatView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            _dualChatView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _dualChatView.CoreWebView2.IsMuted = true;
            try
            {
                _dualChatView.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            }
            catch
            {
            }
            _dualChatView.NavigationStarting += OnDualChatNavigationStarting;
            _dualChatView.NavigationCompleted += OnDualChatNavigationCompleted;
            _dualChatView.CoreWebView2.NewWindowRequested += OnDualChatNewWindowRequested;

            // Stretch embedded stream chat pages to the host panel bounds and
            // hide the YouTube live chat header so only the message list/input remain.
            _dualChatView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    const styleId = 'codex-dual-chat-host-style';

                    function applyLayoutCss() {
                        const hostName = (location.hostname || '').toLowerCase();
                        const isYouTubeChat = hostName.indexOf('youtube.com') >= 0 || hostName.indexOf('youtu.be') >= 0;
                        const isKickChat = hostName === 'kick.com' || hostName === 'www.kick.com' || hostName.endsWith('.kick.com');
                        if (!isYouTubeChat && !isKickChat) {
                            const existingStyle = document.getElementById(styleId);
                            if (existingStyle && existingStyle.parentElement) {
                                existingStyle.parentElement.removeChild(existingStyle);
                            }
                            return;
                        }

                        const kickCssText = [
                            'html, body {',
                            '  margin: 0 !important;',
                            '  padding: 0 !important;',
                            '  width: 100% !important;',
                            '  height: 100% !important;',
                            '  min-width: 0 !important;',
                            '  min-height: 0 !important;',
                            '  overflow: auto !important;',
                            '  -webkit-overflow-scrolling: touch;',
                            '  background: #0e0e10 !important;',
                            '}',
                            '#root, #__next, main, [data-reactroot] {',
                            '  min-height: 100% !important;',
                            '  box-sizing: border-box !important;',
                            '}'
                        ].join('');

                        const cssText = [
                            'html, body {',
                            '  margin: 0 !important;',
                            '  padding: 0 !important;',
                            '  width: 100% !important;',
                            '  height: 100% !important;',
                            '  min-width: 0 !important;',
                            '  min-height: 0 !important;',
                            '  overflow: hidden !important;',
                            '  background: #111113 !important;',
                            '}',
                            'yt-live-chat-header-renderer,',
                            '#chat-messages > yt-live-chat-header-renderer,',
                            'yt-live-chat-ticker-renderer {',
                            '  display: none !important;',
                            '}',
                            'yt-live-chat-app,',
                            'yt-live-chat-renderer,',
                            'yt-live-chat-renderer #contents,',
                            'yt-live-chat-renderer #chat,',
                            'yt-live-chat-renderer #chat-messages,',
                            'yt-live-chat-renderer #item-scroller,',
                            'yt-live-chat-renderer #item-list,',
                            'yt-live-chat-renderer #items,',
                            'yt-live-chat-renderer #panel-pages,',
                            'yt-live-chat-message-input-renderer {',
                            '  width: 100% !important;',
                            '  max-width: none !important;',
                            '  min-width: 0 !important;',
                            '  box-sizing: border-box !important;',
                            '}',
                            'yt-live-chat-app,',
                            'yt-live-chat-renderer {',
                            '  display: block !important;',
                            '  width: 100% !important;',
                            '  height: 100% !important;',
                            '  min-height: 100% !important;',
                            '  max-width: none !important;',
                            '}'
                        ].join('');

                        const host = document.head || document.documentElement;
                        if (!host) {
                            return;
                        }

                        let style = document.getElementById(styleId);
                        if (!style) {
                            style = document.createElement('style');
                            style.id = styleId;
                            host.appendChild(style);
                        }

                        const desired = isYouTubeChat ? cssText : kickCssText;
                        if (style.textContent !== desired) {
                            style.textContent = desired;
                        }
                    }

                    applyLayoutCss();
                    document.addEventListener('DOMContentLoaded', applyLayoutCss);

                    if (typeof MutationObserver === 'function') {
                        let scheduled = false;
                        const scheduleApply = () => {
                            if (scheduled) return;
                            scheduled = true;
                            (window.requestAnimationFrame || window.setTimeout)(() => {
                                scheduled = false;
                                applyLayoutCss();
                            }, 0);
                        };
                        const observer = new MutationObserver(scheduleApply);
                        const startObserving = () => {
                            if (document.body) {
                                observer.observe(document.body, { childList: true, subtree: false });
                            } else if (document.documentElement) {
                                observer.observe(document.documentElement, { childList: true, subtree: false });
                            }
                        };

                        startObserving();
                        document.addEventListener('DOMContentLoaded', startObserving);
                    }
                })();
            ");
        }

        private async void BeginStartupUpdateCheck()
        {
            if (_startupUpdateCheckQueued || !_config.CheckForUpdatesOnStartup || _runSplitSelfTest)
            {
                return;
            }

            _startupUpdateCheckQueued = true;
            await CheckForUpdatesAsync(false);
        }

        private async void OnCheckForUpdatesClicked(object sender, EventArgs e)
        {
            await CheckForUpdatesAsync(true);
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync(bool manual)
        {
            if (_isCheckingForUpdates)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_config.UpdateRepoOwner) || string.IsNullOrWhiteSpace(_config.UpdateRepoName))
            {
                if (manual)
                {
                    MessageBox.Show(
                        this,
                        "GitHub update settings are not configured yet.",
                        "Update Check",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            _isCheckingForUpdates = true;
            _updateButton.Enabled = false;

            try
            {
                UpdateReleaseInfo release = await FetchLatestReleaseAsync();
                if (release == null)
                {
                    if (manual)
                    {
                        MessageBox.Show(
                            this,
                            "No downloadable release was found on GitHub.",
                            "Update Check",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    return;
                }

                Version currentVersion;
                Version releaseVersion;
                if (!TryParseVersion(_config.AppVersion, out currentVersion) ||
                    !TryParseVersion(release.VersionTag, out releaseVersion))
                {
                    if (manual)
                    {
                        MessageBox.Show(
                            this,
                            "The app version or release tag could not be parsed for update comparison.",
                            "Update Check",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }

                    return;
                }

                if (releaseVersion <= currentVersion)
                {
                    if (manual)
                    {
                        MessageBox.Show(
                            this,
                            "You're already on the latest version (" + currentVersion + ").",
                            "Update Check",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    return;
                }

                DialogResult result = MessageBox.Show(
                    this,
                    "Version " + releaseVersion + " is available on GitHub.\r\n\r\nInstall it now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                await DownloadAndApplyUpdateAsync(release);
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N2",
                    "DestinyChatDesktop.cs:1055",
                    "Update check exception",
                    new Dictionary<string, object>
                    {
                        { "manual", manual },
                        { "error", ex.Message }
                    });
                // #endregion
                if (manual)
                {
                    MessageBox.Show(
                        this,
                        "Update check failed.\r\n\r\n" + ex.Message,
                        "Update Check",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                _isCheckingForUpdates = false;
                if (!IsDisposed)
                {
                    _updateButton.Enabled = true;
                }
            }
        }

        private System.Threading.Tasks.Task<UpdateReleaseInfo> FetchLatestReleaseAsync()
        {
            return System.Threading.Tasks.Task.Run(delegate
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls |
                    (SecurityProtocolType)768 |
                    (SecurityProtocolType)3072;

                string apiUrl = _config.AllowPrereleaseUpdates
                    ? "https://api.github.com/repos/" + _config.UpdateRepoOwner + "/" + _config.UpdateRepoName + "/releases"
                    : "https://api.github.com/repos/" + _config.UpdateRepoOwner + "/" + _config.UpdateRepoName + "/releases/latest";

                using (WebClient client = CreateGitHubWebClient())
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = client.DownloadString(apiUrl);
                    object parsed = serializer.DeserializeObject(json);

                    if (_config.AllowPrereleaseUpdates)
                    {
                        foreach (object candidate in EnumerateJsonArray(parsed))
                        {
                            UpdateReleaseInfo info = TryCreateUpdateReleaseInfo(candidate as Dictionary<string, object>);
                            if (info != null)
                            {
                                return info;
                            }
                        }

                        return null;
                    }

                    return TryCreateUpdateReleaseInfo(parsed as Dictionary<string, object>);
                }
            });
        }

        private async System.Threading.Tasks.Task DownloadAndApplyUpdateAsync(UpdateReleaseInfo release)
        {
            string tempRoot = Path.Combine(_storageRoot, "updates", Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempRoot, release.AssetName ?? _config.UpdateAssetName);
            string extractPath = Path.Combine(tempRoot, "package");
            string scriptPath = Path.Combine(tempRoot, "apply-update.ps1");

            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractPath);

            await System.Threading.Tasks.Task.Run(delegate
            {
                using (WebClient client = CreateGitHubWebClient())
                {
                    client.DownloadFile(release.DownloadUrl, zipPath);
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);
            });

            string packageRoot = ResolvePackageRoot(extractPath);
            string installRoot = AppDomain.CurrentDomain.BaseDirectory;
            string exeName = Path.GetFileName(Application.ExecutablePath);
            File.WriteAllText(scriptPath, BuildUpdateScriptContents());

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments =
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"" +
                " -WaitPid " + Process.GetCurrentProcess().Id +
                " -InstallRoot \"" + installRoot + "\"" +
                " -PackageRoot \"" + packageRoot + "\"" +
                " -ExeName \"" + exeName + "\"";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            Process.Start(startInfo);
            BeginInvoke(new Action(Close));
        }

        private static WebClient CreateGitHubWebClient()
        {
            WebClient client = new WebClient();
            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.UserAgent] = "DestinyChatDesktop-Updater";
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return client;
        }

        private UpdateReleaseInfo TryCreateUpdateReleaseInfo(Dictionary<string, object> release)
        {
            if (release == null)
            {
                return null;
            }

            bool draft;
            if (TryGetBooleanValue(release, "draft", out draft) && draft)
            {
                return null;
            }

            if (!_config.AllowPrereleaseUpdates)
            {
                bool prerelease;
                if (TryGetBooleanValue(release, "prerelease", out prerelease) && prerelease)
                {
                    return null;
                }
            }

            string tagName = GetStringValue(release, "tag_name");
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            Dictionary<string, object> matchedAsset = null;
            object assetsValue;
            if (release.TryGetValue("assets", out assetsValue))
            {
                foreach (object assetObject in EnumerateJsonArray(assetsValue))
                {
                    Dictionary<string, object> asset = assetObject as Dictionary<string, object>;
                    if (asset == null)
                    {
                        continue;
                    }

                    string assetName = GetStringValue(asset, "name");
                    if (string.IsNullOrWhiteSpace(assetName))
                    {
                        continue;
                    }

                    if (string.Equals(assetName, _config.UpdateAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedAsset = asset;
                        break;
                    }

                    if (matchedAsset == null && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        matchedAsset = asset;
                    }
                }
            }

            if (matchedAsset == null)
            {
                return null;
            }

            string downloadUrl = GetStringValue(matchedAsset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            return new UpdateReleaseInfo
            {
                VersionTag = tagName,
                AssetName = GetStringValue(matchedAsset, "name"),
                DownloadUrl = downloadUrl,
                ReleasePageUrl = GetStringValue(release, "html_url")
            };
        }

        private static IEnumerable<object> EnumerateJsonArray(object value)
        {
            System.Collections.ArrayList list = value as System.Collections.ArrayList;
            if (list != null)
            {
                foreach (object item in list)
                {
                    yield return item;
                }

                yield break;
            }

            object[] array = value as object[];
            if (array != null)
            {
                foreach (object item in array)
                {
                    yield return item;
                }
            }
        }

        private static string GetStringValue(Dictionary<string, object> source, string key)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            object value;
            if (!source.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value) ?? string.Empty;
        }

        private static bool TryGetBooleanValue(Dictionary<string, object> source, string key, out bool result)
        {
            result = false;
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            object value;
            if (!source.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            return bool.TryParse(Convert.ToString(value), out result);
        }

        private static bool TryParseVersion(string rawVersion, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return false;
            }

            string normalized = rawVersion.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(1);
            }

            return Version.TryParse(normalized, out version);
        }

        private static string ResolvePackageRoot(string extractedPath)
        {
            if (string.IsNullOrWhiteSpace(extractedPath) || !Directory.Exists(extractedPath))
            {
                return extractedPath;
            }

            string[] directories = Directory.GetDirectories(extractedPath);
            string[] files = Directory.GetFiles(extractedPath);
            if (files.Length == 0 && directories.Length == 1)
            {
                return directories[0];
            }

            return extractedPath;
        }

        private static string BuildUpdateScriptContents()
        {
            return @"param(
    [int]$WaitPid,
    [string]$InstallRoot,
    [string]$PackageRoot,
    [string]$ExeName
)

$ErrorActionPreference = 'Stop'

for ($i = 0; $i -lt 240; $i++) {
    $process = Get-Process -Id $WaitPid -ErrorAction SilentlyContinue
    if (-not $process) {
        break
    }

    Start-Sleep -Milliseconds 500
}

Get-ChildItem -LiteralPath $PackageRoot -Force | ForEach-Object {
    $destination = Join-Path $InstallRoot $_.Name
    if ($_.PSIsContainer) {
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }

        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
        return
    }

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}

Start-Sleep -Seconds 1
Start-Process -FilePath (Join-Path $InstallRoot $ExeName)
";
        }

        private System.Threading.Tasks.Task RegisterBigscreenBarCustomizationAsync()
        {
            if (_bigscreenBarView.CoreWebView2 == null)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            return _bigscreenBarView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    const host = window.chrome && window.chrome.webview;
                    let observer = null;
                    let collectTimer = 0;

                    function calmHiddenEmbeds() {
                        const hiddenFrameSelectors = ['#chat-wrap iframe', '#embed iframe'];
                        hiddenFrameSelectors.forEach((selector) => {
                            const frame = document.querySelector(selector);
                            if (frame && frame.getAttribute('src') !== 'about:blank') {
                                frame.setAttribute('src', 'about:blank');
                            }
                        });

                        const video = document.querySelector('#embed video');
                        if (video) {
                            try {
                                video.pause();
                                video.removeAttribute('src');
                                video.load();
                            } catch (error) {
                            }
                        }
                    }

                    function normalizeHref(rawValue) {
                        if (!rawValue) {
                            return null;
                        }

                        try {
                            return new URL(rawValue, window.location.href).href;
                        } catch (error) {
                            return null;
                        }
                    }

                    function collectEmbeds() {
                        calmHiddenEmbeds();

                        if (!host) {
                            return;
                        }

                        const items = Array.from(document.querySelectorAll('#chat-panel-embeds a.embed-link')).map((anchor) => {
                            const textElement = anchor.querySelector('.embed-link__text');
                            return {
                                url: normalizeHref(anchor.getAttribute('href') || anchor.href),
                                platform: anchor.getAttribute('data-platform') || '',
                                mediaId: anchor.getAttribute('data-id') || '',
                                text: ((textElement ? textElement.textContent : anchor.textContent) || '')
                                    .replace(/\s+/g, ' ')
                                    .trim(),
                                title: anchor.getAttribute('title') || '',
                                selected: anchor.classList.contains('embed-link--selected')
                            };
                        }).filter((item) => !!item.url);

                        host.postMessage({
                            type: 'embedsState',
                            items: items
                        });
                    }

                    function scheduleCollect() {
                        window.clearTimeout(collectTimer);
                        collectTimer = window.setTimeout(collectEmbeds, 150);
                    }

                    function attachWatcher() {
                        calmHiddenEmbeds();

                        if (observer) {
                            observer.disconnect();
                        }

                        const root = document.documentElement || document.body;
                        if (!root) {
                            scheduleMetrics();
                            return;
                        }

                        observer = new MutationObserver(() => {
                            scheduleCollect();
                        });
                        observer.observe(root, {
                            childList: true,
                            subtree: true,
                            characterData: true,
                            attributes: true
                        });

                        scheduleCollect();
                        window.setInterval(() => {
                            collectEmbeds();
                        }, 5000);
                    }

                    if (document.readyState === 'loading') {
                        document.addEventListener('DOMContentLoaded', attachWatcher, { once: true });
                    } else {
                        attachWatcher();
                    }

                    window.addEventListener('load', collectEmbeds);
                })();
            ");
        }

        private System.Threading.Tasks.Task RegisterChatCustomizationAsync()
        {
            if (_webView.CoreWebView2 == null)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            return _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    if (window.__codexSplitChatInjected) {
                        return;
                    }

                    window.__codexSplitChatInjected = true;

                    const styleId = 'codex-split-chat-style';
                    const buttonId = 'codex-split-chat-btn';
                    const dualButtonId = 'codex-stream-chat-btn';
                    const overlayId = 'codex-split-chat-overlay';
                    const dualPaneId = 'codex-dual-stream-pane';
                    const measureId = 'codex-split-chat-measure';
                    const storageKey = 'codex-split-chat-enabled';
                    const overlayUnpinnedClass = 'codex-split-chat-unpinned';
                    const splitFocusClass = 'codex-split-chat-focus';
                    const splitFocusMatchClass = 'codex-split-chat-focus-match';
                    let splitEnabled = false;
                    let dualChatEnabled = false;
                    let dualChatSource = null;
                    let dualChatHostBound = false;
                    let dualChatLayoutBound = false;
                    let dualChatLayoutScheduled = false;
                    let dualChatInputObserved = null;
                    let dualChatInputResizeObserver = null;
                    let bigscreenDualModeTimer = null;
                    let bigscreenDualLayoutKey = '';
                    let splitFocusFilter = null;
                    let renderToken = 0;
                    let splitScrollOffset = 0;
                    let splitViewportAnchor = null;
                    let lastCollectedItems = [];
                    let lastRenderedTotalHeight = 0;
                    let lastRenderedMessageCount = 0;
                    let lastPaneHeight = 0;
                    let lastMaxScroll = 0;
                    let frameObserver = null;
                    let rootObserver = null;
                    let frameResizeObserver = null;
                    let sourceResizeObserver = null;
                    let observedSource = null;
                    let toolbarHandlersBound = false;
                    let bootstrapped = false;

                    function ensureStyles() {
                        if (document.getElementById(styleId) || !document.head) {
                            return;
                        }

                        const style = document.createElement('style');
                        style.id = styleId;
                        style.textContent = `
                            #${buttonId} .btn-icon {
                                background-repeat: no-repeat;
                                background-position: center;
                                background-size: 100% 100%;
                                background-image: url(""data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><rect x='3.25' y='4.25' width='17.5' height='15.5' rx='1.5' fill='none' stroke='white' stroke-width='1.75'/><path d='M12 4.5v15' stroke='white' stroke-width='1.75' stroke-linecap='round'/></svg>"");
                            }

                            #${buttonId}.codex-split-chat-active .btn-icon {
                                opacity: 1;
                            }

                            #${dualButtonId} .btn-icon {
                                background-repeat: no-repeat;
                                background-position: center;
                                background-size: 100% 100%;
                                background-image: url(""data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><rect x='3.5' y='4.5' width='8.5' height='15' rx='1.2' fill='none' stroke='white' stroke-width='1.4'/><rect x='12' y='4.5' width='8.5' height='15' rx='1.2' fill='none' stroke='white' stroke-width='1.4'/><text x='7.75' y='14.2' text-anchor='middle' font-family='Segoe UI,Arial,sans-serif' font-size='6.6' fill='white'>D</text><text x='16.25' y='14.2' text-anchor='middle' font-family='Segoe UI,Arial,sans-serif' font-size='6.6' fill='white'>K</text></svg>"");
                            }

                            #${dualButtonId}.codex-split-chat-active .btn-icon {
                                opacity: 1;
                            }

                            #chat-output-frame {
                                position: relative;
                            }

                            body.codex-dual-chat-enabled #chat-output-frame {
                                width: 100% !important;
                                max-width: none !important;
                                box-sizing: border-box;
                            }

                            #${dualPaneId} {
                                position: absolute;
                                top: 0;
                                right: 0;
                                bottom: var(--codex-dual-chat-bottom-offset, 0px);
                                width: calc((100% - 14px) / 2);
                                display: none;
                                flex-direction: column;
                                overflow: hidden;
                                background: #111113;
                                border-left: 1px solid rgba(255,255,255,0.08);
                                box-sizing: border-box;
                                z-index: 7;
                                opacity: 1;
                                visibility: visible;
                                pointer-events: auto;
                            }

                            #${dualPaneId}.enabled {
                                display: flex !important;
                            }

                            #${dualPaneId} .codex-dual-chat-frame {
                                flex: 1;
                                width: 100%;
                                height: 100%;
                                border: none;
                                background: #111113;
                            }

                            #${dualPaneId} .codex-dual-chat-empty {
                                position: absolute;
                                inset: 0;
                                display: none;
                                align-items: center;
                                justify-content: center;
                                padding: 18px;
                                text-align: center;
                                color: rgba(208, 212, 216, 0.88);
                                background: #111113;
                                font-size: 13px;
                                line-height: 1.45;
                            }

                            #${dualPaneId}.empty .codex-dual-chat-empty {
                                display: flex;
                            }

                            #${dualPaneId}.empty .codex-dual-chat-frame {
                                display: none;
                            }

                            #${dualPaneId}.codex-dual-chat-host-guest {
                                pointer-events: none;
                                background: transparent !important;
                                border-left-color: transparent !important;
                            }

                            #${dualPaneId}.codex-dual-chat-host-guest .codex-dual-chat-frame,
                            #${dualPaneId}.codex-dual-chat-host-guest .codex-dual-chat-empty {
                                display: none !important;
                            }

                            #${dualPaneId}.codex-dual-stream-pane--bigscreen-fixed {
                                position: fixed;
                                top: 0;
                                right: 0;
                                bottom: 0;
                                width: min(420px, max(280px, calc((100vw - 48px) / 2)));
                                z-index: 9999;
                                box-shadow: -2px 0 8px rgba(0, 0, 0, 0.35);
                            }

                            body.codex-bigscreen-dual-single-chat #chat-wrap,
                            body.codex-bigscreen-dual-single-chat .chat-wrap {
                                width: 100% !important;
                                max-width: 100% !important;
                                box-sizing: border-box !important;
                                overflow: hidden !important;
                            }

                            body.codex-bigscreen-dual-single-chat #chat-output-frame,
                            body.codex-bigscreen-dual-single-chat .chat-output-frame {
                                width: 100% !important;
                                max-width: 100% !important;
                                box-sizing: border-box !important;
                            }

                            body.codex-bigscreen-dual-single-chat .codex-bigscreen-dual-hide-target {
                                display: none !important;
                                width: 0 !important;
                                min-width: 0 !important;
                                max-width: 0 !important;
                                overflow: hidden !important;
                                margin: 0 !important;
                                padding: 0 !important;
                                border: 0 !important;
                                flex: 0 0 0 !important;
                            }

                            body.codex-bigscreen-dual-single-chat .codex-bigscreen-dual-hide-sibling {
                                display: none !important;
                                width: 0 !important;
                                min-width: 0 !important;
                                max-width: 0 !important;
                                overflow: hidden !important;
                                margin: 0 !important;
                                padding: 0 !important;
                                border: 0 !important;
                                flex: 0 0 0 !important;
                            }

                            body.codex-dual-chat-enabled #chat-output-frame > .chat-output,
                            body.codex-dual-chat-enabled #chat-output-frame > #chat-event-selected,
                            body.codex-dual-chat-enabled #chat-output-frame > #chat-pinned-message {
                                left: 0 !important;
                                right: auto !important;
                                width: calc((100% - 14px) / 2) !important;
                                max-width: calc((100% - 14px) / 2) !important;
                                box-sizing: border-box;
                            }

                            #${overlayId} {
                                position: absolute;
                                inset: 0;
                                z-index: 6;
                                display: none;
                                pointer-events: auto;
                                grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
                                column-gap: 14px;
                                padding: 0 6px;
                                box-sizing: border-box;
                            }

                            #${overlayId}.enabled {
                                display: grid;
                            }

                            #${overlayId}.${splitFocusClass} .codex-split-chat-lines > * {
                                opacity: 0.3;
                            }

                            #${overlayId}.${splitFocusClass} .codex-split-chat-lines > *.${splitFocusMatchClass} {
                                opacity: 1 !important;
                            }

                            #${overlayId} .codex-split-chat-notify {
                                padding: 0.25em 0;
                                color: rgba(208, 212, 216, 0.92);
                                background: rgba(38, 40, 44, 0.96);
                                position: absolute;
                                bottom: -2.5em;
                                left: 16px;
                                right: 16px;
                                text-align: center;
                                cursor: pointer;
                                z-index: 8;
                                border-radius: 4px;
                                opacity: 0;
                                transform: translateY(0);
                                transition: all 250ms cubic-bezier(0.6, 0.08, 0.99, 0.54);
                                pointer-events: auto;
                                user-select: none;
                            }

                            #${overlayId}.${overlayUnpinnedClass} .codex-split-chat-notify {
                                opacity: 1;
                                bottom: 0;
                                transition-timing-function: cubic-bezier(0, 0.99, 0.18, 0.99);
                            }

                            #${overlayId} .codex-split-chat-notify:hover {
                                color: rgba(255, 255, 255, 0.98);
                            }

                            #${overlayId} .codex-split-chat-pane {
                                position: relative;
                                overflow: hidden;
                                min-width: 0;
                                height: 100%;
                                pointer-events: auto;
                            }

                            #${overlayId} .codex-split-chat-pane.left {
                                padding-right: 10px;
                            }

                            #${overlayId} .codex-split-chat-pane.right {
                                padding-left: 10px;
                                border-left: 1px solid rgba(255, 255, 255, 0.08);
                            }

                            #${overlayId} .codex-split-chat-lines {
                                position: absolute;
                                left: 0;
                                right: 0;
                                bottom: 0;
                                display: flex;
                                flex-direction: column;
                                transform-origin: bottom left;
                                pointer-events: auto;
                            }

                            #${overlayId} .codex-split-chat-lines > * {
                                width: 100%;
                                box-sizing: border-box;
                                pointer-events: auto;
                            }

                            #${overlayId} .codex-split-chat-lines a:hover,
                            #${overlayId} .codex-split-chat-lines a:focus,
                            #${overlayId} .codex-split-chat-lines .user:hover,
                            #${overlayId} .codex-split-chat-lines .user:focus,
                            #${overlayId} .codex-split-chat-lines .chat-user:hover,
                            #${overlayId} .codex-split-chat-lines .chat-user:focus {
                                text-decoration: underline !important;
                                text-decoration-line: underline !important;
                                text-underline-offset: 1px;
                                cursor: pointer;
                            }

                            #${overlayId} .codex-split-chat-lines .user,
                            #${overlayId} .codex-split-chat-lines .chat-user {
                                cursor: pointer;
                            }

                            #${overlayId} .codex-split-chat-lines a,
                            #${overlayId} .codex-split-chat-lines .user,
                            #${overlayId} .codex-split-chat-lines .chat-user,
                            #${overlayId} .codex-split-chat-lines .flair {
                                pointer-events: auto;
                            }

                            body.codex-split-chat-enabled #chat-output-frame > .chat-output {
                                visibility: hidden !important;
                            }

                            body.codex-split-chat-enabled #chat-output-frame > #chat-event-selected,
                            body.codex-split-chat-enabled #chat-output-frame > #chat-pinned-message {
                                visibility: hidden !important;
                            }

                            #${measureId} {
                                position: fixed;
                                left: -20000px;
                                top: 0;
                                visibility: hidden;
                                pointer-events: none;
                                overflow: visible;
                                z-index: -2147483647;
                            }

                            #${measureId} .chat-lines {
                                width: 100%;
                            }
                        `;
                        document.head.appendChild(style);
                    }

                    function ensureButton() {
                        const eyeButton = document.getElementById('chat-watching-focus-btn');
                        if (!eyeButton || !eyeButton.parentElement) {
                            return null;
                        }

                        let button = document.getElementById(buttonId);
                        if (!button) {
                            button = document.createElement('a');
                            button.id = buttonId;
                            button.className = 'chat-tool-btn';
                            button.setAttribute('role', 'button');
                            button.setAttribute('aria-label', 'Split Chat');
                            button.setAttribute('title', 'Split Chat');
                            button.setAttribute('data-tippy-content', 'Split Chat');
                            button.innerHTML = '<i class=""btn-icon""></i>';
                            button.addEventListener('click', (event) => {
                                event.preventDefault();
                                event.stopPropagation();
                                const nextEnabled = !splitEnabled;
                                if (nextEnabled && dualChatEnabled) {
                                    setDualChatEnabled(false, false);
                                }
                                setSplitEnabled(nextEnabled, true);
                            });
                        }

                        if (button.parentElement !== eyeButton.parentElement || button.nextElementSibling !== eyeButton) {
                            if (button._tippy && typeof button._tippy.destroy === 'function') {
                                try {
                                    button._tippy.destroy();
                                } catch (error) {
                                }
                            }

                            eyeButton.parentElement.insertBefore(button, eyeButton);
                        }

                        button.setAttribute('aria-label', 'Split Chat');
                        button.setAttribute('title', 'Split Chat');
                        button.setAttribute('data-tippy-content', 'Split Chat');
                        if (button._tippy && typeof button._tippy.setContent === 'function') {
                            button._tippy.setContent('Split Chat');
                        }

                        return button;
                    }

                    function ensureDualButton() {
                        const eyeButton = document.getElementById('chat-watching-focus-btn');
                        const settingsButton = document.getElementById('chat-settings-btn');
                        const anchorBefore = eyeButton || settingsButton;
                        if (!anchorBefore || !anchorBefore.parentElement) {
                            return null;
                        }

                        const toolbarRoot = anchorBefore.parentElement;

                        const staleSteamButton = document.getElementById('codex-steam-chat-btn');
                        if (staleSteamButton && staleSteamButton.parentElement) {
                            staleSteamButton.parentElement.removeChild(staleSteamButton);
                        }
                        const staleDualButton = document.getElementById('codex-dual-stream-chat-btn');
                        if (staleDualButton && staleDualButton.parentElement) {
                            staleDualButton.parentElement.removeChild(staleDualButton);
                        }

                        let btn = document.getElementById(dualButtonId);
                        if (!btn) {
                            btn = document.createElement('a');
                            btn.id = dualButtonId;
                            btn.className = 'chat-tool-btn';
                            btn.setAttribute('role', 'button');
                            btn.setAttribute('aria-label', 'Stream Chat');
                            btn.setAttribute('title', 'Stream Chat');
                            btn.setAttribute('data-tippy-content', 'Stream Chat');
                            btn.innerHTML = '<i class=""btn-icon""></i>';
                            btn.addEventListener('click', (event) => {
                                event.preventDefault();
                                event.stopPropagation();
                                setDualChatEnabled(!dualChatEnabled, true);
                            });
                        }

                        // Place only relative to the native focus/eye control — never relative to split chat.
                        // That avoids reorder bugs when Tippy or the site injects nodes between toolbar buttons.
                        const placementOk = btn.parentElement === toolbarRoot
                            && btn.nextElementSibling === anchorBefore;
                        if (!placementOk) {
                            if (btn._tippy && typeof btn._tippy.destroy === 'function') {
                                try {
                                    btn._tippy.destroy();
                                } catch (error) {
                                }
                            }

                            toolbarRoot.insertBefore(btn, anchorBefore);
                        }

                        btn.setAttribute('aria-label', 'Stream Chat');
                        btn.setAttribute('title', 'Stream Chat');
                        btn.setAttribute('data-tippy-content', 'Stream Chat');
                        if (btn._tippy && typeof btn._tippy.setContent === 'function') {
                            btn._tippy.setContent('Stream Chat');
                        }

                        return btn;
                    }

                    function ensureToolbarHandlers() {
                        if (toolbarHandlersBound) {
                            return;
                        }

                        toolbarHandlersBound = true;
                    }

                    function ensureOverlay() {
                        const frame = document.getElementById('chat-output-frame');
                        if (!frame) {
                            return null;
                        }

                        let overlay = document.getElementById(overlayId);
                        if (!overlay) {
                            overlay = document.createElement('div');
                            overlay.id = overlayId;
                            overlay.setAttribute('aria-hidden', 'true');
                            overlay.innerHTML =
                                '<div class=""codex-split-chat-pane left""><div class=""codex-split-chat-lines""></div></div>' +
                                '<div class=""codex-split-chat-pane right""><div class=""codex-split-chat-lines""></div></div>' +
                                '<div class=""codex-split-chat-notify"">More messages below</div>';
                            frame.appendChild(overlay);
                            const notify = overlay.querySelector('.codex-split-chat-notify');
                            if (notify) {
                                notify.addEventListener('click', (event) => {
                                    event.preventDefault();
                                    event.stopPropagation();
                                    splitScrollOffset = 0;
                                    splitViewportAnchor = null;
                                    queueRender();
                                });
                            }

                            overlay.addEventListener('mouseover', (event) => {
                                if (!(event.target instanceof Element)) {
                                    return;
                                }

                                const hoverTarget = event.target.closest('.user, .chat-user');
                                if (hoverTarget instanceof HTMLElement) {
                                    hoverTarget.style.setProperty('text-decoration', 'underline', 'important');
                                    hoverTarget.style.textUnderlineOffset = '1px';
                                    hoverTarget.style.cursor = 'pointer';
                                }
                            });

                            overlay.addEventListener('mouseout', (event) => {
                                if (!(event.target instanceof Element)) {
                                    return;
                                }

                                const hoverTarget = event.target.closest('.user, .chat-user');
                                if (hoverTarget instanceof HTMLElement) {
                                    hoverTarget.style.removeProperty('text-decoration');
                                    hoverTarget.style.textUnderlineOffset = '';
                                }
                            });

                            overlay.addEventListener('click', handleOverlayClick, true);
                            overlay.addEventListener('contextmenu', handleOverlayContextMenu, true);
                        } else if (overlay.parentElement !== frame) {
                            frame.appendChild(overlay);
                        }

                        return overlay;
                    }

                    function ensureMeasureLines(sourceOutput, width) {
                        if (!document.body) {
                            return null;
                        }

                        let measureRoot = document.getElementById(measureId);
                        if (!measureRoot) {
                            measureRoot = document.createElement('div');
                            measureRoot.id = measureId;
                            document.body.appendChild(measureRoot);
                        }

                        measureRoot.innerHTML = '';

                        const measureOutput = document.createElement('div');
                        measureOutput.className = sourceOutput ? sourceOutput.className : 'chat-output';
                        measureOutput.style.display = 'block';
                        measureOutput.style.position = 'absolute';
                        measureOutput.style.left = '0';
                        measureOutput.style.top = '0';
                        measureOutput.style.width = `${Math.max(120, Math.round(width))}px`;

                        const measureLines = document.createElement('div');
                        measureLines.className = 'chat-lines';
                        measureOutput.appendChild(measureLines);
                        measureRoot.appendChild(measureOutput);
                        return measureLines;
                    }

                    function getActiveOutput() {
                        const outputs = Array.from(document.querySelectorAll('#chat-output-frame .chat-output'));
                        return outputs.find((output) => {
                            const style = window.getComputedStyle(output);
                            return style.display !== 'none';
                        }) || null;
                    }

                    function measureNodeHeight(measureLines, sourceNode) {
                        if (!measureLines || !sourceNode) {
                            return 0;
                        }

                        const clone = sourceNode.cloneNode(true);
                        measureLines.appendChild(clone);
                        const style = window.getComputedStyle(clone);
                        const marginTop = parseFloat(style.marginTop || '0') || 0;
                        const marginBottom = parseFloat(style.marginBottom || '0') || 0;
                        const height = Math.ceil(clone.getBoundingClientRect().height + marginTop + marginBottom);
                        clone.remove();
                        return Math.max(1, height);
                    }

                    function buildMessageTextSignature(messageElement) {
                        if (!(messageElement instanceof Element)) {
                            return '';
                        }

                        return ((messageElement.querySelector('.text')?.textContent) || '')
                            .replace(/\s+/g, ' ')
                            .trim()
                            .slice(0, 160);
                    }

                    function buildRenderKey(sourceNode, sourceIndex) {
                        if (!(sourceNode instanceof Element)) {
                            return `idx:${sourceIndex}`;
                        }

                        const sourceId = sourceNode.getAttribute('data-id') || '';
                        if (sourceId) {
                            return `id:${sourceId}`;
                        }

                        return `idx:${sourceIndex}|user:${sourceNode.getAttribute('data-username') || ''}|time:${sourceNode.querySelector('time')?.getAttribute('data-unixtimestamp') || ''}|text:${buildMessageTextSignature(sourceNode)}`;
                    }

                    function buildRenderSignature(sourceNode) {
                        if (!(sourceNode instanceof Element)) {
                            return '';
                        }

                        return [
                            sourceNode.className || '',
                            sourceNode.getAttribute('data-mentioned') || '',
                            sourceNode.getAttribute('data-giftee') || '',
                            sourceNode.getAttribute('data-username') || '',
                            sourceNode.innerHTML || ''
                        ].join('|');
                    }

                    function stampCloneMetadata(clone, sourceNode, sourceIndex) {
                        if (!(clone instanceof Element) || !(sourceNode instanceof Element)) {
                            return clone;
                        }

                        clone.setAttribute('data-codex-render-key', buildRenderKey(sourceNode, sourceIndex));
                        clone.setAttribute('data-codex-render-signature', buildRenderSignature(sourceNode));
                        clone.setAttribute('data-codex-source-index', String(sourceIndex));
                        clone.setAttribute('data-codex-source-id', sourceNode.getAttribute('data-id') || '');
                        clone.setAttribute('data-codex-source-username', sourceNode.getAttribute('data-username') || '');
                        clone.setAttribute('data-codex-source-time', sourceNode.querySelector('time')?.getAttribute('data-unixtimestamp') || '');
                        clone.setAttribute('data-codex-source-text', buildMessageTextSignature(sourceNode));
                        return clone;
                    }

                    function cloneMeasuredNode(sourceNode, sourceIndex) {
                        const clone = sourceNode.cloneNode(true);
                        return stampCloneMetadata(clone, sourceNode, sourceIndex);
                    }

                    function renderPane(target, items) {
                        if (!target) {
                            return;
                        }

                        const existingByKey = new Map();
                        Array.from(target.children).forEach((node) => {
                            if (node instanceof Element) {
                                const key = node.getAttribute('data-codex-render-key') || '';
                                if (key) {
                                    existingByKey.set(key, node);
                                }
                            }
                        });

                        let cursor = target.firstElementChild;
                        items.forEach((item) => {
                            const key = buildRenderKey(item.sourceNode, item.sourceIndex);
                            const signature = buildRenderSignature(item.sourceNode);
                            let node = existingByKey.get(key) || null;
                            existingByKey.delete(key);

                            if (node && (node.getAttribute('data-codex-render-signature') || '') !== signature) {
                                const replacement = cloneMeasuredNode(item.sourceNode, item.sourceIndex);
                                if (node === cursor) {
                                    cursor = cursor ? cursor.nextElementSibling : null;
                                }
                                target.insertBefore(replacement, node);
                                node.remove();
                                node = replacement;
                            } else if (!node) {
                                node = cloneMeasuredNode(item.sourceNode, item.sourceIndex);
                            } else {
                                stampCloneMetadata(node, item.sourceNode, item.sourceIndex);
                            }

                            bindOverlayNodeInteractions(node);

                            if (node === cursor) {
                                cursor = cursor ? cursor.nextElementSibling : null;
                            } else {
                                target.insertBefore(node, cursor);
                            }
                        });

                        existingByKey.forEach((node) => {
                            if (node === cursor) {
                                cursor = cursor ? cursor.nextElementSibling : null;
                            }
                            node.remove();
                        });

                        while (cursor) {
                            const next = cursor.nextElementSibling;
                            cursor.remove();
                            cursor = next;
                        }
                    }

                    function bindOverlayNodeInteractions(rootNode) {
                        // All interaction is handled via event delegation on the overlay itself.
                        // No per-element binding needed.
                    }

                    function normalizeWheelDelta(event, paneHeight) {
                        let delta = event.deltaY || 0;
                        if (event.deltaMode === 1) {
                            delta *= 18;
                        } else if (event.deltaMode === 2) {
                            delta *= Math.max(40, paneHeight * 0.85);
                        }

                        return delta;
                    }

                    function normalizeFocusValue(value) {
                        return (value || '')
                            .replace(/\s+/g, ' ')
                            .trim()
                            .toLowerCase();
                    }

                    function hasSplitFocus() {
                        return splitFocusFilter !== null;
                    }

                    function matchesSplitFocus(message, filter) {
                        if (!(message instanceof Element) || !filter || !filter.value) {
                            return false;
                        }

                        if (filter.type === 'flair') {
                            return message.classList.contains(filter.value);
                        }

                        const username = normalizeFocusValue(message.getAttribute('data-username') || '');
                        if (username && username === filter.value) {
                            return true;
                        }

                        const giftee = normalizeFocusValue(message.getAttribute('data-giftee') || '');
                        if (giftee && giftee === filter.value) {
                            return true;
                        }

                        const mentioned = normalizeFocusValue(message.getAttribute('data-mentioned') || '');
                        if (mentioned) {
                            const mentionedList = mentioned.split(/\s+/);
                            if (mentionedList.includes(filter.value)) {
                                return true;
                            }
                        }

                        const text = normalizeFocusValue(message.textContent || '');
                        return text.indexOf(filter.value) !== -1;
                    }

                    function toggleSplitFocusFilter(filter) {
                        if (!filter || !filter.value) {
                            splitFocusFilter = null;
                            return;
                        }

                        if (splitFocusFilter &&
                            splitFocusFilter.type === filter.type &&
                            splitFocusFilter.value === filter.value) {
                            splitFocusFilter = null;
                            return;
                        }

                        splitFocusFilter = filter;
                    }

                    function applySplitFocusState() {
                        const overlay = document.getElementById(overlayId);
                        if (!overlay) {
                            return;
                        }

                        const focused = hasSplitFocus();
                        overlay.classList.toggle(splitFocusClass, focused);
                        overlay.classList.toggle('focus', focused);
                        overlay.querySelectorAll('.codex-split-chat-lines > *').forEach((message) => {
                            const isMatch = !focused || matchesSplitFocus(message, splitFocusFilter);
                            message.classList.toggle(splitFocusMatchClass, isMatch);
                            message.style.opacity = focused ? (isMatch ? '1' : '0.3') : '';
                        });
                    }

                    function clearSplitFocus() {
                        splitFocusFilter = null;
                        applySplitFocusState();
                    }

                    function collectVisibleItems(messages, measureLines, capacityHeight) {
                        const visibleItems = [];
                        let totalHeight = 0;

                        for (let index = messages.length - 1; index >= 0; index -= 1) {
                            const sourceNode = messages[index];
                            const nodeHeight = measureNodeHeight(measureLines, sourceNode);
                            visibleItems.push({
                                sourceNode: sourceNode,
                                nodeHeight: nodeHeight,
                                sourceIndex: index
                            });
                            totalHeight += nodeHeight;

                            if (visibleItems.length > 1 && totalHeight >= capacityHeight) {
                                break;
                            }
                        }

                        visibleItems.reverse();
                        return {
                            items: visibleItems,
                            totalHeight: totalHeight
                        };
                    }

                    function buildViewportAnchor(item, offsetWithinMessage) {
                        if (!item || !(item.sourceNode instanceof Element)) {
                            return null;
                        }

                        const sourceNode = item.sourceNode;
                        return {
                            sourceNode: sourceNode,
                            sourceId: sourceNode.getAttribute('data-id') || '',
                            sourceIndex: item.sourceIndex,
                            sourceUsername: (sourceNode.getAttribute('data-username') || '').toLowerCase(),
                            sourceTime: sourceNode.querySelector('time')?.getAttribute('data-unixtimestamp') || '',
                            sourceText: buildMessageTextSignature(sourceNode),
                            offsetWithinMessage: Math.max(0, Math.min(item.nodeHeight - 1, Math.round(offsetWithinMessage || 0)))
                        };
                    }

                    function captureViewportAnchor(collected, paneHeight, scrollOffset) {
                        if (!collected || !Array.isArray(collected.items) || !collected.items.length || paneHeight < 1) {
                            return null;
                        }

                        const fullCanvasHeight = paneHeight * 2;
                        const viewportTop = Math.max(0, collected.totalHeight - fullCanvasHeight - scrollOffset);
                        let cumulative = 0;
                        for (let index = 0; index < collected.items.length; index += 1) {
                            const item = collected.items[index];
                            const next = cumulative + item.nodeHeight;
                            if (viewportTop < next || index === collected.items.length - 1) {
                                return buildViewportAnchor(item, viewportTop - cumulative);
                            }
                            cumulative = next;
                        }

                        return null;
                    }

                    function findViewportTopFromAnchor(collected, anchor) {
                        if (!collected || !Array.isArray(collected.items) || !collected.items.length || !anchor) {
                            return null;
                        }

                        const anchorText = anchor.sourceText || '';
                        const anchorUsername = (anchor.sourceUsername || '').toLowerCase();
                        const anchorTime = anchor.sourceTime || '';
                        let cumulative = 0;

                        for (const item of collected.items) {
                            const sourceNode = item.sourceNode;
                            if (!(sourceNode instanceof Element)) {
                                cumulative += item.nodeHeight;
                                continue;
                            }

                            const itemId = sourceNode.getAttribute('data-id') || '';
                            const itemUsername = (sourceNode.getAttribute('data-username') || '').toLowerCase();
                            const itemTime = sourceNode.querySelector('time')?.getAttribute('data-unixtimestamp') || '';
                            const itemText = buildMessageTextSignature(sourceNode);
                            const directNodeMatch = anchor.sourceNode && sourceNode === anchor.sourceNode;

                            const idMatch = anchor.sourceId && itemId === anchor.sourceId;
                            const signatureMatch =
                                (!!anchorUsername && itemUsername === anchorUsername) &&
                                (!anchorTime || itemTime === anchorTime) &&
                                (!anchorText || itemText === anchorText);

                            if (directNodeMatch || idMatch || signatureMatch) {
                                const offsetWithinMessage = Math.max(0, Math.min(item.nodeHeight - 1, anchor.offsetWithinMessage || 0));
                                return cumulative + offsetWithinMessage;
                            }

                            cumulative += item.nodeHeight;
                        }

                        return null;
                    }

                    function clearSplitOverlay() {
                        const overlay = document.getElementById(overlayId);
                        if (!overlay) {
                            return;
                        }

                        splitViewportAnchor = null;
                        overlay.classList.toggle(splitFocusClass, hasSplitFocus());
                        overlay.classList.toggle('focus', hasSplitFocus());
                        overlay.classList.remove(overlayUnpinnedClass);
                        const panes = overlay.querySelectorAll('.codex-split-chat-lines');
                        panes.forEach((pane) => {
                            pane.textContent = '';
                            pane.style.transform = 'translateY(0px)';
                        });
                    }

                    function updateScrollNotify(overlay) {
                        if (!overlay) {
                            return;
                        }

                        overlay.classList.toggle(overlayUnpinnedClass, splitScrollOffset > 2);
                    }

                    function updateSplitTransforms() {
                        const overlay = document.getElementById(overlayId);
                        if (!overlay) {
                            return;
                        }

                        const rightTarget = overlay.querySelector('.codex-split-chat-pane.right .codex-split-chat-lines');
                        const leftTarget = overlay.querySelector('.codex-split-chat-pane.left .codex-split-chat-lines');
                        if (rightTarget) {
                            rightTarget.style.transform = `translateY(${Math.round(splitScrollOffset)}px)`;
                        }
                        if (leftTarget) {
                            leftTarget.style.transform = `translateY(${Math.round(lastPaneHeight + splitScrollOffset)}px)`;
                        }
                        updateScrollNotify(overlay);
                    }

                    function getSourceMessages() {
                        const sourceOutput = getActiveOutput();
                        const sourceLines = sourceOutput && sourceOutput.querySelector('.chat-lines');
                        if (!sourceLines) {
                            return [];
                        }

                        return Array.from(sourceLines.children).filter((node) => node instanceof HTMLElement);
                    }

                    function getSourceMessageByIndex(sourceIndex) {
                        const normalizedIndex = parseInt(sourceIndex || '-1', 10);
                        if (Number.isNaN(normalizedIndex) || normalizedIndex < 0) {
                            return null;
                        }

                        const messages = getSourceMessages();
                        return messages[normalizedIndex] || null;
                    }

                    function findOriginalMessage(cloneMessage) {
                        if (!(cloneMessage instanceof Element)) {
                            return null;
                        }

                        const messages = getSourceMessages();
                        if (!messages.length) {
                            return null;
                        }

                        const sourceId = cloneMessage.getAttribute('data-codex-source-id') || '';
                        if (sourceId) {
                            const idMatch = messages.find((message) => (message.getAttribute('data-id') || '') === sourceId);
                            if (idMatch) {
                                return idMatch;
                            }
                        }

                        const sourceUsername = (cloneMessage.getAttribute('data-codex-source-username') || '').toLowerCase();
                        const sourceTime = cloneMessage.getAttribute('data-codex-source-time') || '';
                        const sourceText = cloneMessage.getAttribute('data-codex-source-text') || '';

                        let candidates = messages;
                        if (sourceUsername) {
                            const usernameMatches = candidates.filter((message) =>
                                ((message.getAttribute('data-username') || '').toLowerCase() === sourceUsername));
                            if (usernameMatches.length) {
                                candidates = usernameMatches;
                            }
                        }

                        if (sourceTime) {
                            const timeMatches = candidates.filter((message) =>
                                ((message.querySelector('time')?.getAttribute('data-unixtimestamp')) || '') === sourceTime);
                            if (timeMatches.length) {
                                candidates = timeMatches;
                            }
                        }

                        if (sourceText) {
                            const textMatches = candidates.filter((message) => buildMessageTextSignature(message) === sourceText);
                            if (textMatches.length) {
                                candidates = textMatches;
                            }
                        }

                        if (candidates.length) {
                            return candidates[candidates.length - 1];
                        }

                        return getSourceMessageByIndex(cloneMessage.getAttribute('data-codex-source-index'));
                    }

                    function findOriginalInteractiveTarget(overlayTarget) {
                        if (!(overlayTarget instanceof Element)) {
                            return null;
                        }

                        const interactive = overlayTarget.closest('.user, .chat-user, .flair, a');
                        const cloneMessage = overlayTarget.closest('[data-codex-source-index]');
                        if (!interactive || !cloneMessage) {
                            return null;
                        }

                        const originalMessage = findOriginalMessage(cloneMessage);
                        if (!originalMessage) {
                            return null;
                        }

                        if (interactive.classList.contains('user')) {
                            const sourceUsername = (cloneMessage.getAttribute('data-codex-source-username') || '').toLowerCase();
                            const originalUsers = Array.from(originalMessage.querySelectorAll('.user'));
                            if (sourceUsername) {
                                const usernameMatch = originalUsers.find((node) =>
                                    ((node.textContent || '').trim().toLowerCase() === sourceUsername));
                                if (usernameMatch) {
                                    return usernameMatch;
                                }
                            }

                            const clickedText = ((interactive.textContent || '').trim().toLowerCase());
                            const textMatch = originalUsers.find((node) =>
                                ((node.textContent || '').trim().toLowerCase() === clickedText));
                            return textMatch || originalUsers[0] || null;
                        }

                        if (interactive.classList.contains('chat-user')) {
                            const cloneMatches = Array.from(cloneMessage.querySelectorAll('.chat-user'));
                            const originalMatches = Array.from(originalMessage.querySelectorAll('.chat-user'));
                            const ordinal = cloneMatches.indexOf(interactive);
                            if (ordinal >= 0 && ordinal < originalMatches.length) {
                                return originalMatches[ordinal];
                            }

                            const clickedText = normalizeFocusValue(interactive.textContent || '');
                            return originalMatches.find((node) =>
                                normalizeFocusValue(node.textContent || '') === clickedText) || originalMatches[0] || null;
                        }

                        if (interactive.classList.contains('flair')) {
                            const cloneMatches = Array.from(cloneMessage.querySelectorAll('.flair'));
                            const originalMatches = Array.from(originalMessage.querySelectorAll('.flair'));
                            const ordinal = cloneMatches.indexOf(interactive);
                            if (ordinal >= 0 && ordinal < originalMatches.length) {
                                return originalMatches[ordinal];
                            }

                            const flairName = interactive.getAttribute('data-flair') || '';
                            return originalMatches.find((node) =>
                                (node.getAttribute('data-flair') || '') === flairName) || originalMatches[0] || null;
                        }

                        const selector = 'a';
                        const cloneMatches = Array.from(cloneMessage.querySelectorAll(selector));
                        const originalMatches = Array.from(originalMessage.querySelectorAll(selector));
                        const ordinal = cloneMatches.indexOf(interactive);
                        if (ordinal >= 0 && ordinal < originalMatches.length) {
                            return originalMatches[ordinal];
                        }

                        const href = interactive.getAttribute('href') || interactive.href || '';
                        return originalMatches.find((node) => (node.getAttribute('href') || node.href || '') === href) || null;
                    }

                    function dispatchMouseEventToOriginal(target, sourceEvent, eventType, button) {
                        if (!target) {
                            return;
                        }

                        const mouseEvent = new MouseEvent(eventType, {
                            bubbles: true,
                            cancelable: true,
                            composed: true,
                            view: window,
                            clientX: sourceEvent.clientX,
                            clientY: sourceEvent.clientY,
                            screenX: sourceEvent.screenX,
                            screenY: sourceEvent.screenY,
                            ctrlKey: !!sourceEvent.ctrlKey,
                            shiftKey: !!sourceEvent.shiftKey,
                            altKey: !!sourceEvent.altKey,
                            metaKey: !!sourceEvent.metaKey,
                            button: button,
                            buttons: button === 2 ? 2 : 1,
                            detail: 1
                        });
                        target.dispatchEvent(mouseEvent);
                    }

                    function dispatchContextMenuSequenceToOriginal(target, sourceEvent) {
                        if (!target) {
                            return;
                        }

                        const messageEl = target.closest('.msg-chat');
                        if (messageEl) {
                            messageEl.style.visibility = 'visible';
                            messageEl.style.position = 'fixed';
                            messageEl.style.left = '-9999px';
                            messageEl.style.top = '-9999px';
                        }

                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseenter', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseover', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mousedown', 2);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseup', 2);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'contextmenu', 2);

                        if (messageEl) {
                            window.setTimeout(() => {
                                messageEl.style.visibility = '';
                                messageEl.style.position = '';
                                messageEl.style.left = '';
                                messageEl.style.top = '';
                            }, 100);
                        }
                    }

                    function dispatchClickSequenceToOriginal(target, sourceEvent) {
                        if (!target) {
                            return;
                        }

                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseenter', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseover', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mousedown', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'mouseup', 0);
                        dispatchMouseEventToOriginal(target, sourceEvent, 'click', 0);
                    }

                    function handleOverlayClick(event) {
                        if (!splitEnabled || !(event.target instanceof Element)) {
                            return;
                        }

                        event.preventDefault();
                        event.stopPropagation();

                        const clickedCensored = event.target.closest('.censored');
                        if (clickedCensored) {
                            const cloneMessage = clickedCensored.closest('[data-codex-source-index]');
                            const originalMessage = cloneMessage ? findOriginalMessage(cloneMessage) : null;
                            if (originalMessage && originalMessage.classList.contains('censored')) {
                                dispatchClickSequenceToOriginal(originalMessage, event);
                                window.setTimeout(queueRender, 0);
                            }
                            return;
                        }

                        const clickedMention = event.target.closest('.chat-user');
                        if (clickedMention) {
                            const mentionValue = normalizeFocusValue(clickedMention.textContent || '');
                            toggleSplitFocusFilter({ type: 'name', value: mentionValue });
                            applySplitFocusState();
                            return;
                        }

                        const clickedUser = event.target.closest('.user');
                        if (clickedUser && !clickedUser.classList.contains('tier')) {
                            const userValue = normalizeFocusValue(clickedUser.textContent || '');
                            toggleSplitFocusFilter({ type: 'name', value: userValue });
                            applySplitFocusState();
                            return;
                        }

                        const clickedFlair = event.target.closest('.flair');
                        if (clickedFlair) {
                            const flairName = clickedFlair.getAttribute('data-flair') || '';
                            if (flairName) {
                                toggleSplitFocusFilter({ type: 'flair', value: flairName });
                                applySplitFocusState();
                                return;
                            }
                        }

                        const clickedLink = event.target.closest('a');
                        if (clickedLink) {
                            const href = clickedLink.getAttribute('href') || '';
                            if (href && href !== '#') {
                                window.open(href, '_blank', 'noopener');
                            }
                            return;
                        }

                        if (hasSplitFocus()) {
                            clearSplitFocus();
                        }
                    }

                    function handleOverlayContextMenu(event) {
                        if (!splitEnabled || !(event.target instanceof Element)) {
                            return;
                        }

                        const clickedUser = event.target.closest('.user, .chat-user');
                        if (!clickedUser) {
                            return;
                        }

                        event.preventDefault();
                        event.stopPropagation();

                        const target = findOriginalInteractiveTarget(clickedUser);
                        if (target) {
                            dispatchContextMenuSequenceToOriginal(target, event);
                        }
                    }

                    function bindFrameObservers() {
                        const frame = document.getElementById('chat-output-frame');
                        if (!frame) {
                            return;
                        }

                        if (frameObserver) {
                            frameObserver.disconnect();
                        }

                        frameObserver = new MutationObserver((mutations) => {
                            if (!splitEnabled) {
                                return;
                            }

                            const overlayEl = document.getElementById(overlayId);
                            const hasSourceChange = mutations.some((m) => {
                                if (overlayEl && (m.target === overlayEl || overlayEl.contains(m.target))) {
                                    return false;
                                }
                                return true;
                            });

                            if (hasSourceChange) {
                                if (splitScrollOffset > 0) {
                                    return;
                                }
                                queueRender();
                            }
                        });
                        frameObserver.observe(frame, {
                            childList: true,
                            subtree: true,
                            attributes: true,
                            attributeFilter: ['class', 'style']
                        });

                        frame.addEventListener('scroll', () => {
                            if (!splitEnabled) {
                                return;
                            }

                            if (splitScrollOffset > 0) {
                                updateSplitTransforms();
                                return;
                            }

                            queueRender();
                        }, true);
                        frame.addEventListener('wheel', (event) => {
                            if (!splitEnabled) {
                                return;
                            }

                            const delta = normalizeWheelDelta(event, lastPaneHeight || frame.clientHeight || 240);
                            if (Math.abs(delta) < 0.5) {
                                return;
                            }

                            event.preventDefault();
                            event.stopPropagation();

                            const nextOffset = Math.max(0, Math.min(lastMaxScroll, splitScrollOffset - delta));
                            if (Math.abs(nextOffset - splitScrollOffset) < 0.5) {
                                return;
                            }
                            const wasUnpinned = splitScrollOffset > 0.5;
                            splitScrollOffset = nextOffset;
                            splitViewportAnchor = splitScrollOffset > 0
                                ? captureViewportAnchor({ items: lastCollectedItems, totalHeight: lastRenderedTotalHeight }, lastPaneHeight || frame.clientHeight || 240, splitScrollOffset)
                                : null;
                            if (wasUnpinned && splitScrollOffset <= 0.5) {
                                queueRender();
                            } else {
                                updateSplitTransforms();
                            }
                        }, { passive: false });

                        if (frameResizeObserver) {
                            frameResizeObserver.disconnect();
                        }

                        frameResizeObserver = new ResizeObserver(() => {
                            if (splitEnabled) {
                                queueRender();
                            }
                        });
                        frameResizeObserver.observe(frame);

                        window.addEventListener('resize', queueRender);
                    }

                    function bindSourceObserver(sourceLines) {
                        if (!sourceLines || observedSource === sourceLines) {
                            return;
                        }

                        observedSource = sourceLines;

                        if (sourceResizeObserver) {
                            sourceResizeObserver.disconnect();
                        }

                        sourceResizeObserver = new ResizeObserver(() => {
                            if (splitEnabled) {
                                if (splitScrollOffset > 0) {
                                    return;
                                }
                                queueRender();
                            }
                        });
                        sourceResizeObserver.observe(sourceLines);
                    }

                    function queueRender() {
                        if (!splitEnabled || renderToken) {
                            return;
                        }

                        renderToken = window.requestAnimationFrame(() => {
                            renderToken = 0;
                            renderSplitChat();
                        });
                    }

                    function renderSplitChat() {
                        const overlay = ensureOverlay();
                        const sourceOutput = getActiveOutput();
                        const sourceLines = sourceOutput && sourceOutput.querySelector('.chat-lines');
                        if (!overlay || !sourceOutput || !sourceLines) {
                            clearSplitOverlay();
                            return;
                        }

                        bindSourceObserver(sourceLines);

                        const rightPane = overlay.querySelector('.codex-split-chat-pane.right');
                        const leftPane = overlay.querySelector('.codex-split-chat-pane.left');
                        const rightTarget = overlay.querySelector('.codex-split-chat-pane.right .codex-split-chat-lines');
                        const leftTarget = overlay.querySelector('.codex-split-chat-pane.left .codex-split-chat-lines');
                        if (!rightPane || !leftPane || !rightTarget || !leftTarget) {
                            return;
                        }

                        overlay.classList.toggle(splitFocusClass, hasSplitFocus());
                        overlay.classList.toggle('focus', hasSplitFocus());

                        const paneHeight = Math.max(leftPane.clientHeight, rightPane.clientHeight);
                        const paneWidth = Math.max(leftPane.clientWidth, rightPane.clientWidth);
                        if (paneHeight < 8 || paneWidth < 40) {
                            clearSplitOverlay();
                            return;
                        }

                        lastPaneHeight = paneHeight;

                        const measureLines = ensureMeasureLines(sourceOutput, paneWidth);
                        if (!measureLines) {
                            return;
                        }

                        const messages = Array.from(sourceLines.children).filter((node) => node instanceof HTMLElement);
                        if (messages.length === 0) {
                            clearSplitOverlay();
                            return;
                        }

                        const collected = collectVisibleItems(messages, measureLines, Number.POSITIVE_INFINITY);
                        if (!collected.items.length) {
                            clearSplitOverlay();
                            return;
                        }

                        const fullCanvasHeight = paneHeight * 2;
                        if (splitScrollOffset > 0) {
                            const anchoredViewportTop = findViewportTopFromAnchor(collected, splitViewportAnchor);
                            if (anchoredViewportTop !== null) {
                                splitScrollOffset = collected.totalHeight - fullCanvasHeight - anchoredViewportTop;
                            } else if (lastRenderedTotalHeight > 0 && collected.totalHeight > lastRenderedTotalHeight) {
                                splitScrollOffset += collected.totalHeight - lastRenderedTotalHeight;
                            }
                        }

                        lastMaxScroll = Math.max(0, collected.totalHeight - fullCanvasHeight);
                        splitScrollOffset = Math.max(0, Math.min(lastMaxScroll, splitScrollOffset));
                        lastRenderedTotalHeight = collected.totalHeight;
                        lastRenderedMessageCount = messages.length;
                        lastCollectedItems = collected.items.slice();
                        splitViewportAnchor = splitScrollOffset > 0
                            ? captureViewportAnchor(collected, paneHeight, splitScrollOffset)
                            : null;

                        renderPane(leftTarget, collected.items);
                        renderPane(rightTarget, collected.items);
                        applySplitFocusState();
                        updateSplitTransforms();
                    }

                    function setSplitEnabled(enabled, persist) {
                        splitEnabled = !!enabled;
                        const button = ensureButton();
                        const overlay = ensureOverlay();
                        const body = document.body;
                        if (button) {
                            button.classList.toggle('codex-split-chat-active', splitEnabled);
                            const icon = button.querySelector('.btn-icon');
                            if (icon) {
                                icon.classList.toggle('active', splitEnabled);
                            }
                        }

                        if (overlay) {
                            overlay.classList.toggle('enabled', splitEnabled);
                        }

                        if (body) {
                            body.classList.toggle('codex-split-chat-enabled', splitEnabled);
                        }

                        if (persist) {
                            try {
                                window.localStorage.setItem(storageKey, splitEnabled ? '1' : '0');
                            } catch (error) {
                            }
                        }

                        if (splitEnabled) {
                            splitScrollOffset = 0;
                            splitViewportAnchor = null;
                            lastCollectedItems = [];
                            splitFocusFilter = null;
                            lastRenderedTotalHeight = 0;
                            lastRenderedMessageCount = 0;
                            lastMaxScroll = 0;
                            queueRender();
                        } else {
                            splitScrollOffset = 0;
                            splitViewportAnchor = null;
                            lastCollectedItems = [];
                            splitFocusFilter = null;
                            lastRenderedTotalHeight = 0;
                            lastRenderedMessageCount = 0;
                            lastMaxScroll = 0;
                            clearSplitOverlay();
                        }
                    }

                    function applySavedState() {
                        let savedState = '0';
                        try {
                            savedState = window.localStorage.getItem(storageKey) || '0';
                        } catch (error) {
                        }

                        setSplitEnabled(savedState === '1', false);
                    }

                    function normalizeDualChatPayload(payload) {
                        if (!payload) {
                            return null;
                        }

                        if (typeof payload === 'string') {
                            try {
                                payload = JSON.parse(payload);
                            } catch (error) {
                                return null;
                            }
                        }

                        if (!payload || (payload.type !== 'codex-stream-chat-source' && payload.type !== 'codex-dual-chat-source')) {
                            return null;
                        }

                        return payload;
                    }

                    function ensureDualChatMessageBridge() {
                        if (dualChatHostBound) {
                            return;
                        }

                        dualChatHostBound = true;

                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.addEventListener === 'function') {
                            host.addEventListener('message', event => {
                                const payload = normalizeDualChatPayload(event && event.data);
                                if (!payload) {
                                    return;
                                }

                                dualChatSource = payload;
                                updateDualChatPane();
                            });
                        }

                        window.addEventListener('message', event => {
                            const payload = normalizeDualChatPayload(event && event.data);
                            if (!payload) {
                                return;
                            }

                            dualChatSource = payload;
                            updateDualChatPane();
                        });
                    }

                    function reportEmbedPanelToHost() {
                        const host = window.chrome && window.chrome.webview;
                        if (!host || typeof host.postMessage !== 'function') {
                            return;
                        }

                        function normalizeHref(rawValue) {
                            if (!rawValue) {
                                return null;
                            }

                            try {
                                return new URL(rawValue, window.location.href).href;
                            } catch (error) {
                                return null;
                            }
                        }

                        const anchors = Array.from(document.querySelectorAll(
                            '#chat-panel-embeds a.embed-link, .chat-embeds a.embed-link, #embeds-panel a.embed-link'));
                        const items = anchors.map((anchor) => {
                            const textElement = anchor.querySelector('.embed-link__text');
                            const href = normalizeHref(anchor.getAttribute('href') || anchor.href);
                            if (!href) {
                                return null;
                            }

                            return {
                                url: href,
                                platform: anchor.getAttribute('data-platform') || '',
                                mediaId: anchor.getAttribute('data-id') || anchor.getAttribute('data-mediaid') || '',
                                text: ((textElement ? textElement.textContent : anchor.textContent) || '')
                                    .replace(/\s+/g, ' ')
                                    .trim(),
                                title: anchor.getAttribute('title') || '',
                                selected: anchor.classList.contains('embed-link--selected')
                            };
                        }).filter((item) => !!item);

                        function getActiveStreamPlayerUrl() {
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            function srcOf(f) {
                                return String(f && (f.getAttribute('src') || f.src) || '').trim();
                            }
                            for (let i = 0; i < frames.length; i++) {
                                const s = srcOf(frames[i]).toLowerCase();
                                if (s && s.indexOf('player.kick.com') >= 0) {
                                    return srcOf(frames[i]);
                                }
                            }
                            for (let i = 0; i < frames.length; i++) {
                                const s = srcOf(frames[i]).toLowerCase();
                                if (s && s.indexOf('player.twitch') >= 0) {
                                    return srcOf(frames[i]);
                                }
                            }
                            for (let i = 0; i < frames.length; i++) {
                                const s = srcOf(frames[i]).toLowerCase();
                                if (s && (s.indexOf('youtube.com/embed') >= 0 || s.indexOf('youtube-nocookie.com/embed') >= 0)) {
                                    return srcOf(frames[i]);
                                }
                            }
                            for (let i = 0; i < frames.length; i++) {
                                const s = srcOf(frames[i]).toLowerCase();
                                if (s && s.indexOf('kick.com') >= 0 && s.indexOf('player.kick.com') < 0) {
                                    return srcOf(frames[i]);
                                }
                            }
                            return '';
                        }

                        const activeStreamUrl = getActiveStreamPlayerUrl() || '';
                        const fp = JSON.stringify({ items: items, activeStreamUrl: activeStreamUrl });
                        if (window.__codexLastEmbedFingerprint === fp) {
                            return;
                        }
                        window.__codexLastEmbedFingerprint = fp;

                        host.postMessage({
                            type: 'embedsState',
                            items: items,
                            activeStreamUrl: activeStreamUrl
                        });
                    }

                    let codexEmbedHostSyncTimer = 0;
                    function scheduleEmbedPanelReport() {
                        window.clearTimeout(codexEmbedHostSyncTimer);
                        codexEmbedHostSyncTimer = window.setTimeout(reportEmbedPanelToHost, 200);
                    }

                    function startEmbedListHostSync() {
                        if (window.__codexEmbedHostSyncStarted) {
                            return;
                        }

                        window.__codexEmbedHostSyncStarted = true;
                        window.setInterval(reportEmbedPanelToHost, 5000);
                        window.addEventListener('load', () => {
                            scheduleEmbedPanelReport();
                            window.setTimeout(reportEmbedPanelToHost, 1200);
                        });
                        if (document.documentElement && typeof MutationObserver === 'function') {
                            const embedMo = new MutationObserver(() => scheduleEmbedPanelReport());
                            embedMo.observe(document.documentElement, {
                                childList: true,
                                subtree: true,
                                attributes: true,
                                attributeFilter: ['class', 'href']
                            });
                        }
                    }

                    function requestDualChatSource() {
                        reportEmbedPanelToHost();
                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.postMessage === 'function') {
                            host.postMessage({ type: 'codex-stream-chat-request' });
                        }
                    }

                    function ensureDualChatPane() {
                        const onBigscreen = window.location.pathname && window.location.pathname.indexOf('/bigscreen') >= 0;
                        const hostLiftsStreamChat = !!(window.chrome && window.chrome.webview);
                        let anchor = document.getElementById('chat-output-frame');
                        let fixedToBody = false;
                        if (!anchor && onBigscreen && !hostLiftsStreamChat) {
                            anchor = document.body;
                            fixedToBody = true;
                        }

                        if (!anchor) {
                            return null;
                        }

                        let pane = document.getElementById(dualPaneId);
                        if (!pane) {
                            pane = document.createElement('div');
                            pane.id = dualPaneId;
                            pane.innerHTML =
                                '<iframe class=""codex-dual-chat-frame"" allow=""autoplay; clipboard-write"" sandbox=""allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox""></iframe>' +
                                '<div class=""codex-dual-chat-empty""></div>';
                            anchor.appendChild(pane);
                            if (fixedToBody) {
                                pane.classList.add('codex-dual-stream-pane--bigscreen-fixed');
                            }
                        } else if (fixedToBody) {
                            if (pane.parentElement !== document.body) {
                                document.body.appendChild(pane);
                            }

                            pane.classList.add('codex-dual-stream-pane--bigscreen-fixed');
                        } else {
                            pane.classList.remove('codex-dual-stream-pane--bigscreen-fixed');
                            if (pane.parentElement !== anchor) {
                                anchor.appendChild(pane);
                            }
                        }

                        return pane;
                    }

                    function getDualChatUnavailableText() {
                        if (dualChatSource && dualChatSource.platformLabel) {
                            return `${dualChatSource.platformLabel} chat is not available for the current stream selection.`;
                        }

                        return 'No live stream chat is available for the current stream selection.';
                    }

                    function syncDualChatButton() {
                        const btn = document.getElementById(dualButtonId);
                        if (!btn) {
                            return;
                        }
                        btn.classList.toggle('codex-split-chat-active', !!dualChatEnabled);
                    }

                    function getDualChatBottomAnchors() {
                        const selectors = [
                            '#chat-input-frame',
                            '#chat-input-wrap',
                            '#chat-input-control',
                            '#chat-tools-wrap',
                            '.chat-tools-group'
                        ];
                        const anchors = [];
                        for (const selector of selectors) {
                            const matches = Array.from(document.querySelectorAll(selector));
                            for (const match of matches) {
                                if (match instanceof Element && anchors.indexOf(match) < 0) {
                                    anchors.push(match);
                                }
                            }
                        }
                        return anchors;
                    }

                    function getDualChatBottomChromeTop() {
                        const anchors = getDualChatBottomAnchors();
                        let top = 0;
                        for (const anchor of anchors) {
                            const rect = anchor.getBoundingClientRect();
                            if (rect.height <= 2 || rect.width <= 2 || rect.bottom <= 0 || rect.top >= window.innerHeight) {
                                continue;
                            }

                            if (rect.bottom < Math.max(48, window.innerHeight * 0.45)) {
                                continue;
                            }

                            if (top <= 0 || rect.top < top) {
                                top = rect.top;
                            }
                        }

                        return top;
                    }

                    function bindDualChatInputResizeObserver() {
                        const anchors = getDualChatBottomAnchors();
                        const signature = anchors.map((anchor, index) => {
                            return `${index}:${anchor.id || anchor.className || anchor.tagName}`;
                        }).join('|');
                        if (dualChatInputObserved === signature) {
                            return;
                        }

                        dualChatInputObserved = signature;
                        if (dualChatInputResizeObserver) {
                            dualChatInputResizeObserver.disconnect();
                        }

                        if (anchors.length > 0 && typeof ResizeObserver === 'function') {
                            dualChatInputResizeObserver = new ResizeObserver(() => {
                                scheduleDualChatLayoutReport();
                            });
                            for (const anchor of anchors) {
                                dualChatInputResizeObserver.observe(anchor);
                                if (anchor.parentElement) {
                                    dualChatInputResizeObserver.observe(anchor.parentElement);
                                }
                            }
                        }
                    }

                    function reportDualChatLayout() {
                        bindDualChatInputResizeObserver();

                        const root = document.documentElement;
                        const frame = document.getElementById('chat-output-frame');
                        const sourceOutput = getActiveOutput();
                        let inputTop = 0;
                        let bottomOffset = 0;
                        let paneLeft = 0;
                        let paneTop = 0;
                        let paneWidth = 0;
                        let paneHeight = 0;
                        let layoutActive = !!dualChatEnabled && document.body.classList.contains('codex-dual-chat-enabled');

                        const bottomChromeTop = getDualChatBottomChromeTop();
                        if (bottomChromeTop > 0) {
                            inputTop = Math.max(0, Math.round(bottomChromeTop - 10));
                            bottomOffset = Math.max(0, Math.round(window.innerHeight - inputTop));
                        }

                        if (frame instanceof Element) {
                            const frameRect = frame.getBoundingClientRect();
                            const outputRect = sourceOutput instanceof Element
                                ? sourceOutput.getBoundingClientRect()
                                : null;
                            const expectedPaneWidth = frameRect.width > 0
                                ? Math.max(0, (frameRect.width - 14) / 2)
                                : 0;
                            const widthTolerance = Math.max(32, Math.round(frameRect.width * 0.06));
                            const outputLooksSplit = !!(outputRect &&
                                outputRect.width > 0 &&
                                expectedPaneWidth > 0 &&
                                Math.abs(outputRect.width - expectedPaneWidth) <= widthTolerance);

                            const hostLiftsStreamChat = !!(window.chrome && window.chrome.webview);
                            if (layoutActive && !hostLiftsStreamChat) {
                                layoutActive = layoutActive && outputLooksSplit;
                            }

                            if (frameRect.width > 0) {
                                paneWidth = Math.max(0, Math.round((frameRect.width - 14) / 2));
                                paneLeft = Math.max(0, Math.round(frameRect.right - paneWidth));

                                let topBound = frameRect.top;
                                let bottomBound = inputTop > frameRect.top ? inputTop : frameRect.bottom;

                                if (outputRect && outputRect.height > 0) {
                                    topBound = Math.max(topBound, outputRect.top);
                                    if (inputTop > 0) {
                                        bottomBound = Math.min(bottomBound, inputTop);
                                    } else {
                                        bottomBound = Math.min(bottomBound, outputRect.bottom);
                                    }
                                }

                                paneTop = Math.max(0, Math.round(topBound));
                                paneHeight = Math.max(0, Math.round(bottomBound - topBound));
                            }
                        }

                        if (root) {
                            root.style.setProperty('--codex-dual-chat-bottom-offset', `${bottomOffset}px`);
                        }

                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.postMessage === 'function') {
                            host.postMessage({
                                type: 'codex-dual-chat-layout',
                                inputTop: inputTop,
                                bottomOffset: bottomOffset,
                                layoutActive: layoutActive,
                                paneLeft: paneLeft,
                                paneTop: paneTop,
                                paneWidth: paneWidth,
                                paneHeight: paneHeight,
                                windowWidth: window.innerWidth | 0,
                                windowHeight: window.innerHeight | 0
                            });
                        }
                    }

                    function scheduleDualChatLayoutReport() {
                        if (dualChatLayoutScheduled) {
                            return;
                        }

                        dualChatLayoutScheduled = true;
                        window.requestAnimationFrame(() => {
                            dualChatLayoutScheduled = false;
                            reportDualChatLayout();
                        });
                    }

                    function bindDualChatLayout() {
                        if (dualChatLayoutBound) {
                            return;
                        }

                        dualChatLayoutBound = true;
                        window.addEventListener('resize', scheduleDualChatLayoutReport);
                        scheduleDualChatLayoutReport();
                    }

                    function notifyDualChatHostState() {
                        // Stream chat feature removed.
                    }

                    function notifyStreamChatHostToggle(enabled) {
                        const host = window.chrome && window.chrome.webview;
                        if (!host || typeof host.postMessage !== 'function') {
                            return;
                        }
                        host.postMessage({
                            type: 'codex-stream-chat-toggle',
                            enabled: !!enabled
                        });
                    }

                    function isBigscreenDualLayoutActive() {
                        if (window.location && window.location.pathname &&
                            window.location.pathname.indexOf('/bigscreen') >= 0) {
                            return true;
                        }
                        const chatWrap = document.querySelector('#chat-wrap') || document.querySelector('.chat-wrap');
                        if (!chatWrap || !chatWrap.parentElement) {
                            return false;
                        }
                        const siblings = Array.from(chatWrap.parentElement.children).filter((node) => node && node !== chatWrap);
                        if (!siblings.length) {
                            return false;
                        }
                        for (const sibling of siblings) {
                            if (!(sibling instanceof HTMLElement)) {
                                continue;
                            }
                            const rect = sibling.getBoundingClientRect();
                            if (rect.width > 40 && rect.height > 40) {
                                return true;
                            }
                        }
                        return false;
                    }

                    function applyBigscreenDualMode(enabled) {
                        const inBigscreenDualLayout = isBigscreenDualLayoutActive();
                        if (!inBigscreenDualLayout || !document.body) {
                            if (bigscreenDualModeTimer) {
                                window.clearInterval(bigscreenDualModeTimer);
                                bigscreenDualModeTimer = null;
                            }
                            document.body && document.body.classList.remove('codex-bigscreen-dual-single-chat');
                            return;
                        }

                        const applyTargets = () => {
                            const streamChatEnabled = !!enabled;
                            document.body.classList.toggle('codex-bigscreen-dual-single-chat', !streamChatEnabled);
                            const chatWrap = document.querySelector('#chat-wrap') || document.querySelector('.chat-wrap');
                            if (chatWrap && chatWrap.parentElement) {
                                const siblings = Array.from(chatWrap.parentElement.children);
                                for (const sibling of siblings) {
                                    if (!sibling || sibling === chatWrap) {
                                        continue;
                                    }
                                    if (!streamChatEnabled) {
                                        sibling.classList.add('codex-bigscreen-dual-hide-sibling');
                                    } else {
                                        sibling.classList.remove('codex-bigscreen-dual-hide-sibling');
                                    }
                                }
                            }

                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (const frame of frames) {
                                if (!frame || frame.closest('#' + dualPaneId)) {
                                    continue;
                                }
                                const src = (frame.getAttribute('src') || frame.src || '').toLowerCase();
                                if (!src || src.indexOf('kick.com') < 0) {
                                    continue;
                                }

                                let target = frame;
                                let parent = frame.parentElement;
                                let depth = 0;
                                while (parent && depth < 5 && parent !== document.body && parent !== document.documentElement) {
                                    if (parent.querySelector('#chat-wrap')) {
                                        break;
                                    }
                                    target = parent;
                                    parent = parent.parentElement;
                                    depth += 1;
                                }

                                if (!streamChatEnabled) {
                                    target.classList.add('codex-bigscreen-dual-hide-target');
                                } else {
                                    target.classList.remove('codex-bigscreen-dual-hide-target');
                                }
                            }
                        };

                        applyTargets();
                        if (enabled) {
                            if (!bigscreenDualModeTimer) {
                                bigscreenDualModeTimer = window.setInterval(applyTargets, 1000);
                            }
                        } else if (bigscreenDualModeTimer) {
                            window.clearInterval(bigscreenDualModeTimer);
                            bigscreenDualModeTimer = null;
                        }
                    }

                    function normalizeChatUrlForCompare(rawUrl) {
                        if (!rawUrl) {
                            return '';
                        }
                        try {
                            const parsed = new URL(String(rawUrl), window.location.href);
                            parsed.hash = '';
                            let normalized = parsed.toString();
                            if (normalized.endsWith('/')) {
                                normalized = normalized.slice(0, -1);
                            }
                            return normalized.toLowerCase();
                        } catch (error) {
                            return String(rawUrl).trim().toLowerCase();
                        }
                    }

                    function isChatUrlAlreadyEmbedded(chatUrl, ignoreFrame) {
                        if (!chatUrl) {
                            return false;
                        }
                        const expected = normalizeChatUrlForCompare(chatUrl);
                        if (!expected) {
                            return false;
                        }
                        const iframes = Array.from(document.querySelectorAll('iframe'));
                        for (const frame of iframes) {
                            if (!frame || frame === ignoreFrame) {
                                continue;
                            }
                            const src = frame.getAttribute('src') || frame.src || '';
                            if (!src) {
                                continue;
                            }
                            const current = normalizeChatUrlForCompare(src);
                            if (current && current === expected) {
                                return true;
                            }
                        }
                        return false;
                    }

                    function updateDualChatPane() {
                        syncDualChatButton();

                        const hostLiftsStreamChat = !!(window.chrome && window.chrome.webview);
                        const pane = ensureDualChatPane();
                        if (!pane) {
                            scheduleDualChatLayoutReport();
                            return;
                        }

                        const iframe = pane.querySelector('.codex-dual-chat-frame');
                        const emptyEl = pane.querySelector('.codex-dual-chat-empty');

                        if (!dualChatEnabled) {
                            pane.classList.remove('enabled', 'empty', 'codex-dual-chat-host-guest', 'codex-dual-stream-pane--bigscreen-fixed');
                            pane.removeAttribute('data-codex-chat-url');
                            if (iframe instanceof HTMLIFrameElement) {
                                iframe.setAttribute('src', 'about:blank');
                            }

                            if (emptyEl instanceof HTMLElement) {
                                emptyEl.textContent = '';
                            }

                            scheduleDualChatLayoutReport();
                            return;
                        }

                        const chatUrl = dualChatSource && dualChatSource.chatUrl ? String(dualChatSource.chatUrl) : '';
                        const available = !!(dualChatSource && dualChatSource.available && chatUrl);

                        if (!available) {
                            pane.classList.remove('codex-dual-chat-host-guest');
                            pane.classList.add('empty');
                            pane.classList.remove('enabled');
                            pane.removeAttribute('data-codex-chat-url');
                            if (iframe instanceof HTMLIFrameElement) {
                                iframe.setAttribute('src', 'about:blank');
                            }

                            if (emptyEl instanceof HTMLElement) {
                                emptyEl.textContent = getDualChatUnavailableText();
                            }

                            scheduleDualChatLayoutReport();
                            return;
                        }

                        if (hostLiftsStreamChat) {
                            pane.classList.add('enabled');
                            pane.classList.remove('empty');
                            pane.classList.add('codex-dual-chat-host-guest');
                            pane.setAttribute('data-codex-chat-url', chatUrl);
                            if (iframe instanceof HTMLIFrameElement) {
                                iframe.setAttribute('src', 'about:blank');
                            }

                            if (emptyEl instanceof HTMLElement) {
                                emptyEl.textContent = '';
                            }

                            scheduleDualChatLayoutReport();
                            return;
                        }

                        if (isChatUrlAlreadyEmbedded(chatUrl, iframe instanceof HTMLIFrameElement ? iframe : null)) {
                            pane.classList.remove('enabled', 'codex-dual-chat-host-guest');
                            pane.classList.add('empty');
                            if (iframe instanceof HTMLIFrameElement) {
                                iframe.setAttribute('src', 'about:blank');
                            }

                            if (emptyEl instanceof HTMLElement) {
                                emptyEl.textContent = 'Stream chat is already open in another embed.';
                            }

                            scheduleDualChatLayoutReport();
                            return;
                        }

                        pane.classList.add('enabled');
                        pane.classList.remove('empty', 'codex-dual-chat-host-guest');
                        pane.setAttribute('data-codex-chat-url', chatUrl);
                        if (iframe instanceof HTMLIFrameElement) {
                            const currentSrc = iframe.getAttribute('src') || iframe.src || '';
                            if (normalizeChatUrlForCompare(currentSrc) !== normalizeChatUrlForCompare(chatUrl)) {
                                iframe.setAttribute('src', chatUrl);
                            }
                        }

                        if (emptyEl instanceof HTMLElement) {
                            emptyEl.textContent = '';
                        }

                        scheduleDualChatLayoutReport();
                    }

                    function setDualChatEnabled(enabled, refreshSource) {
                        dualChatEnabled = !!enabled;
                        if (document.body) {
                            document.body.classList.toggle('codex-dual-chat-enabled', dualChatEnabled);
                        }

                        notifyStreamChatHostToggle(dualChatEnabled);
                        if (refreshSource || dualChatEnabled) {
                            requestDualChatSource();
                        }

                        updateDualChatPane();
                        scheduleDualChatLayoutReport();
                    }

                    function exposeCodexHarness() {
                        window.__codexHarness = window.__codexHarness || {};
                        window.__codexHarness.setSplitEnabled = (enabled) => {
                            setSplitEnabled(!!enabled, false);
                            return !!splitEnabled;
                        };
                        window.__codexHarness.setDualChatEnabled = (enabled) => {
                            setDualChatEnabled(!!enabled, false);
                            return !!dualChatEnabled;
                        };
                        window.__codexHarness.setDualChatSource = (payload) => {
                            dualChatSource = normalizeDualChatPayload(payload) || payload || null;
                            updateDualChatPane();
                            return !!(dualChatSource && dualChatSource.available);
                        };
                    }

                    function exposeSplitChatApi() {
                        window.__codexSplitChatApi = {
                            ensureDualButton: () => ensureDualButton(),
                            requestDualChatSource: () => requestDualChatSource(),
                            setDualChatEnabled: (enabled, refreshSource) => {
                                setDualChatEnabled(!!enabled, !!refreshSource);
                                return !!dualChatEnabled;
                            }
                        };
                    }

                    function ensureCodexEventBridge() {
                        if (document.documentElement && document.documentElement.dataset.codexEventBridgeBound === '1') {
                            return;
                        }

                        if (document.documentElement) {
                            document.documentElement.dataset.codexEventBridgeBound = '1';
                        }

                        document.addEventListener('codex:split-set', event => {
                            const detail = event && event.detail ? event.detail : {};
                            setSplitEnabled(!!detail.enabled, false);
                        }, true);

                        document.addEventListener('codex:dualchat-set', event => {
                            const detail = event && event.detail ? event.detail : {};
                            setDualChatEnabled(!!detail.enabled, false);
                        }, true);

                        document.addEventListener('codex:dualchat-source', event => {
                            const detail = event && event.detail ? event.detail : null;
                            dualChatSource = normalizeDualChatPayload(detail) || detail || null;
                            updateDualChatPane();
                        }, true);
                    }

                    function bootstrap() {
                        ensureStyles();
                        const staleDualButton = document.getElementById('codex-dual-stream-chat-btn');
                        if (staleDualButton && staleDualButton.parentElement) {
                            staleDualButton.parentElement.removeChild(staleDualButton);
                        }
                        try {
                            ensureDualChatMessageBridge();
                        } catch (error) {
                        }
                        ensureCodexEventBridge();
                        ensureToolbarHandlers();
                        const button = ensureButton();
                        ensureDualButton();
                        const overlay = ensureOverlay();
                        if (!button || !overlay || bootstrapped) {
                            return !!(button && overlay);
                        }

                        bootstrapped = true;
                        exposeCodexHarness();
                        exposeSplitChatApi();
                        bindDualChatLayout();
                        bindFrameObservers();
                        applySavedState();
                        startEmbedListHostSync();
                        return true;
                    }

                    function waitForChatUi() {
                        if (bootstrap()) {
                            return;
                        }

                        if (rootObserver || !(document.documentElement || document.body)) {
                            return;
                        }

                        rootObserver = new MutationObserver(() => {
                            if (bootstrap() && rootObserver) {
                                rootObserver.disconnect();
                                rootObserver = null;
                            }
                        });
                        rootObserver.observe(document.documentElement || document.body, {
                            childList: true,
                            subtree: true
                        });
                    }

                    if (document.readyState === 'loading') {
                        document.addEventListener('DOMContentLoaded', waitForChatUi, { once: true });
                    } else {
                        waitForChatUi();
                    }

                    window.addEventListener('load', waitForChatUi);
                })();
            ");
        }

        private System.Threading.Tasks.Task RegisterChatExtensionsAsync()
        {
            if (_webView.CoreWebView2 == null)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            return _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    if (window.__codexExtensionsInjected) return;
                    window.__codexExtensionsInjected = true;

                    // ── Storage helpers ──────────────────────────────────────────
                    const EXT = 'codex-ext.';
                    function xGet(k, d) {
                        try { const v = localStorage.getItem(EXT + k); return v === null ? d : JSON.parse(v); } catch { return d; }
                    }
                    function xSet(k, v) {
                        try { localStorage.setItem(EXT + k, JSON.stringify(v)); } catch {}
                    }

                    // ── Shared WebSocket interceptor ─────────────────────────────
                    const wsListeners = [];
                    function onWsMsg(cb) { wsListeners.push(cb); }
                    window.__codexEmitWsTest = function(payload) {
                        wsListeners.forEach(fn => { try { fn(payload); } catch {} });
                    };
                    if (!window.__codexWsPatched) {
                        window.__codexWsPatched = true;
                        const NativeWS = window.WebSocket;
                        function PatchedWS(...a) {
                            const ws = new NativeWS(...a);
                            ws.addEventListener('message', ({ data }) => {
                                wsListeners.forEach(fn => { try { fn(data); } catch {} });
                            });
                            return ws;
                        }
                        PatchedWS.prototype = NativeWS.prototype;
                        window.WebSocket = PatchedWS;
                    }

                    // ── Watched-users list (shared by activity + notifications) ──
                    function getWatched() { return xGet('watchedUsers', ['destiny']); }
                    function setWatched(arr) { xSet('watchedUsers', arr); }
                    function isWatched(nick) {
                        if (!nick) return false;
                        return getWatched().some(u => u.toLowerCase() === nick.toLowerCase());
                    }

                    // ── Feature 1: User activity tracker (sarkyScript) ───────────
                    // Inline colour-coded alerts for JOIN / QUIT / UPDATEUSER
                    function postActivityMsg(nick, type, color, extra) {
                        if (!xGet('activity.enabled', true)) return;
                        const lines = document.querySelector('.chat-lines');
                        if (!lines) return;
                        const now = new Date();
                        const ts = `${String(now.getHours()).padStart(2,'0')}:${String(now.getMinutes()).padStart(2,'0')}`;
                        let body = '';
                        if (type === 'JOIN')  body = `${nick} joined`;
                        else if (type === 'QUIT') body = `${nick} left`;
                        else if (extra) body = `${nick} watching: ${extra.title || extra.channel || extra.id || '?'} on ${extra.platform || '?'}`;
                        const el = document.createElement('div');
                        el.className = 'msg-chat msg-info';
                        el.style.cssText = `border-left:3px solid ${color};padding-left:6px;opacity:.85;`;
                        el.innerHTML = `<span class=""time"">${ts}</span> <span class=""text"" style=""color:${color}"">${body}</span>`;
                        lines.appendChild(el);
                        lines.scrollTop = lines.scrollHeight;
                    }
                    onWsMsg(data => {
                        if (!xGet('activity.enabled', true)) return;
                        let type = null, p = null;
                        if (data.startsWith('JOIN '))       { try { p = JSON.parse(data.slice(5));  type = 'JOIN';       } catch {} }
                        else if (data.startsWith('QUIT '))  { try { p = JSON.parse(data.slice(5));  type = 'QUIT';       } catch {} }
                        else if (data.startsWith('UPDATEUSER ')) { try { p = JSON.parse(data.slice(11)); type = 'UPDATEUSER'; } catch {} }
                        if (!type || !p) return;
                        const nick = p.nick || p.user;
                        if (!nick || !isWatched(nick)) return;
                        if (type === 'JOIN' && xGet('activity.join', true))  postActivityMsg(nick, 'JOIN',  '#22c55e', null);
                        if (type === 'QUIT' && xGet('activity.quit', true))  postActivityMsg(nick, 'QUIT',  '#ef4444', null);
                        if (type === 'UPDATEUSER' && xGet('activity.embed', true) && p.watching)
                            postActivityMsg(nick, 'UPDATEUSER', '#a855f7', p.watching);
                    });

                    // ── Feature 2: Desktop notifications on watched-user messages ─
                    function maybeNotify(nick, text) {
                        if (!xGet('notifications.enabled', false)) return;
                        if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
                        new Notification(`${nick} said:`, {
                            body: text,
                            icon: 'https://cdn.destiny.gg/2.49.0/emotes/6296cf7e8ccd0.png'
                        });
                    }
                    let mentionDingAudioCtx = null;
                    let lastMentionDingAt = 0;
                    function normalizeNickValue(value) {
                        return String(value || '').trim().toLowerCase().replace(/^@+/, '');
                    }
                    function resolveMentionTargetNick() {
                        const configured = normalizeNickValue(xGet('mentions.username', ''));
                        if (configured) return configured;
                        const domCandidates = [
                            document.body?.getAttribute('data-username'),
                            document.documentElement?.getAttribute('data-username'),
                            document.querySelector('[data-current-user]')?.getAttribute('data-current-user'),
                            document.querySelector('[data-username][data-current-user]')?.getAttribute('data-username')
                        ];
                        for (const candidate of domCandidates) {
                            const normalized = normalizeNickValue(candidate);
                            if (normalized) return normalized;
                        }
                        const watched = getWatched();
                        if (Array.isArray(watched) && watched.length) {
                            return normalizeNickValue(watched[0]);
                        }
                        return '';
                    }
                    function hasMentionForTarget(messageNode, targetNick) {
                        if (!messageNode || !targetNick) return false;
                        const mentionedAttr = normalizeNickValue(messageNode.getAttribute('data-mentioned') || '');
                        if (mentionedAttr) {
                            const mentionedValues = mentionedAttr.split(/\s+/).map(normalizeNickValue).filter(Boolean);
                            if (mentionedValues.includes(targetNick)) return true;
                        }
                        const text = String(messageNode.querySelector('.text')?.textContent || messageNode.textContent || '').toLowerCase();
                        if (!text) return false;
                        if (text.indexOf('@' + targetNick) >= 0) return true;
                        return new RegExp('(^|\\W)' + targetNick.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(\\W|$)', 'i').test(text);
                    }
                    function playMentionDing() {
                        if (!xGet('mentions.ding.enabled', true)) return;
                        const now = Date.now();
                        if ((now - lastMentionDingAt) < 900) return;
                        lastMentionDingAt = now;
                        try {
                            const AudioCtx = window.AudioContext || window.webkitAudioContext;
                            if (!AudioCtx) return;
                            mentionDingAudioCtx = mentionDingAudioCtx || new AudioCtx();
                            if (mentionDingAudioCtx.state === 'suspended') {
                                mentionDingAudioCtx.resume();
                            }
                            const osc = mentionDingAudioCtx.createOscillator();
                            const gain = mentionDingAudioCtx.createGain();
                            osc.type = 'sine';
                            osc.frequency.setValueAtTime(880, mentionDingAudioCtx.currentTime);
                            osc.frequency.exponentialRampToValueAtTime(1320, mentionDingAudioCtx.currentTime + 0.12);
                            gain.gain.setValueAtTime(0.0001, mentionDingAudioCtx.currentTime);
                            gain.gain.exponentialRampToValueAtTime(0.12, mentionDingAudioCtx.currentTime + 0.01);
                            gain.gain.exponentialRampToValueAtTime(0.0001, mentionDingAudioCtx.currentTime + 0.2);
                            osc.connect(gain);
                            gain.connect(mentionDingAudioCtx.destination);
                            osc.start();
                            osc.stop(mentionDingAudioCtx.currentTime + 0.21);
                        } catch (error) {
                        }
                    }
                    function watchMsgNotify() {
                        const lines = document.querySelector('.chat-lines');
                        if (!lines) return;
                        new MutationObserver(muts => {
                            const mentionTarget = resolveMentionTargetNick();
                            for (const m of muts) {
                                for (const n of m.addedNodes) {
                                    if (!(n instanceof Element) || !n.classList.contains('msg-user')) continue;
                                    const nick = n.getAttribute('data-username') || '';
                                    if (isWatched(nick)) {
                                        maybeNotify(nick, n.querySelector('.text')?.textContent?.trim() || '');
                                    }
                                    if (mentionTarget && hasMentionForTarget(n, mentionTarget)) {
                                        playMentionDing();
                                    }
                                }
                            }
                        }).observe(lines, { childList: true });
                    }

                    // ── Feature 3: DinkDonk button ───────────────────────────────
                    let ddTimer = null;
                    let ddLastAutoOpenUrl = '';
                    let ddLastAutoOpenAt = 0;
                    function isDinkDonkAnnouncer(nick) {
                        if (!nick) return false;
                        const norm = String(nick).toLowerCase().replace(/[^a-z0-9]/g, '');
                        return norm === 'dinkdonkbot';
                    }
                    function extractDinkDonkUrl(raw) {
                        if (typeof raw !== 'string' || !raw) return '';
                        const text = String(raw);
                        const full = text.match(/https?:\/\/(?:www\.)?dinkdonk\.mov[^\s""'<>]*/i);
                        if (full && full[0]) return full[0];
                        const partial = text.match(/\bdinkdonk\.mov[^\s""'<>]*/i);
                        if (partial && partial[0]) return partial[0].startsWith('http') ? partial[0] : ('https://' + partial[0]);
                        return '';
                    }
                    function maybeAutoOpenDinkDonk(url) {
                        if (!url || !xGet('dinkdonk.autoOpenPoll', true)) return;
                        const now = Date.now();
                        if (ddLastAutoOpenUrl === url && (now - ddLastAutoOpenAt) < 15000) return;
                        ddLastAutoOpenUrl = url;
                        ddLastAutoOpenAt = now;
                        try {
                            window.open(url, '_blank', 'noopener,noreferrer');
                        } catch (error) {
                        }
                    }
                    function setDinkDonkActiveUrl(url) {
                        const btn = document.getElementById('codex-dinkdonk-btn');
                        if (!btn) return;
                        btn.href = url;
                        btn.style.color = 'yellow';
                        const svg = btn.querySelector('svg');
                        if (svg) svg.style.stroke = 'yellow';
                        clearTimeout(ddTimer);
                        ddTimer = setTimeout(() => {
                            ddTimer = null;
                            btn.href = 'https://dinkdonk.mov';
                            btn.style.color = '';
                            const s = btn.querySelector('svg'); if (s) s.style.stroke = '#555';
                        }, 4 * 60 * 1000);
                    }
                    function handleDinkDonkAnnouncement(nick, text) {
                        const pollUrl = extractDinkDonkUrl(text);
                        if (!isDinkDonkAnnouncer(nick) || !pollUrl) return;
                        setDinkDonkActiveUrl(pollUrl);
                        maybeAutoOpenDinkDonk(pollUrl);
                    }
                    function buildDinkDonkBtn() {
                        if (document.getElementById('codex-dinkdonk-btn')) return;
                        const ref = document.getElementById('chat-whisper-btn');
                        if (!ref) return;
                        const btn = document.createElement('a');
                        btn.id = 'codex-dinkdonk-btn';
                        btn.className = 'chat-tool-btn';
                        btn.href = 'https://dinkdonk.mov';
                        btn.target = '_blank';
                        btn.rel = 'noopener noreferrer';
                        btn.title = 'DinkDonk polls';
                        btn.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;';
                        if (ref.offsetWidth) { btn.style.width = ref.offsetWidth + 'px'; btn.style.height = ref.offsetHeight + 'px'; }
                        btn.innerHTML = `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' style='width:85%;height:85%;' fill='none' stroke='#555' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'><rect x='2' y='13' width='20' height='10' rx='1'/><rect x='2' y='10' width='20' height='3'/><line x1='4' y1='10' x2='1' y2='13'/><line x1='8' y1='10' x2='5' y2='13'/><line x1='12' y1='10' x2='9' y2='13'/><line x1='16' y1='10' x2='13' y2='13'/><line x1='20' y1='10' x2='17' y2='13'/><g transform='rotate(-15,2,10)'><rect x='2' y='7' width='20' height='3'/></g></svg>`;
                        const svg = btn.querySelector('svg');
                        btn.addEventListener('mouseenter', () => svg.style.stroke = ddTimer ? 'yellow' : '#fff');
                        btn.addEventListener('mouseleave', () => svg.style.stroke = ddTimer ? 'yellow' : '#555');
                        ref.after(btn);
                    }
                    onWsMsg(data => {
                        if (!xGet('dinkdonk.enabled', true)) return;
                        try {
                            const splitIndex = data.indexOf(' ');
                            if (splitIndex <= 0) return;
                            const payload = data.slice(splitIndex + 1);
                            if (!payload || payload[0] !== '{') return;
                            const p = JSON.parse(payload);
                            if (!p) return;
                            const nick = p.nick || p.username || p.user || '';
                            const text = typeof p.data === 'string' ? p.data : (typeof p.message === 'string' ? p.message : '');
                            handleDinkDonkAnnouncement(nick, text);
                        } catch {}
                    });
                    function watchDinkDonkChatLines() {
                        const lines = document.querySelector('.chat-lines');
                        if (!lines || lines.dataset.codexDinkDonkWatchBound) return;
                        lines.dataset.codexDinkDonkWatchBound = '1';
                        new MutationObserver(muts => {
                            if (!xGet('dinkdonk.enabled', true)) return;
                            for (const m of muts) {
                                for (const n of m.addedNodes) {
                                    if (!(n instanceof Element)) continue;
                                    const row = n.classList.contains('msg-user') || n.classList.contains('msg-chat')
                                        ? n
                                        : n.querySelector('.msg-user, .msg-chat');
                                    if (!(row instanceof Element)) continue;
                                    const nick = row.getAttribute('data-username')
                                        || row.querySelector('.user, .chat-user')?.textContent
                                        || '';
                                    const text = row.querySelector('.text')?.textContent || row.textContent || '';
                                    handleDinkDonkAnnouncement(String(nick).trim(), String(text).trim());
                                }
                            }
                        }).observe(lines, { childList: true, subtree: true });
                    }

                    // ── Feature 4: External chat side panel (Kick / YouTube) ──────
                    function getSplitChatApi() {
                        return window.__codexSplitChatApi || null;
                    }

                    function ensureDualChatMessageBridge() {
                        return getSplitChatApi();
                    }

                    function requestDualChatSource() {
                        const api = getSplitChatApi();
                        if (api && typeof api.requestDualChatSource === 'function') {
                            api.requestDualChatSource();
                        }
                    }

                    function buildDualChatBtn() {
                        const api = getSplitChatApi();
                        if (api && typeof api.ensureDualButton === 'function') {
                            return api.ensureDualButton();
                        }

                        window.setTimeout(() => {
                            const delayedApi = getSplitChatApi();
                            if (delayedApi && typeof delayedApi.ensureDualButton === 'function') {
                                delayedApi.ensureDualButton();
                            }
                        }, 0);

                        return document.getElementById('codex-stream-chat-btn');
                    }

                    const EXT_PANEL = 'codex-ext-panel';
                    const PANEL_MIN = 240, PANEL_MAX = 700;
                    function extSrc(url) {
                        const u = (url || '').trim();
                        if (u.startsWith('#kick/'))    return 'https://kick.com/popout/' + u.slice(6) + '/chat';
                        if (u.startsWith('#youtube/')) return 'https://www.youtube.com/live_chat?v=' + encodeURIComponent(u.slice(9)) + '&embed_domain=' + encodeURIComponent(location.hostname || 'www.destiny.gg');
                        if (u.startsWith('http'))      return u;
                        return '';
                    }
                    function openExtPanel() {
                        let panel = document.getElementById(EXT_PANEL);
                        const src = extSrc(xGet('externalChat.url', ''));
                        if (!src) return false;
                        if (!panel) {
                            const w = Math.max(PANEL_MIN, Math.min(PANEL_MAX, xGet('externalChat.width', 340)));
                            panel = document.createElement('div');
                            panel.id = EXT_PANEL;
                            panel.style.cssText = `position:fixed;top:0;right:0;bottom:0;width:${w}px;display:flex;flex-direction:column;background:#18181b;z-index:8888;box-shadow:-2px 0 8px rgba(0,0,0,.4);`;
                            const hdr = document.createElement('div');
                            hdr.style.cssText = 'display:flex;align-items:center;justify-content:space-between;padding:4px 8px;background:#111;border-bottom:1px solid #2a2a2a;flex-shrink:0;';
                            const lbl = document.createElement('span');
                            lbl.style.cssText = 'font-size:10px;color:#666;';
                            lbl.textContent = 'External Chat';
                            const xbtn = document.createElement('button');
                            xbtn.textContent = '✕';
                            xbtn.style.cssText = 'background:none;border:none;color:#666;cursor:pointer;font-size:11px;padding:0 4px;';
                            xbtn.addEventListener('click', () => closeExtPanel());
                            hdr.appendChild(lbl);
                            hdr.appendChild(xbtn);
                            const iframe = document.createElement('iframe');
                            iframe.src = src;
                            iframe.style.cssText = 'flex:1;width:100%;border:none;min-height:0;';
                            const handle = document.createElement('div');
                            handle.style.cssText = 'position:absolute;left:0;top:0;bottom:0;width:5px;cursor:col-resize;';
                            handle.addEventListener('mouseenter', () => handle.style.background = 'rgba(255,255,255,.1)');
                            handle.addEventListener('mouseleave', () => handle.style.background = '');
                            handle.addEventListener('mousedown', e => {
                                e.preventDefault();
                                const sx = e.clientX, sw = panel.offsetWidth;
                                const mv = e2 => { const nw = Math.max(PANEL_MIN, Math.min(PANEL_MAX, sw + sx - e2.clientX)); panel.style.width = nw + 'px'; xSet('externalChat.width', nw); };
                                const up = () => { document.removeEventListener('mousemove', mv); document.removeEventListener('mouseup', up); };
                                document.addEventListener('mousemove', mv);
                                document.addEventListener('mouseup', up);
                            });
                            panel.appendChild(handle);
                            panel.appendChild(hdr);
                            panel.appendChild(iframe);
                            document.body.appendChild(panel);
                        } else {
                            panel.style.display = 'flex';
                            const iframe = panel.querySelector('iframe');
                            if (iframe && iframe.src !== src) iframe.src = src;
                        }
                        xSet('externalChat.open', true);
                        syncExtBtn(true);
                        return true;
                    }
                    function closeExtPanel() {
                        const p = document.getElementById(EXT_PANEL);
                        if (p) p.style.display = 'none';
                        xSet('externalChat.open', false);
                        syncExtBtn(false);
                    }
                    window.__codexCloseExtPanel = closeExtPanel;
                    function syncExtBtn(open) {
                        const btn = document.getElementById('codex-ext-panel-btn');
                        if (btn) btn.classList.toggle('codex-split-chat-active', !!open);
                    }
                    function removeExtPanelBtn() {
                        const btn = document.getElementById('codex-ext-panel-btn');
                        if (btn && btn.parentElement) {
                            btn.parentElement.removeChild(btn);
                        }
                    }

                    // ── Feature 5: Double-click username → append to chat input ───
                    document.addEventListener('dblclick', e => {
                        if (!xGet('chatInput.doubleClick', false)) return;
                        const el = e.target;
                        if (!el || (!el.classList.contains('user') && !el.classList.contains('chat-user'))) return;
                        const nick = (el.textContent || '').trim();
                        if (!nick) return;
                        const ta = document.querySelector('#chat-input-control');
                        if (!ta) return;
                        if (ta.value.length > 0 && ta.value.slice(-1) !== ' ') ta.value += ' ';
                        ta.value += nick + ' ';
                        ta.dispatchEvent(new Event('input', { bubbles: true }));
                        ta.focus();
                    }, true);

                    // ── Feature 6: Custom phrase highlights on chat input ─────────
                    let lastPhraseColor = null;
                    function checkPhraseColor(text) {
                        if (!xGet('phrases.enabled', true)) return null;
                        const lc = text.toLowerCase();
                        for (const p of (xGet('phrases', []) || [])) {
                            if (p.text && lc.includes(p.text.toLowerCase())) return p.color || '#1f0000';
                        }
                        return null;
                    }
                    function setupPhraseInput() {
                        const ta = document.querySelector('#chat-input-control');
                        if (!ta || ta.dataset.codexPhraseBound) return;
                        ta.dataset.codexPhraseBound = '1';
                        ta.addEventListener('input', () => {
                            const c = checkPhraseColor(ta.value);
                            if (c !== lastPhraseColor) {
                                lastPhraseColor = c;
                                ta.style.backgroundColor = c || '';
                            }
                        });
                    }

                    // ── Feature 7: Hidden phrases (hide messages containing them) ─
                    function setupHiddenPhrases() {
                        const lines = document.querySelector('.chat-lines');
                        if (!lines) return;
                        new MutationObserver(muts => {
                            if (!xGet('hiddenPhrases.enabled', false)) return;
                            const list = xGet('hiddenPhrases', []) || [];
                            if (!list.length) return;
                            for (const m of muts) {
                                for (const n of m.addedNodes) {
                                    if (!(n instanceof Element)) continue;
                                    const txt = (n.textContent || '').toLowerCase();
                                    if (list.some(p => p && txt.includes(p.toLowerCase()))) n.style.display = 'none';
                                }
                            }
                        }).observe(lines, { childList: true });
                    }

                    // ── Feature 8: Image paste → upload to femboy.beauty ─────────
                    function setupImageUpload() {
                        document.addEventListener('paste', async e => {
                            if (!xGet('imageUpload.enabled', false)) return;
                            const ta = e.target;
                            if (ta.tagName !== 'TEXTAREA' || !ta.closest('#chat-input-control')) return;
                            const items = (e.clipboardData || e.originalEvent?.clipboardData)?.items || [];
                            for (const item of items) {
                                if (!item.type.startsWith('image/')) continue;
                                e.preventDefault();
                                const file = item.getAsFile();
                                if (!file) break;
                                const orig = ta.getAttribute('placeholder') || '';
                                ta.setAttribute('placeholder', 'Uploading...');
                                try {
                                    const fd = new FormData();
                                    fd.append('file', file);
                                    const r = await fetch('https://femboy.beauty/api/upload', { method: 'POST', body: fd });
                                    if (!r.ok) throw new Error(r.status);
                                    const d = await r.json();
                                    if (!d.link) throw new Error('no link');
                                    const s = ta.selectionStart, en = ta.selectionEnd;
                                    ta.value = ta.value.slice(0, s) + d.link + ' ' + ta.value.slice(en);
                                    ta.selectionStart = ta.selectionEnd = s + d.link.length + 1;
                                    ta.dispatchEvent(new Event('input', { bubbles: true }));
                                } catch (err) {
                                    console.error('[codex] Upload error:', err);
                                } finally {
                                    ta.setAttribute('placeholder', orig);
                                }
                                break;
                            }
                        });
                    }

                    // ── Settings panel ────────────────────────────────────────────
                    const SETT_ID = 'codex-ext-settings';
                    function ensureSettingsStyles() {
                        if (document.getElementById('codex-ext-styles')) return;
                        const s = document.createElement('style');
                        s.id = 'codex-ext-styles';
                        s.textContent = `
                            #${SETT_ID} {
                                position:absolute; bottom:0; left:0; right:0;
                                z-index:200; background:#141418;
                                border-top:1px solid #2a2a2a;
                                display:none; flex-direction:column;
                                max-height:65%;
                            }
                            #${SETT_ID}.active { display:flex; }
                            #${SETT_ID} .xs-bar {
                                display:flex; align-items:center; justify-content:space-between;
                                padding:5px 10px; background:#0e0e12; border-bottom:1px solid #2a2a2a;
                                flex-shrink:0;
                            }
                            #${SETT_ID} .xs-bar h5 { margin:0; font-size:11px; color:#999; font-weight:700; text-transform:uppercase; letter-spacing:.04em; }
                            #${SETT_ID} .xs-close { background:none; border:none; color:#666; cursor:pointer; font-size:13px; padding:0 4px; line-height:1; }
                            #${SETT_ID} .xs-close:hover { color:#fff; }
                            #${SETT_ID} .xs-body { overflow-y:auto; flex:1; padding:6px 10px 10px; }
                            #${SETT_ID} .xs-section { font-size:9px; color:#555; text-transform:uppercase; font-weight:700; letter-spacing:.06em; margin:10px 0 4px; padding-bottom:3px; border-bottom:1px solid #222; }
                            #${SETT_ID} .xs-section:first-child { margin-top:2px; }
                            #${SETT_ID} .xs-row { display:flex; align-items:center; gap:6px; margin:3px 0; }
                            #${SETT_ID} .xs-row label { font-size:12px; color:#bbb; flex:1; cursor:pointer; }
                            #${SETT_ID} .xs-row input[type=checkbox] { cursor:pointer; margin:0; flex-shrink:0; accent-color:#5b7fa6; }
                            #${SETT_ID} .xs-row input[type=text] { flex:1; background:#0e0e12; border:1px solid #2a2a2a; color:#ccc; border-radius:3px; padding:3px 6px; font-size:11px; }
                            #${SETT_ID} .xs-row input[type=text]:focus { outline:none; border-color:#444; }
                            #${SETT_ID} .xs-row input[type=color] { width:30px; height:22px; padding:1px 2px; cursor:pointer; border:1px solid #2a2a2a; border-radius:3px; background:#0e0e12; }
                            #${SETT_ID} .xs-phrase-list { margin-top:3px; }
                            #${SETT_ID} .xs-prow { display:flex; align-items:center; gap:4px; margin:2px 0; }
                            #${SETT_ID} .xs-prow input[type=text] { flex:1; background:#0e0e12; border:1px solid #2a2a2a; color:#ccc; border-radius:3px; padding:3px 6px; font-size:11px; }
                            #${SETT_ID} .xs-prow input[type=color] { width:28px; height:22px; padding:1px 2px; cursor:pointer; border:1px solid #2a2a2a; border-radius:3px; background:#0e0e12; }
                            #${SETT_ID} .xs-prow .xs-del { background:#1a1a1f; border:1px solid #333; color:#666; cursor:pointer; border-radius:3px; padding:1px 6px; font-size:11px; }
                            #${SETT_ID} .xs-prow .xs-del:hover { color:#ef4444; border-color:#ef4444; }
                            #${SETT_ID} .xs-add { background:#1a1a1f; border:1px solid #333; color:#888; cursor:pointer; border-radius:3px; padding:3px 10px; font-size:11px; margin-top:5px; }
                            #${SETT_ID} .xs-add:hover { color:#fff; border-color:#555; }
                            #${SETT_ID} textarea.xs-ta { width:100%; background:#0e0e12; border:1px solid #2a2a2a; color:#ccc; border-radius:3px; padding:4px 6px; font-size:11px; resize:vertical; min-height:52px; box-sizing:border-box; font-family:inherit; }
                            #${SETT_ID} textarea.xs-ta:focus { outline:none; border-color:#444; }
                            #codex-ext-settings-btn.codex-split-chat-active { opacity:1 !important; }
                            #codex-ext-settings-btn .btn-icon,
                            #codex-ext-panel-btn .btn-icon,
                            #codex-snip-btn .btn-icon { opacity:1 !important; }
                        `;
                        (document.head || document.documentElement).appendChild(s);
                    }

                    function mkSection(txt) {
                        const d = document.createElement('div');
                        d.className = 'xs-section';
                        d.textContent = txt;
                        return d;
                    }
                    function mkCheckRow(label, key, def, cb) {
                        const row = document.createElement('div');
                        row.className = 'xs-row';
                        const id = 'xs-cb-' + key.replace(/\./g, '-');
                        const chk = document.createElement('input');
                        chk.type = 'checkbox'; chk.id = id; chk.checked = xGet(key, def);
                        chk.addEventListener('change', () => { xSet(key, chk.checked); if (cb) cb(chk.checked); });
                        const lbl = document.createElement('label');
                        lbl.htmlFor = id; lbl.textContent = label;
                        row.appendChild(chk); row.appendChild(lbl);
                        return row;
                    }

                    function buildSettings() {
                        if (document.getElementById(SETT_ID)) return;
                        const chat = document.getElementById('chat');
                        if (!chat) return;
                        ensureSettingsStyles();

                        const panel = document.createElement('div');
                        panel.id = SETT_ID;

                        // Top bar
                        const bar = document.createElement('div');
                        bar.className = 'xs-bar';
                        const ttl = document.createElement('h5');
                        ttl.textContent = '⚙ Chat Extensions';
                        const closeBtn = document.createElement('button');
                        closeBtn.className = 'xs-close'; closeBtn.textContent = '✕';
                        closeBtn.addEventListener('click', () => { panel.classList.remove('active'); document.getElementById('codex-ext-settings-btn')?.classList.remove('codex-split-chat-active'); });
                        bar.appendChild(ttl); bar.appendChild(closeBtn);

                        const body = document.createElement('div');
                        body.className = 'xs-body';

                        // ── Watched Users ──
                        body.appendChild(mkSection('👥 Watched Users'));
                        const wRow = document.createElement('div'); wRow.className = 'xs-row';
                        const wta = document.createElement('textarea');
                        wta.className = 'xs-ta'; wta.placeholder = 'destiny\n(one per line)';
                        wta.value = getWatched().join('\n');
                        wta.addEventListener('input', () => setWatched(wta.value.split('\n').map(s => s.trim()).filter(Boolean)));
                        wRow.appendChild(wta); body.appendChild(wRow);
                        body.appendChild(mkCheckRow('Show inline join/quit/embed alerts for watched users', 'activity.enabled', true));
                        body.appendChild(mkCheckRow('Alert on JOIN (watched users)', 'activity.join', true));
                        body.appendChild(mkCheckRow('Alert on QUIT (watched users)', 'activity.quit', true));
                        body.appendChild(mkCheckRow('Alert on embed update (watched users)', 'activity.embed', true));
                        body.appendChild(mkCheckRow('Desktop notification when watched user messages', 'notifications.enabled', false, on => {
                            if (on && typeof Notification !== 'undefined' && Notification.permission === 'default') Notification.requestPermission();
                        }));

                        // ── Activity Alerts ──
                        body.appendChild(mkSection('🔔 Activity Alerts'));
                        body.appendChild(mkCheckRow('Play ding when your username is mentioned', 'mentions.ding.enabled', true));
                        const mentionRow = document.createElement('div'); mentionRow.className = 'xs-row';
                        const mentionLabel = document.createElement('label');
                        mentionLabel.textContent = 'Mention username override:';
                        mentionLabel.style.flex = '0 0 auto';
                        mentionLabel.style.color = '#888';
                        const mentionInput = document.createElement('input');
                        mentionInput.type = 'text';
                        mentionInput.placeholder = '(optional, leave blank to auto-detect)';
                        mentionInput.value = xGet('mentions.username', '');
                        mentionInput.addEventListener('input', () => xSet('mentions.username', mentionInput.value.trim()));
                        mentionRow.appendChild(mentionLabel);
                        mentionRow.appendChild(mentionInput);
                        body.appendChild(mentionRow);

                        // ── DinkDonk ──
                        body.appendChild(mkSection('🎬 DinkDonk'));
                        body.appendChild(mkCheckRow('Show DinkDonk poll button', 'dinkdonk.enabled', true, on => {
                            const b = document.getElementById('codex-dinkdonk-btn');
                            if (b) b.style.display = on ? '' : 'none';
                        }));
                        body.appendChild(mkCheckRow('Auto-open poll links from DinkDonk_bot', 'dinkdonk.autoOpenPoll', true));

                        body.appendChild(mkSection('External Chat'));
                        const extChatRow = document.createElement('div');
                        extChatRow.className = 'xs-row';
                        const extChatLabel = document.createElement('label');
                        extChatLabel.textContent = 'Panel URL:';
                        extChatLabel.style.flex = '0 0 auto';
                        extChatLabel.style.color = '#888';
                        const extChatInput = document.createElement('input');
                        extChatInput.type = 'text';
                        extChatInput.placeholder = '#kick/username, #youtube/video-id, or https://...';
                        extChatInput.value = xGet('externalChat.url', '');
                        extChatInput.addEventListener('input', () => xSet('externalChat.url', extChatInput.value.trim()));
                        extChatRow.appendChild(extChatLabel);
                        extChatRow.appendChild(extChatInput);
                        body.appendChild(extChatRow);

                        // ── Chat Input ──
                        body.appendChild(mkSection('✏️ Chat Input'));
                        body.appendChild(mkCheckRow('Double-click username to append to chat', 'chatInput.doubleClick', false));

                        // ── Phrase Highlights ──
                        body.appendChild(mkSection('🎨 Phrase Highlights'));
                        body.appendChild(mkCheckRow('Highlight chat input on flagged phrases', 'phrases.enabled', true));
                        const phraseList = document.createElement('div');
                        phraseList.className = 'xs-phrase-list';
                        function renderPhraseList() {
                            phraseList.innerHTML = '';
                            const phrases = xGet('phrases', []) || [];
                            phrases.forEach((p, i) => {
                                const row = document.createElement('div'); row.className = 'xs-prow';
                                const tin = document.createElement('input'); tin.type = 'text'; tin.value = p.text || ''; tin.placeholder = 'phrase...';
                                tin.addEventListener('input', () => { const cur = xGet('phrases', []); if (cur[i]) { cur[i].text = tin.value; xSet('phrases', cur); } });
                                const cin = document.createElement('input'); cin.type = 'color'; cin.value = p.color || '#1f0000';
                                cin.addEventListener('input', () => { const cur = xGet('phrases', []); if (cur[i]) { cur[i].color = cin.value; xSet('phrases', cur); } });
                                const del = document.createElement('button'); del.className = 'xs-del'; del.textContent = '✕';
                                del.addEventListener('click', () => { const cur = xGet('phrases', []); cur.splice(i, 1); xSet('phrases', cur); renderPhraseList(); });
                                row.appendChild(tin); row.appendChild(cin); row.appendChild(del);
                                phraseList.appendChild(row);
                            });
                        }
                        renderPhraseList();
                        body.appendChild(phraseList);
                        const addPhrase = document.createElement('button');
                        addPhrase.className = 'xs-add'; addPhrase.textContent = '+ Add phrase';
                        addPhrase.addEventListener('click', () => { const cur = xGet('phrases', []); cur.push({ text: '', color: '#1f0000' }); xSet('phrases', cur); renderPhraseList(); });
                        body.appendChild(addPhrase);

                        // ── Hidden Phrases ──
                        body.appendChild(mkSection('🚫 Hidden Phrases'));
                        body.appendChild(mkCheckRow('Hide messages containing these phrases', 'hiddenPhrases.enabled', false));
                        const hidTa = document.createElement('textarea');
                        hidTa.className = 'xs-ta'; hidTa.placeholder = 'one phrase per line';
                        hidTa.value = (xGet('hiddenPhrases', []) || []).join('\n');
                        hidTa.addEventListener('input', () => xSet('hiddenPhrases', hidTa.value.split('\n').map(s => s.trim()).filter(Boolean)));
                        body.appendChild(hidTa);

                        // ── Image Upload ──
                        body.appendChild(mkSection('📎 Image Upload'));
                        body.appendChild(mkCheckRow('Auto-upload pasted images to femboy.beauty', 'imageUpload.enabled', false));

                        panel.appendChild(bar);
                        panel.appendChild(body);
                        chat.appendChild(panel);
                    }

                    // ── Snip button (scissors → ms-screenclip → femboy.beauty) ──────
                    function tryPostSnipStart(payload) {
                        let sent = false;
                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.postMessage === 'function') {
                            try {
                                host.postMessage(payload);
                                sent = true;
                            } catch (error) {
                            }
                        }

                        const topWin = window.top;
                        if (topWin && topWin !== window) {
                            try {
                                const topHost = topWin.chrome && topWin.chrome.webview;
                                if (topHost && typeof topHost.postMessage === 'function') {
                                    topHost.postMessage(payload);
                                    sent = true;
                                }
                            } catch (error) {
                            }
                        }

                        if (topWin && topWin !== window && typeof topWin.postMessage === 'function') {
                            try {
                                topWin.postMessage(payload, '*');
                                sent = true;
                            } catch (error) {
                            }
                        }

                        return sent;
                    }

                    window.__codexSendSnipProbe = function() {
                        const payload = { type: 'codex-snip-start', requestId: String(Date.now()), probe: true };
                        const sent = tryPostSnipStart(payload);
                        postSnipDebug('probe-send', { sent: sent });
                        return sent;
                    };

                    function postSnipDebug(stage, extra) {
                        const data = extra || {};
                        data.type = 'codex-snip-debug';
                        data.stage = stage;
                        data.href = (() => { try { return String(window.location.href || ''); } catch (e) { return ''; } })();
                        data.inIframe = (() => { try { return window.top !== window; } catch (e) { return true; } })();
                        data.hasHost = !!(window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function');
                        data.hasTopHost = (() => {
                            try {
                                return !!(window.top && window.top.chrome && window.top.chrome.webview && typeof window.top.chrome.webview.postMessage === 'function');
                            } catch (error) {
                                return false;
                            }
                        })();
                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.postMessage === 'function') {
                            try {
                                host.postMessage(data);
                                return true;
                            } catch (error) {
                            }
                        }
                        const topWin = window.top;
                        if (topWin && topWin !== window && typeof topWin.postMessage === 'function') {
                            try {
                                topWin.postMessage(data, '*');
                                return true;
                            } catch (error) {
                            }
                        }
                        return false;
                    }

                    function buildSnipBtn() {
                        if (document.getElementById('codex-snip-btn')) return;
                        const ref = document.getElementById('codex-ext-settings-btn')
                            || document.getElementById('chat-settings-btn');
                        if (!ref || !ref.parentElement) return;
                        const btn = document.createElement('a');
                        btn.id = 'codex-snip-btn';
                        btn.className = 'chat-tool-btn';
                        btn.setAttribute('role', 'button');
                        btn.title = 'Snip & upload';
                        btn.setAttribute('data-tippy-content', 'Snip & upload to femboy.beauty');
                        btn.innerHTML = `<i class=""btn-icon"" style=""opacity:1;font-style:normal;font-size:16px;line-height:1;display:flex;align-items:center;justify-content:center;"">✂</i>`;
                        btn.addEventListener('click', e => {
                            e.preventDefault(); e.stopPropagation();
                            // Debug: immediately turn orange so we know click fired
                            btn.style.outline = '2px solid orange';
                            const host = window.chrome && window.chrome.webview;
                            if (!host) {
                                btn.style.outline = '2px solid red';
                                btn.title = 'ERR: no webview host';
                            }
                            btn.title = 'Snipping…';
                            btn.classList.add('codex-split-chat-active');
                            postSnipDebug('click-before-send', {});
                            const payload = { type: 'codex-snip-start', requestId: String(Date.now()) };
                            const sent = tryPostSnipStart(payload);
                            window.__codexLastSnipSendTs = Date.now();
                            window.__codexLastSnipSent = !!sent;
                            postSnipDebug('click-after-send', { sent: sent });
                            if (!sent) {
                                btn.style.outline = '2px solid red';
                                btn.title = 'ERR: snip message failed';
                            }
                        });
                        ref.parentElement.insertBefore(btn, ref);
                    }

                    if (!window.__codexSnipRelayBound) {
                        window.__codexSnipRelayBound = true;
                        window.addEventListener('message', event => {
                            const data = event && event.data;
                            if (!data || typeof data !== 'object') {
                                return;
                            }

                            if (data.type === 'codex-snip-debug') {
                                const hostForDebug = window.chrome && window.chrome.webview;
                                if (hostForDebug && typeof hostForDebug.postMessage === 'function') {
                                    try {
                                        hostForDebug.postMessage(data);
                                    } catch (error) {
                                    }
                                }
                                return;
                            }

                            if (data.type !== 'codex-snip-start') {
                                return;
                            }

                            const host = window.chrome && window.chrome.webview;
                            if (!host || typeof host.postMessage !== 'function') {
                                return;
                            }

                            try {
                                host.postMessage({
                                    type: 'codex-snip-start',
                                    requestId: data.requestId ? String(data.requestId) : String(Date.now()),
                                    relayed: true
                                });
                                postSnipDebug('relay-posted', { sent: true });
                            } catch (error) {
                                postSnipDebug('relay-failed', { sent: false, error: error && error.message ? String(error.message) : 'relay-error' });
                            }
                        });
                    }

                    // ── Receive snip URL back from C# ────────────────────────────
                    if (!window.__codexSnipListenerAdded) {
                        window.__codexSnipListenerAdded = true;
                        window.__codexLastSnipDoneSignature = window.__codexLastSnipDoneSignature || '';
                        window.__codexLastSnipDoneAt = window.__codexLastSnipDoneAt || 0;
                        const applySnipDone = (msg) => {
                            if (!msg || msg.type !== 'codex-snip-done') return;
                            const signature = String(msg.url || '') + '|' + String(msg.error || '');
                            const now = Date.now();
                            if (window.__codexLastSnipDoneSignature === signature && (now - window.__codexLastSnipDoneAt) < 2500) {
                                postSnipDebug('done-duplicate-ignored', { hasUrl: !!msg.url });
                                return;
                            }
                            window.__codexLastSnipDoneSignature = signature;
                            window.__codexLastSnipDoneAt = now;
                            const btn = document.getElementById('codex-snip-btn');
                            if (btn) {
                                btn.style.outline = '2px solid cyan';
                                btn.classList.remove('codex-split-chat-active');
                                btn.title = msg.url ? 'Snip & upload' : ('ERR: ' + (msg.error || 'unknown'));
                            }
                            const findInputTarget = () => {
                                const direct = document.querySelector('#chat-input-control, #chat-input-wrap textarea, #chat-input-frame textarea');
                                if (direct) {
                                    return { input: direct, context: 'main' };
                                }
                                return { input: null, context: 'none' };
                            };
                            if (!msg.url) {
                                const target = findInputTarget();
                                if (target.input) target.input.value = '[snip error: ' + (msg.error || 'unknown') + ']';
                                postSnipDebug('done-error', { target: target.context, error: msg.error || 'unknown' });
                                return;
                            }
                            const target = findInputTarget();
                            const ta = target.input;
                            if (!ta) {
                                postSnipDebug('done-no-input-target', { target: target.context });
                                return;
                            }
                            const s = ta.selectionStart != null ? ta.selectionStart : ta.value.length;
                            const en = ta.selectionEnd != null ? ta.selectionEnd : ta.value.length;
                            const prefix = (s > 0 && ta.value[s - 1] !== ' ') ? ' ' : '';
                            ta.value = ta.value.slice(0, s) + prefix + msg.url + ' ' + ta.value.slice(en);
                            ta.selectionStart = ta.selectionEnd = s + prefix.length + msg.url.length + 1;
                            ta.dispatchEvent(new Event('input', { bubbles: true }));
                            ta.focus();
                            postSnipDebug('done-inserted', { target: target.context, hasUrl: true });
                        };
                        const host = window.chrome && window.chrome.webview;
                        if (host) {
                            host.addEventListener('message', e => {
                                let msg;
                                try { msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data; } catch { return; }
                                if (msg && msg.type === 'codex-snip-ack') {
                                    const btn = document.getElementById('codex-snip-btn');
                                    if (btn) {
                                        btn.style.outline = '2px solid lime';
                                        btn.title = 'Snip acknowledged';
                                    }
                                    return;
                                }
                                applySnipDone(msg);
                            });
                        }
                        if (!window.__codexSnipWindowMessageBound) {
                            window.__codexSnipWindowMessageBound = true;
                            window.addEventListener('message', e => {
                                const msg = e && e.data;
                                if (!msg || msg.type !== 'codex-snip-done') return;
                                applySnipDone(msg);
                            });
                        }
                    }

                    function buildSettingsBtn() {
                        if (document.getElementById('codex-ext-settings-btn')) return;
                        const ref = document.getElementById('chat-settings-btn');
                        if (!ref || !ref.parentElement) return;
                        const btn = document.createElement('a');
                        btn.id = 'codex-ext-settings-btn';
                        btn.className = 'chat-tool-btn';
                        btn.setAttribute('role', 'button');
                        btn.title = 'Chat Extensions';
                        btn.setAttribute('data-tippy-content', 'Chat Extensions');
                        btn.innerHTML = `<i class=""btn-icon"" style=""opacity:1;font-style:normal;font-size:15px;display:flex;align-items:center;justify-content:center;"">⚙</i>`;
                        btn.addEventListener('click', e => {
                            e.preventDefault(); e.stopPropagation();
                            buildSettings();
                            const p = document.getElementById(SETT_ID);
                            const open = p && !p.classList.contains('active');
                            if (p) p.classList.toggle('active', !!open);
                            btn.classList.toggle('codex-split-chat-active', !!open);
                            // close other menus
                            document.querySelectorAll('.chat-menu.active').forEach(m => m.classList.remove('active'));
                        });
                        ref.parentElement.insertBefore(btn, ref);
                    }

                    // ── Bootstrap ─────────────────────────────────────────────────
                    function boot() {
                        if (!document.querySelector('#chat-tools-wrap, .chat-tools-group')) return false;
                        ensureSettingsStyles();
                        ensureDualChatMessageBridge();
                        buildSettingsBtn();
                        removeExtPanelBtn();
                        buildDinkDonkBtn();
                        buildSnipBtn();
                        buildDualChatBtn();
                        requestDualChatSource();
                        setupPhraseInput();
                        setupHiddenPhrases();
                        setupImageUpload();
                        watchMsgNotify();
                        watchDinkDonkChatLines();
                        const ddBtn = document.getElementById('codex-dinkdonk-btn');
                        if (ddBtn && !xGet('dinkdonk.enabled', true)) ddBtn.style.display = 'none';
                        return true;
                    }

                    function waitAndBoot() {
                        if (boot()) return;
                        const obs = new MutationObserver(() => { if (boot()) obs.disconnect(); });
                        obs.observe(document.documentElement || document.body, { childList: true, subtree: true });
                    }

                    if (document.readyState === 'loading') {
                        document.addEventListener('DOMContentLoaded', waitAndBoot, { once: true });
                    } else {
                        waitAndBoot();
                    }
                    window.addEventListener('load', waitAndBoot);
                })();
            ");
        }

        private void ShowFatalError(string message)
        {
            SetStatus("Browser initialization failed.");
            MessageBox.Show(
                this,
                message,
                _config.Title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            SetStatus("Loading " + e.Uri);
            UpdateAddressBox(e.Uri);
            UpdateBigscreenBarVisibility();

            Uri targetUri;
            if (TryBuildUri(e.Uri, out targetUri) && IsAllowedUri(targetUri) && ShouldResetChatLayoutForNavigation(targetUri))
            {
                ResetChatLayoutForModeSwitch();
            }

            if (string.IsNullOrWhiteSpace(e.Uri) || e.Uri.IndexOf("/embed/chat", StringComparison.OrdinalIgnoreCase) < 0)
            {
                _dualChatInputTop = 0;
                _dualChatPaneLeft = 0;
                _dualChatPaneTop = 0;
                _dualChatPaneWidth = 0;
                _dualChatPaneHeight = 0;
                _dualChatLayoutActive = false;
                _lastDualChatLayoutFingerprint = null;
                _embedChatLayoutViewportWidth = 0;
                _embedChatLayoutViewportHeight = 0;
                LayoutDualChatPanel();
            }

            bool navigatingToBigscreen = !string.IsNullOrWhiteSpace(e.Uri) && e.Uri.IndexOf("/bigscreen", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!navigatingToBigscreen && !IsBigscreenHostMode())
            {
                _bigscreenChatTopOffset = 0;
                _bigscreenViewportWidth = 0;
                _bigscreenViewportHeight = 0;
                _lastSnipRequestId = string.Empty;
                _lastSnipRequestUtc = DateTime.MinValue;
                _treatSnipAsProbe = false;
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                SetStatus("Ready.");
                PersistBrowserSession();
                _lastStreamChatSourceJson = null;
                _lastDualChatLayoutFingerprint = null;
                _activeStreamPlayerUrl = null;

                if (_runStreamChatPanelSelfTest && !_streamChatPanelSelfTestStarted)
                {
                    _streamChatPanelSelfTestStarted = true;
                    _suppressPageEmbedsState = true;
                    _pendingSplitSelfTestTask = RunStreamChatPanelSelfTestAsync();
                }

                NotifyDualChatSourceChanged();
                ApplyBigscreenExternalChatLayout(_dualChatHostEnabled && IsBigscreenPage());

                if (_runSplitSelfTest && !_splitSelfTestStarted)
                {
                    _splitSelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunSplitSelfTestAsync();
                }

                if (_runPopoutPauseSelfTest && !_popoutPauseSelfTestStarted)
                {
                    _popoutPauseSelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunPopoutPauseSelfTestAsync();
                }

                if (_runToolbarSelfTest && !_toolbarSelfTestStarted)
                {
                    _toolbarSelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunToolbarSelfTestAsync();
                }

                if (_runDualSelfTest && !_dualSelfTestStarted)
                {
                    _dualSelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunDualSelfTestAsync();
                }

                if (_runBigscreenGeometrySelfTest && !_bigscreenGeometrySelfTestStarted)
                {
                    _bigscreenGeometrySelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunBigscreenGeometrySelfTestAsync();
                }
            }
            else
            {
                SetStatus("Navigation failed: " + e.WebErrorStatus);
            }

            UpdateBigscreenBarVisibility();
            UpdateNavigationButtons();
            UpdateWindowTitle();
        }

        private void OnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (_webView.Source != null)
            {
                UpdateAddressBox(_webView.Source.AbsoluteUri);
            }

            ApplyBigscreenExternalChatLayout(_dualChatHostEnabled && IsBigscreenPage());
            UpdateBigscreenBarVisibility();
            LayoutDualChatPanel();
        }

        private bool IsChatPage()
        {
            Uri currentUri = _webView.Source;
            if (currentUri == null)
            {
                return false;
            }

            return IsChatUri(currentUri);
        }

        private bool IsBigscreenPage()
        {
            Uri currentUri = _webView.Source;
            if (currentUri == null)
            {
                return false;
            }

            return IsBigscreenUri(currentUri);
        }

        private bool IsBigscreenHostMode()
        {
            return IsBigscreenPage() || (_bigscreenBarPanel != null && _bigscreenBarPanel.Visible);
        }

        private static bool IsChatUri(Uri uri)
        {
            return uri != null &&
                uri.AbsolutePath.StartsWith("/embed/chat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBigscreenUri(Uri uri)
        {
            return uri != null &&
                uri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase);
        }

        private async System.Threading.Tasks.Task RunSplitSelfTestAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(2000);
                if (_webView.CoreWebView2 == null)
                {
                    WriteSplitSelfTestResult("{\"ok\":false,\"reason\":\"webview-not-ready\"}");
                    return;
                }

                await _webView.ExecuteScriptAsync(@"
                    (async () => {
                        const host = window.chrome && window.chrome.webview;

                        function finish(payload) {
                            if (host) {
                                host.postMessage(Object.assign({ type: 'split-self-test-result' }, payload || {}));
                            }
                            return true;
                        }

                        function wait(ms) {
                            return new Promise((resolve) => window.setTimeout(resolve, ms));
                        }

                        async function waitFor(predicate, timeoutMs) {
                            const started = Date.now();
                            while ((Date.now() - started) < timeoutMs) {
                                const value = predicate();
                                if (value) {
                                    return value;
                                }
                                await wait(250);
                            }
                            return null;
                        }

                        try {
                        const button = await waitFor(() => document.getElementById('codex-split-chat-btn'), 15000);
                        if (!button) {
                            return finish({
                                ok: false,
                                reason: 'split-button-missing',
                                hasChatToolsWrap: !!document.querySelector('#chat-tools-wrap, .chat-tools-group'),
                                hasWatchingButton: !!document.getElementById('chat-watching-focus-btn'),
                                hasSettingsButton: !!document.getElementById('chat-settings-btn'),
                                hasSnipButton: !!document.getElementById('codex-snip-btn'),
                                hasDualButton: !!document.getElementById('codex-stream-chat-btn')
                            });
                        }

                        if (!button.classList.contains('codex-split-chat-active')) {
                            button.click();
                        }

                        const overlayUser = await waitFor(() => {
                            const overlay = document.getElementById('codex-split-chat-overlay');
                            if (!overlay || !overlay.classList.contains('enabled')) {
                                return null;
                            }

                            return overlay.querySelector('.codex-split-chat-pane.right .user:not(.tier)') ||
                                overlay.querySelector('.codex-split-chat-pane.left .user:not(.tier)');
                        }, 15000);

                        if (!overlayUser) {
                            return finish({ ok: false, reason: 'overlay-user-missing' });
                        }

                        overlayUser.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true, cancelable: true, composed: true }));
                        overlayUser.dispatchEvent(new MouseEvent('mouseover', { bubbles: true, cancelable: true, composed: true }));
                        await wait(100);

                        const hoverUnderline = window.getComputedStyle(overlayUser).textDecorationLine;
                        const hoverInlineStyle = overlayUser.style.textDecoration || '';
                        let splitFocusClearWorked = false;
                        let notifyResetWorked = false;
                        let notifyFound = false;
                        let notifyUnpinnedBefore = false;
                        let notifyUnpinnedAfter = false;
                        let notifyHandlerRan = false;
                        let notifyScrollOffsetAfter = 0;

                        overlayUser.dispatchEvent(new MouseEvent('click', {
                            bubbles: true,
                            cancelable: true,
                            composed: true,
                            button: 0,
                            buttons: 1,
                            detail: 1,
                            clientX: 24,
                            clientY: 24
                        }));
                        await wait(250);

                        const overlay = document.getElementById('codex-split-chat-overlay');
                        const messages = overlay ? Array.from(overlay.querySelectorAll('.codex-split-chat-lines > *')) : [];
                        const dimmedCount = messages.filter((message) => {
                            const opacity = parseFloat(window.getComputedStyle(message).opacity || '1');
                            return opacity < 0.5;
                        }).length;
                        const brightCount = messages.filter((message) => {
                            const opacity = parseFloat(window.getComputedStyle(message).opacity || '1');
                            return opacity > 0.9;
                        }).length;

                        overlayUser.dispatchEvent(new MouseEvent('click', {
                            bubbles: true,
                            cancelable: true,
                            composed: true,
                            button: 0,
                            buttons: 1,
                            detail: 1,
                            clientX: 24,
                            clientY: 24
                        }));
                        await wait(250);
                        {
                            const overlayEl = document.getElementById('codex-split-chat-overlay');
                            splitFocusClearWorked = !(overlayEl && overlayEl.classList.contains('codex-split-chat-focus'));
                        }

                        overlayUser.dispatchEvent(new MouseEvent('contextmenu', {
                            bubbles: true,
                            cancelable: true,
                            composed: true,
                            button: 2,
                            buttons: 2,
                            detail: 1,
                            clientX: 36,
                            clientY: 36
                        }));
                        await wait(500);

                        const userInfo = document.getElementById('chat-user-info');
                        const userInfoStyle = userInfo ? window.getComputedStyle(userInfo) : null;
                        const menuVisible = !!(userInfo &&
                            userInfoStyle &&
                            userInfoStyle.display !== 'none' &&
                            userInfoStyle.visibility !== 'hidden' &&
                            parseFloat(userInfoStyle.opacity || '1') > 0.01);
                        const joinedText = userInfo?.querySelector('.date-subheader')?.textContent?.trim() || '';
                        const watchingText = userInfo?.querySelector('.watching-subheader')?.textContent?.trim() || '';
                        const frame = document.getElementById('chat-output-frame');

                        function sampleVisibleSourceKey() {
                            const overlayEl = document.getElementById('codex-split-chat-overlay');
                            if (!overlayEl) {
                                return '';
                            }

                            const panes = [
                                overlayEl.querySelector('.codex-split-chat-pane.left'),
                                overlayEl.querySelector('.codex-split-chat-pane.right')
                            ];

                            for (const pane of panes) {
                                if (!(pane instanceof Element)) {
                                    continue;
                                }

                                const rect = pane.getBoundingClientRect();
                                if (rect.width < 20 || rect.height < 20) {
                                    continue;
                                }

                                const hit = document.elementFromPoint(
                                    rect.left + (rect.width * 0.5),
                                    rect.top + (rect.height * 0.5)
                                );
                                const message = hit?.closest('[data-codex-source-id], [data-codex-source-index]');
                                if (message) {
                                    return message.getAttribute('data-codex-source-id') ||
                                        message.getAttribute('data-codex-source-text') ||
                                        message.getAttribute('data-codex-source-index') ||
                                        '';
                                }
                            }

                            return '';
                        }

                        const sourceOutput = Array.from(document.querySelectorAll('#chat-output-frame .chat-output')).find((output) => {
                            const style = window.getComputedStyle(output);
                            return style.display !== 'none';
                        });
                        const sourceLines = sourceOutput?.querySelector('.chat-lines');
                        let dualChatButtonFound = false;
                        let dualChatEnabledWorked = false;
                        let dualChatFrameSrc = '';
                        let dualBodyEnabled = false;
                        let dualPanePresent = false;
                        let dualPaneEnabled = false;
                        let dualPaneEmpty = false;
                        let dualButtonActive = false;
                        let dualViaEventWorked = false;
                        let censoredRevealWorked = false;
                        let censoredOverlayFound = false;
                        let censoredOriginalFound = false;
                        let scrollHoldWorked = false;
                        let scrollAnchorBefore = '';
                        let scrollAnchorAfter = '';
                        let extSettingsButtonFound = false;
                        let extSettingsOpenWorked = false;
                        let extSettingsCloseWorked = false;
                        let extSettingsPersistWorked = false;
                        let watchedUsersPersistWorked = false;
                        let activityTogglesWorked = false;
                        let activityFunctionWorked = false;
                        let notificationsFunctionWorked = false;
                        let dinkDonkTogglesWorked = false;
                        let dinkDonkButtonToggleWorked = false;
                        let dinkDonkAutoOpenFunctionWorked = false;
                        let externalChatUrlPersistWorked = false;
                        let externalChatPanelButtonAbsent = false;
                        let chatInputDoubleClickToggleWorked = false;
                        let chatInputDoubleClickFunctionWorked = false;
                        let phraseHighlightsToggleWorked = false;
                        let phraseAddButtonFound = false;
                        let phraseAddFunctionWorked = false;
                        let phraseHighlightFunctionWorked = false;
                        let hiddenPhrasesToggleWorked = false;
                        let hiddenPhrasesPersistWorked = false;
                        let hiddenPhrasesFunctionWorked = false;
                        let imageUploadToggleWorked = false;
                        let imageUploadFunctionWorked = false;
                        let snipButtonFound = false;
                        let snipActivateWorked = false;

                        const dualButton = await waitFor(() => document.getElementById('codex-stream-chat-btn'), 5000);
                        dualChatButtonFound = !!dualButton;
                        if (dualButton) {
                            document.dispatchEvent(new CustomEvent('codex:dualchat-source', {
                                detail: {
                                    type: 'codex-dual-chat-source',
                                    available: true,
                                    platform: 'twitch',
                                    platformLabel: 'Twitch',
                                    chatUrl: 'https://example.com/dual-chat-self-test'
                                }
                            }));
                            await wait(100);

                            dualButton.click();
                            await wait(400);

                            const dualPane = document.getElementById('codex-dual-stream-pane');
                            dualBodyEnabled = document.body.classList.contains('codex-dual-chat-enabled');
                            dualPanePresent = !!dualPane;
                            dualPaneEnabled = !!(dualPane && dualPane.classList.contains('enabled'));
                            dualPaneEmpty = !!(dualPane && dualPane.classList.contains('empty'));
                            dualButtonActive = dualButton.classList.contains('codex-split-chat-active');
                            dualChatFrameSrc = dualPane?.getAttribute('data-codex-chat-url') ||
                                dualPane?.querySelector('.codex-dual-chat-frame')?.getAttribute('src') || '';
                            dualChatEnabledWorked = !!(dualPane &&
                                dualPane.classList.contains('enabled') &&
                                document.body.classList.contains('codex-dual-chat-enabled') &&
                                dualChatFrameSrc &&
                                dualChatFrameSrc !== 'about:blank');

                            if (!dualChatEnabledWorked) {
                                document.dispatchEvent(new CustomEvent('codex:dualchat-set', {
                                    detail: { enabled: true }
                                }));
                                await wait(250);

                                const dualPaneAfterEvent = document.getElementById('codex-dual-stream-pane');
                                const dualFrameAfterEvent = dualPaneAfterEvent?.getAttribute('data-codex-chat-url') ||
                                    dualPaneAfterEvent?.querySelector('.codex-dual-chat-frame')?.getAttribute('src') || '';
                                dualViaEventWorked = !!(dualPaneAfterEvent &&
                                    dualPaneAfterEvent.classList.contains('enabled') &&
                                    document.body.classList.contains('codex-dual-chat-enabled') &&
                                    dualFrameAfterEvent &&
                                    dualFrameAfterEvent !== 'about:blank');

                                dualChatFrameSrc = dualChatFrameSrc || dualFrameAfterEvent;
                                dualPanePresent = dualPanePresent || !!dualPaneAfterEvent;
                                dualPaneEnabled = dualPaneEnabled || !!(dualPaneAfterEvent && dualPaneAfterEvent.classList.contains('enabled'));
                                dualPaneEmpty = dualPaneEmpty || !!(dualPaneAfterEvent && dualPaneAfterEvent.classList.contains('empty'));
                                dualButtonActive = dualButtonActive || dualButton.classList.contains('codex-split-chat-active');
                                dualBodyEnabled = dualBodyEnabled || document.body.classList.contains('codex-dual-chat-enabled');
                                dualChatEnabledWorked = dualChatEnabledWorked || dualViaEventWorked;
                            }

                            dualButton.click();
                            await wait(200);

                            document.dispatchEvent(new CustomEvent('codex:split-set', {
                                detail: { enabled: true }
                            }));
                            await waitFor(() => {
                                const overlayEl = document.getElementById('codex-split-chat-overlay');
                                return overlayEl && overlayEl.classList.contains('enabled');
                            }, 5000);
                        }

                        if (sourceLines) {
                            const synthetic = document.createElement('div');
                            synthetic.className = 'msg-chat censored';
                            synthetic.setAttribute('data-username', 'codexcensored');
                            synthetic.setAttribute('data-id', `codex-censored-${Date.now()}`);
                            synthetic.innerHTML = '<span class=""user"">codexcensored</span><span class=""text"">synthetic censored line</span>';
                            sourceLines.appendChild(synthetic);

                            const overlayCensored = await waitFor(() => {
                                const overlayEl = document.getElementById('codex-split-chat-overlay');
                                return overlayEl?.querySelector('[data-codex-source-username=""codexcensored""]');
                            }, 5000);
                            censoredOverlayFound = !!overlayCensored;
                            censoredOriginalFound = synthetic.classList.contains('censored');

                            if (overlayCensored) {
                                overlayCensored.dispatchEvent(new MouseEvent('click', {
                                    bubbles: true,
                                    cancelable: true,
                                    composed: true,
                                    button: 0,
                                    buttons: 1,
                                    detail: 1,
                                    clientX: 28,
                                    clientY: 28
                                }));
                                await wait(250);
                                censoredRevealWorked = !synthetic.classList.contains('censored');
                            }

                            synthetic.remove();
                        }

                        if (frame && sourceLines && sourceLines.children.length > 20) {
                            frame.dispatchEvent(new WheelEvent('wheel', {
                                deltaY: -Math.max(220, frame.clientHeight * 0.9),
                                bubbles: true,
                                cancelable: true,
                                composed: true
                            }));
                            await wait(400);

                            scrollAnchorBefore = sampleVisibleSourceKey();

                            const removedTop = sourceLines.firstElementChild;
                            const removedNextSibling = removedTop ? removedTop.nextSibling : null;
                            const appended = document.createElement('div');
                            appended.className = 'msg-chat';
                            appended.setAttribute('data-username', 'codexscroll');
                            appended.setAttribute('data-id', `codex-scroll-${Date.now()}`);
                            appended.innerHTML = '<span class=""user"">codexscroll</span><span class=""text"">synthetic scroll hold line</span>';

                            if (removedTop) {
                                removedTop.remove();
                            }
                            sourceLines.appendChild(appended);
                            await wait(500);

                            scrollAnchorAfter = sampleVisibleSourceKey();
                            scrollHoldWorked = !!scrollAnchorBefore && scrollAnchorBefore === scrollAnchorAfter;

                            appended.remove();
                            if (removedTop) {
                                if (removedNextSibling && removedNextSibling.parentNode === sourceLines) {
                                    sourceLines.insertBefore(removedTop, removedNextSibling);
                                } else {
                                    sourceLines.insertBefore(removedTop, sourceLines.firstChild);
                                }
                            }

                            const notify = document.querySelector('#codex-split-chat-overlay .codex-split-chat-notify');
                            notifyFound = !!notify;
                            if (notify) {
                                const overlayBeforeNotify = document.getElementById('codex-split-chat-overlay');
                                notifyUnpinnedBefore = !!(overlayBeforeNotify && overlayBeforeNotify.classList.contains('codex-split-chat-unpinned'));
                                const notifyStampBefore = overlayBeforeNotify?.getAttribute('data-codex-notify-click-ts') || '';
                                notify.dispatchEvent(new MouseEvent('click', {
                                    bubbles: true,
                                    cancelable: true,
                                    composed: true,
                                    button: 0,
                                    buttons: 1,
                                    detail: 1,
                                    clientX: 30,
                                    clientY: 30
                                }));
                                await wait(250);
                                const overlayEl = document.getElementById('codex-split-chat-overlay');
                                notifyUnpinnedAfter = !!(overlayEl && overlayEl.classList.contains('codex-split-chat-unpinned'));
                                const notifyStampAfter = overlayEl?.getAttribute('data-codex-notify-click-ts') || '';
                                notifyHandlerRan = !!notifyStampAfter && notifyStampAfter !== notifyStampBefore;
                                notifyResetWorked = !notifyUnpinnedAfter;
                            }
                        }

                        const extSettingsBtn = await waitFor(() => document.getElementById('codex-ext-settings-btn'), 5000);
                        extSettingsButtonFound = !!extSettingsBtn;
                        if (extSettingsBtn) {
                            extSettingsBtn.dispatchEvent(new MouseEvent('click', {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                button: 0,
                                buttons: 1,
                                detail: 1,
                                clientX: 16,
                                clientY: 16
                            }));
                            await wait(220);

                            const extPanel = document.getElementById('codex-ext-settings');
                            extSettingsOpenWorked = !!(extPanel &&
                                extPanel.classList.contains('active') &&
                                extSettingsBtn.classList.contains('codex-split-chat-active'));

                            const notificationsCheckbox = extPanel
                                ? extPanel.querySelector('#xs-cb-notifications-enabled')
                                : null;
                            if (notificationsCheckbox instanceof HTMLInputElement) {
                                const before = notificationsCheckbox.checked;
                                notificationsCheckbox.click();
                                await wait(120);
                                let stored = null;
                                try {
                                    stored = JSON.parse(localStorage.getItem('codex-ext.notifications.enabled') || 'null');
                                } catch (error) {
                                    stored = null;
                                }
                                extSettingsPersistWorked = (stored === notificationsCheckbox.checked);
                                if (notificationsCheckbox.checked !== before) {
                                    notificationsCheckbox.click();
                                    await wait(80);
                                }
                            }

                            function readStored(key) {
                                try {
                                    return JSON.parse(localStorage.getItem('codex-ext.' + key) || 'null');
                                } catch (error) {
                                    return null;
                                }
                            }
                            function writeStored(key, value) {
                                try {
                                    localStorage.setItem('codex-ext.' + key, JSON.stringify(value));
                                } catch (error) {
                                }
                            }

                            async function verifyToggle(id, key) {
                                const cb = extPanel ? extPanel.querySelector('#' + id) : null;
                                if (!(cb instanceof HTMLInputElement)) {
                                    return false;
                                }
                                const before = cb.checked;
                                cb.click();
                                await wait(80);
                                const storedAfterOn = readStored(key);
                                const toggled = cb.checked !== before && storedAfterOn === cb.checked;
                                cb.click();
                                await wait(80);
                                const storedAfterRestore = readStored(key);
                                const restored = cb.checked === before && storedAfterRestore === cb.checked;
                                return toggled && restored;
                            }

                            const watchedUsersArea = extPanel
                                ? extPanel.querySelector('textarea.xs-ta[placeholder*=""destiny""]')
                                : null;
                            if (watchedUsersArea instanceof HTMLTextAreaElement) {
                                const beforeText = watchedUsersArea.value;
                                watchedUsersArea.value = 'destiny\ncodexselftest';
                                watchedUsersArea.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const storedWatched = readStored('watchedUsers');
                                const writeWorked = Array.isArray(storedWatched) &&
                                    storedWatched.length === 2 &&
                                    String(storedWatched[0] || '').toLowerCase() === 'destiny' &&
                                    String(storedWatched[1] || '').toLowerCase() === 'codexselftest';
                                watchedUsersArea.value = beforeText;
                                watchedUsersArea.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const restoredWatched = readStored('watchedUsers');
                                const beforeList = beforeText.split('\n').map(s => s.trim()).filter(Boolean);
                                const restored = Array.isArray(restoredWatched) &&
                                    restoredWatched.length === beforeList.length &&
                                    restoredWatched.every((v, idx) => String(v || '') === String(beforeList[idx] || ''));
                                watchedUsersPersistWorked = writeWorked && restored;
                            }

                            activityTogglesWorked =
                                await verifyToggle('xs-cb-activity-enabled', 'activity.enabled') &&
                                await verifyToggle('xs-cb-activity-join', 'activity.join') &&
                                await verifyToggle('xs-cb-activity-quit', 'activity.quit') &&
                                await verifyToggle('xs-cb-activity-embed', 'activity.embed') &&
                                await verifyToggle('xs-cb-notifications-enabled', 'notifications.enabled');

                            const dinkDonkCheckbox = extPanel ? extPanel.querySelector('#xs-cb-dinkdonk-enabled') : null;
                            const ddButton = document.getElementById('codex-dinkdonk-btn');
                            if (dinkDonkCheckbox instanceof HTMLInputElement && ddButton) {
                                const beforeDisplay = window.getComputedStyle(ddButton).display;
                                const beforeChecked = dinkDonkCheckbox.checked;
                                dinkDonkCheckbox.click();
                                await wait(100);
                                const hiddenDisplay = window.getComputedStyle(ddButton).display;
                                const hiddenWorked = hiddenDisplay === 'none';
                                dinkDonkCheckbox.click();
                                await wait(100);
                                const restoredDisplay = window.getComputedStyle(ddButton).display;
                                const restoredWorked = restoredDisplay !== 'none' && dinkDonkCheckbox.checked === beforeChecked;
                                dinkDonkButtonToggleWorked = hiddenWorked && restoredWorked;
                                const storedDinkDonk = readStored('dinkdonk.enabled');
                                dinkDonkTogglesWorked = (storedDinkDonk === beforeChecked);
                            }
                            dinkDonkTogglesWorked = dinkDonkTogglesWorked &&
                                await verifyToggle('xs-cb-dinkdonk-autoOpenPoll', 'dinkdonk.autoOpenPoll');

                            const allInputs = extPanel ? Array.from(extPanel.querySelectorAll('input[type=""text""]')) : [];
                            const externalUrlInput = allInputs.find((input) => {
                                return input instanceof HTMLInputElement &&
                                    String(input.placeholder || '').toLowerCase().indexOf('#kick/username') >= 0;
                            });
                            if (externalUrlInput instanceof HTMLInputElement) {
                                const before = externalUrlInput.value;
                                externalUrlInput.value = '#kick/codexselftest';
                                externalUrlInput.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const stored = readStored('externalChat.url');
                                const writeWorked = stored === '#kick/codexselftest';
                                externalUrlInput.value = before;
                                externalUrlInput.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const restored = readStored('externalChat.url') === before;
                                externalChatUrlPersistWorked = writeWorked && restored;
                            }

                            chatInputDoubleClickToggleWorked = await verifyToggle('xs-cb-chatInput-doubleClick', 'chatInput.doubleClick');
                            phraseHighlightsToggleWorked = await verifyToggle('xs-cb-phrases-enabled', 'phrases.enabled');
                            phraseAddButtonFound = !!(extPanel && Array.from(extPanel.querySelectorAll('button')).some((buttonEl) => {
                                return String(buttonEl.textContent || '').trim().toLowerCase() === '+ add phrase';
                            }));
                            hiddenPhrasesToggleWorked = await verifyToggle('xs-cb-hiddenPhrases-enabled', 'hiddenPhrases.enabled');

                            const hiddenPhrasesArea = extPanel
                                ? extPanel.querySelector('textarea.xs-ta[placeholder*=""one phrase per line""]')
                                : null;
                            if (hiddenPhrasesArea instanceof HTMLTextAreaElement) {
                                const before = hiddenPhrasesArea.value;
                                hiddenPhrasesArea.value = 'codex-hidden-alpha\ncodex-hidden-beta';
                                hiddenPhrasesArea.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const stored = readStored('hiddenPhrases');
                                const writeWorked = Array.isArray(stored) &&
                                    stored.length === 2 &&
                                    stored[0] === 'codex-hidden-alpha' &&
                                    stored[1] === 'codex-hidden-beta';
                                hiddenPhrasesArea.value = before;
                                hiddenPhrasesArea.dispatchEvent(new Event('input', { bubbles: true }));
                                await wait(80);
                                const restored = readStored('hiddenPhrases');
                                const beforeLines = before.split('\n').map(s => s.trim()).filter(Boolean);
                                hiddenPhrasesPersistWorked = writeWorked &&
                                    Array.isArray(restored) &&
                                    restored.length === beforeLines.length &&
                                    restored.every((v, idx) => String(v || '') === String(beforeLines[idx] || ''));
                            }

                            imageUploadToggleWorked = await verifyToggle('xs-cb-imageUpload-enabled', 'imageUpload.enabled');

                            // Functional checks for extension behavior (not only persistence)
                            const chatLines = document.querySelector('.chat-lines');
                            if (chatLines instanceof Element && typeof window.__codexEmitWsTest === 'function') {
                                const watchedBefore = readStored('watchedUsers');
                                const activityEnabledBefore = readStored('activity.enabled');
                                const activityJoinBefore = readStored('activity.join');
                                const activityQuitBefore = readStored('activity.quit');
                                const activityEmbedBefore = readStored('activity.embed');
                                writeStored('watchedUsers', ['codexwatch']);
                                writeStored('activity.enabled', true);
                                writeStored('activity.join', true);
                                writeStored('activity.quit', true);
                                writeStored('activity.embed', true);
                                const baseInfoCount = chatLines.querySelectorAll('.msg-info').length;
                                window.__codexEmitWsTest('JOIN ' + JSON.stringify({ nick: 'codexwatch' }));
                                window.__codexEmitWsTest('QUIT ' + JSON.stringify({ nick: 'codexwatch' }));
                                window.__codexEmitWsTest('UPDATEUSER ' + JSON.stringify({
                                    nick: 'codexwatch',
                                    watching: { title: 'codex stream', platform: 'kick', channel: 'codexwatch' }
                                }));
                                await wait(160);
                                const infoMessages = Array.from(chatLines.querySelectorAll('.msg-info')).slice(baseInfoCount);
                                const infoText = infoMessages.map((n) => (n.textContent || '').toLowerCase()).join(' ');
                                activityFunctionWorked = infoText.indexOf('codexwatch joined') >= 0 &&
                                    infoText.indexOf('codexwatch left') >= 0 &&
                                    infoText.indexOf('watching') >= 0;
                                writeStored('watchedUsers', watchedBefore);
                                writeStored('activity.enabled', activityEnabledBefore);
                                writeStored('activity.join', activityJoinBefore);
                                writeStored('activity.quit', activityQuitBefore);
                                writeStored('activity.embed', activityEmbedBefore);
                            }

                            {
                                const watchedBefore = readStored('watchedUsers');
                                const notificationsBefore = readStored('notifications.enabled');
                                const originalNotification = window.Notification;
                                let notifyHit = false;
                                try {
                                    writeStored('watchedUsers', ['codexnotify']);
                                    writeStored('notifications.enabled', true);
                                    const FakeNotification = function(title, opts) {
                                        notifyHit = !!title && !!(opts && opts.body);
                                    };
                                    FakeNotification.permission = 'granted';
                                    window.Notification = FakeNotification;
                                    const msg = document.createElement('div');
                                    msg.className = 'msg-user';
                                    msg.setAttribute('data-username', 'codexnotify');
                                    msg.innerHTML = '<span class=""text"">codex notification ping</span>';
                                    const lines = document.querySelector('.chat-lines');
                                    if (lines) {
                                        lines.appendChild(msg);
                                        await wait(120);
                                        notificationsFunctionWorked = notifyHit;
                                        msg.remove();
                                    }
                                } catch (error) {
                                    notificationsFunctionWorked = false;
                                } finally {
                                    window.Notification = originalNotification;
                                    writeStored('watchedUsers', watchedBefore);
                                    writeStored('notifications.enabled', notificationsBefore);
                                }
                            }

                            {
                                const autoOpenBefore = readStored('dinkdonk.autoOpenPoll');
                                const originalOpen = window.open;
                                let openedUrl = '';
                                try {
                                    writeStored('dinkdonk.autoOpenPoll', true);
                                    window.open = function(url) { openedUrl = String(url || ''); return null; };
                                    if (typeof window.__codexEmitWsTest === 'function') {
                                        window.__codexEmitWsTest('BROADCAST ' + JSON.stringify({
                                            nick: 'DinkDonk_bot',
                                            data: 'new poll https://dinkdonk.mov/poll/codex-auto-open'
                                        }));
                                        await wait(120);
                                        dinkDonkAutoOpenFunctionWorked = openedUrl.toLowerCase().indexOf('dinkdonk.mov') >= 0;
                                    }
                                } catch (error) {
                                    dinkDonkAutoOpenFunctionWorked = false;
                                } finally {
                                    window.open = originalOpen;
                                    writeStored('dinkdonk.autoOpenPoll', autoOpenBefore);
                                }
                            }

                            externalChatPanelButtonAbsent = !document.getElementById('codex-ext-panel-btn');

                            {
                                const ta = document.querySelector('#chat-input-control');
                                const dcBefore = readStored('chatInput.doubleClick');
                                if (ta instanceof HTMLTextAreaElement) {
                                    const beforeValue = ta.value;
                                    writeStored('chatInput.doubleClick', true);
                                    const probeUser = document.createElement('span');
                                    probeUser.className = 'user';
                                    probeUser.textContent = 'CodexPing';
                                    document.body.appendChild(probeUser);
                                    probeUser.dispatchEvent(new MouseEvent('dblclick', {
                                        bubbles: true,
                                        cancelable: true,
                                        composed: true,
                                        button: 0,
                                        buttons: 1,
                                        detail: 2
                                    }));
                                    await wait(80);
                                    chatInputDoubleClickFunctionWorked = ta.value.indexOf('CodexPing') >= 0;
                                    probeUser.remove();
                                    ta.value = beforeValue;
                                    ta.dispatchEvent(new Event('input', { bubbles: true }));
                                    writeStored('chatInput.doubleClick', dcBefore);
                                }
                            }

                            {
                                const ta = document.querySelector('#chat-input-control');
                                const phrasesEnabledBefore = readStored('phrases.enabled');
                                const phrasesBefore = readStored('phrases');
                                if (ta instanceof HTMLTextAreaElement) {
                                    const beforeValue = ta.value;
                                    writeStored('phrases.enabled', true);
                                    writeStored('phrases', [{ text: 'codexphrase', color: '#112233' }]);
                                    ta.value = 'contains codexphrase trigger';
                                    ta.dispatchEvent(new Event('input', { bubbles: true }));
                                    await wait(80);
                                    const bg = String(ta.style.backgroundColor || '').toLowerCase();
                                    phraseHighlightFunctionWorked = bg.indexOf('17') >= 0 || bg.indexOf('34') >= 0 || bg.indexOf('51') >= 0 || bg.indexOf('#112233') >= 0;
                                    const beforeCount = Array.isArray(readStored('phrases')) ? readStored('phrases').length : 0;
                                    const addPhraseButton = extPanel
                                        ? Array.from(extPanel.querySelectorAll('button')).find((buttonEl) => String(buttonEl.textContent || '').trim().toLowerCase() === '+ add phrase')
                                        : null;
                                    if (addPhraseButton) {
                                        addPhraseButton.click();
                                        await wait(80);
                                        const afterCount = Array.isArray(readStored('phrases')) ? readStored('phrases').length : 0;
                                        phraseAddFunctionWorked = afterCount === (beforeCount + 1);
                                    }
                                    ta.value = beforeValue;
                                    ta.dispatchEvent(new Event('input', { bubbles: true }));
                                    ta.style.backgroundColor = '';
                                    writeStored('phrases.enabled', phrasesEnabledBefore);
                                    writeStored('phrases', phrasesBefore);
                                }
                            }

                            {
                                const hiddenEnabledBefore = readStored('hiddenPhrases.enabled');
                                const hiddenBefore = readStored('hiddenPhrases');
                                const lines = document.querySelector('.chat-lines');
                                if (lines instanceof Element) {
                                    writeStored('hiddenPhrases.enabled', true);
                                    writeStored('hiddenPhrases', ['codex-hidden-marker']);
                                    const hiddenMsg = document.createElement('div');
                                    hiddenMsg.className = 'msg-user';
                                    hiddenMsg.innerHTML = '<span class=""text"">this has codex-hidden-marker inside</span>';
                                    lines.appendChild(hiddenMsg);
                                    await wait(100);
                                    hiddenPhrasesFunctionWorked = hiddenMsg.style.display === 'none';
                                    hiddenMsg.remove();
                                    writeStored('hiddenPhrases.enabled', hiddenEnabledBefore);
                                    writeStored('hiddenPhrases', hiddenBefore);
                                }
                            }

                            {
                                const ta = document.querySelector('#chat-input-control');
                                const imageUploadBefore = readStored('imageUpload.enabled');
                                const originalFetch = window.fetch;
                                let fetchCalled = false;
                                if (ta instanceof HTMLTextAreaElement && typeof File !== 'undefined') {
                                    try {
                                        writeStored('imageUpload.enabled', true);
                                        window.fetch = async function() {
                                            fetchCalled = true;
                                            return {
                                                ok: true,
                                                json: async () => ({ link: 'https://femboy.beauty/codex-self-test' })
                                            };
                                        };
                                        ta.value = '';
                                        ta.selectionStart = 0;
                                        ta.selectionEnd = 0;
                                        const testFile = new File([new Blob(['codex'])], 'codex.png', { type: 'image/png' });
                                        const pasteEvt = new Event('paste', { bubbles: true, cancelable: true });
                                        Object.defineProperty(pasteEvt, 'clipboardData', {
                                            value: {
                                                items: [{
                                                    type: 'image/png',
                                                    getAsFile: function() { return testFile; }
                                                }]
                                            }
                                        });
                                        ta.dispatchEvent(pasteEvt);
                                        await wait(220);
                                        imageUploadFunctionWorked = fetchCalled && ta.value.indexOf('https://femboy.beauty/codex-self-test') >= 0;
                                        ta.value = '';
                                        ta.dispatchEvent(new Event('input', { bubbles: true }));
                                    } catch (error) {
                                        imageUploadFunctionWorked = false;
                                    } finally {
                                        window.fetch = originalFetch;
                                        writeStored('imageUpload.enabled', imageUploadBefore);
                                    }
                                }
                            }

                            extSettingsBtn.dispatchEvent(new MouseEvent('click', {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                button: 0,
                                buttons: 1,
                                detail: 1,
                                clientX: 16,
                                clientY: 16
                            }));
                            await wait(180);
                            extSettingsCloseWorked = !!(!document.getElementById('codex-ext-settings')?.classList.contains('active') &&
                                !extSettingsBtn.classList.contains('codex-split-chat-active'));
                        }

                        const snipButton = document.getElementById('codex-snip-btn');
                        snipButtonFound = !!snipButton;
                        if (snipButton) {
                            window.__codexLastSnipSendTs = 0;
                            window.__codexLastSnipSent = false;

                            snipButton.dispatchEvent(new MouseEvent('click', {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                button: 0,
                                buttons: 1,
                                detail: 1,
                                clientX: 18,
                                clientY: 18
                            }));
                            await wait(160);

                            const snipTitle = (snipButton.getAttribute('title') || '').toLowerCase();
                            const snipSent = !!window.__codexLastSnipSent;
                            const snipSentAt = Number(window.__codexLastSnipSendTs || 0);
                            const snipSentRecently = snipSentAt > 0;
                            const snipUiAcknowledged = snipButton.classList.contains('codex-split-chat-active') ||
                                snipTitle.indexOf('snipping') >= 0 ||
                                snipTitle.indexOf('acknowledged') >= 0;
                            snipActivateWorked = snipUiAcknowledged &&
                                snipSent &&
                                snipSentRecently;

                            window.__codexLastSnipSendTs = 0;
                            window.__codexLastSnipSent = false;
                            snipButton.classList.remove('codex-split-chat-active');
                            snipButton.setAttribute('title', 'Snip & upload');
                        }

                        const inputRect = (document.querySelector('#chat-input-control')
                            || document.querySelector('#chat-input-wrap')
                            || document.querySelector('#chat-input-frame'))?.getBoundingClientRect?.() || null;
                        const frameRect = frame?.getBoundingClientRect?.() || null;
                        const sourceRect = sourceOutput?.getBoundingClientRect?.() || null;

                        const failedChecks = [];
                        if (hoverUnderline !== 'underline' && hoverInlineStyle !== 'underline') {
                            failedChecks.push('hover-user-underline');
                        }
                        if (!menuVisible) {
                            failedChecks.push('user-menu-visible');
                        }
                        if (!(dimmedCount > 0 && brightCount > 0)) {
                            failedChecks.push('focus-dim-highlight');
                        }
                        if (!joinedText || joinedText.length < 8) {
                            failedChecks.push('joined-label');
                        }
                        if (!dualChatButtonFound) {
                            failedChecks.push('dual-chat-button-found');
                        }
                        if (!(dualChatEnabledWorked || dualViaEventWorked)) {
                            failedChecks.push('dual-chat-enable');
                        }
                        if (!censoredOverlayFound || !censoredOriginalFound || !censoredRevealWorked) {
                            failedChecks.push('censored-reveal');
                        }
                        if (!scrollHoldWorked) {
                            failedChecks.push('scroll-hold');
                        }
                        if (!splitFocusClearWorked) {
                            failedChecks.push('focus-clear');
                        }
                        const clickedUserValue = overlayUser && overlayUser.textContent
                            ? overlayUser.textContent.trim()
                            : '';
                        if (!clickedUserValue) {
                            failedChecks.push('overlay-user-click');
                        }
                        if (!inputRect || !(inputRect.width > 0)) {
                            failedChecks.push('chat-input-visible');
                        }
                        if (!extSettingsButtonFound) {
                            failedChecks.push('ext-settings-button-found');
                        }
                        if (!(extSettingsOpenWorked && extSettingsCloseWorked)) {
                            failedChecks.push('ext-settings-toggle');
                        }
                        if (!extSettingsPersistWorked) {
                            failedChecks.push('ext-settings-persist');
                        }
                        if (!watchedUsersPersistWorked) {
                            failedChecks.push('watched-users-persist');
                        }
                        if (!activityTogglesWorked) {
                            failedChecks.push('activity-toggles');
                        }
                        if (!activityFunctionWorked) {
                            failedChecks.push('activity-function');
                        }
                        if (!notificationsFunctionWorked) {
                            failedChecks.push('notifications-function');
                        }
                        if (!dinkDonkTogglesWorked) {
                            failedChecks.push('dinkdonk-toggles');
                        }
                        if (!dinkDonkButtonToggleWorked) {
                            failedChecks.push('dinkdonk-button-toggle');
                        }
                        if (!dinkDonkAutoOpenFunctionWorked) {
                            failedChecks.push('dinkdonk-autoopen-function');
                        }
                        if (!externalChatUrlPersistWorked) {
                            failedChecks.push('external-chat-url-persist');
                        }
                        if (!externalChatPanelButtonAbsent) {
                            failedChecks.push('external-chat-panel-button-absent');
                        }
                        if (!chatInputDoubleClickToggleWorked) {
                            failedChecks.push('chat-input-doubleclick-toggle');
                        }
                        if (!chatInputDoubleClickFunctionWorked) {
                            failedChecks.push('chat-input-doubleclick-function');
                        }
                        if (!phraseHighlightsToggleWorked) {
                            failedChecks.push('phrase-highlights-toggle');
                        }
                        if (!phraseAddButtonFound) {
                            failedChecks.push('phrase-add-button');
                        }
                        if (!phraseAddFunctionWorked) {
                            failedChecks.push('phrase-add-function');
                        }
                        if (!phraseHighlightFunctionWorked) {
                            failedChecks.push('phrase-highlight-function');
                        }
                        if (!hiddenPhrasesToggleWorked) {
                            failedChecks.push('hidden-phrases-toggle');
                        }
                        if (!hiddenPhrasesPersistWorked) {
                            failedChecks.push('hidden-phrases-persist');
                        }
                        if (!hiddenPhrasesFunctionWorked) {
                            failedChecks.push('hidden-phrases-function');
                        }
                        if (!imageUploadToggleWorked) {
                            failedChecks.push('image-upload-toggle');
                        }
                        if (!imageUploadFunctionWorked) {
                            failedChecks.push('image-upload-function');
                        }
                        if (!snipButtonFound) {
                            failedChecks.push('snip-button-found');
                        }
                        if (!snipActivateWorked) {
                            failedChecks.push('snip-activate');
                        }

                        return finish({
                            ok: failedChecks.length === 0,
                            failedChecks: failedChecks,
                            hoverUnderline: hoverUnderline,
                            hoverInlineStyle: hoverInlineStyle,
                            dimmedCount: dimmedCount,
                            brightCount: brightCount,
                            menuVisible: menuVisible,
                            joinedText: joinedText,
                            watchingText: watchingText,
                            dualChatButtonFound: dualChatButtonFound,
                            dualChatEnabledWorked: dualChatEnabledWorked,
                            dualChatFrameSrc: dualChatFrameSrc,
                            dualBodyEnabled: dualBodyEnabled,
                            dualPanePresent: dualPanePresent,
                            dualPaneEnabled: dualPaneEnabled,
                            dualPaneEmpty: dualPaneEmpty,
                            dualButtonActive: dualButtonActive,
                            dualViaEventWorked: dualViaEventWorked,
                            censoredOverlayFound: censoredOverlayFound,
                            censoredOriginalFound: censoredOriginalFound,
                            censoredRevealWorked: censoredRevealWorked,
                            scrollHoldWorked: scrollHoldWorked,
                            splitFocusClearWorked: splitFocusClearWorked,
                            notifyResetWorked: notifyResetWorked,
                            notifyFound: notifyFound,
                            notifyUnpinnedBefore: notifyUnpinnedBefore,
                            notifyUnpinnedAfter: notifyUnpinnedAfter,
                            notifyHandlerRan: notifyHandlerRan,
                            notifyScrollOffsetAfter: notifyScrollOffsetAfter,
                            extSettingsButtonFound: extSettingsButtonFound,
                            extSettingsOpenWorked: extSettingsOpenWorked,
                            extSettingsCloseWorked: extSettingsCloseWorked,
                            extSettingsPersistWorked: extSettingsPersistWorked,
                            watchedUsersPersistWorked: watchedUsersPersistWorked,
                            activityTogglesWorked: activityTogglesWorked,
                            activityFunctionWorked: activityFunctionWorked,
                            notificationsFunctionWorked: notificationsFunctionWorked,
                            dinkDonkTogglesWorked: dinkDonkTogglesWorked,
                            dinkDonkButtonToggleWorked: dinkDonkButtonToggleWorked,
                            dinkDonkAutoOpenFunctionWorked: dinkDonkAutoOpenFunctionWorked,
                            externalChatUrlPersistWorked: externalChatUrlPersistWorked,
                            externalChatPanelButtonAbsent: externalChatPanelButtonAbsent,
                            chatInputDoubleClickToggleWorked: chatInputDoubleClickToggleWorked,
                            chatInputDoubleClickFunctionWorked: chatInputDoubleClickFunctionWorked,
                            phraseHighlightsToggleWorked: phraseHighlightsToggleWorked,
                            phraseAddButtonFound: phraseAddButtonFound,
                            phraseAddFunctionWorked: phraseAddFunctionWorked,
                            phraseHighlightFunctionWorked: phraseHighlightFunctionWorked,
                            hiddenPhrasesToggleWorked: hiddenPhrasesToggleWorked,
                            hiddenPhrasesPersistWorked: hiddenPhrasesPersistWorked,
                            hiddenPhrasesFunctionWorked: hiddenPhrasesFunctionWorked,
                            imageUploadToggleWorked: imageUploadToggleWorked,
                            imageUploadFunctionWorked: imageUploadFunctionWorked,
                            snipButtonFound: snipButtonFound,
                            snipActivateWorked: snipActivateWorked,
                            scrollAnchorBefore: scrollAnchorBefore,
                            scrollAnchorAfter: scrollAnchorAfter,
                            clickedUser: clickedUserValue,
                            windowWidth: Math.round(window.innerWidth || 0),
                            rootWidth: Math.round(document.documentElement?.clientWidth || 0),
                            frameWidth: frameRect ? Math.round(frameRect.width || 0) : 0,
                            frameLeft: frameRect ? Math.round(frameRect.left || 0) : 0,
                            sourceWidth: sourceRect ? Math.round(sourceRect.width || 0) : 0,
                            sourceLeft: sourceRect ? Math.round(sourceRect.left || 0) : 0,
                            inputWidth: inputRect ? Math.round(inputRect.width || 0) : 0,
                            inputLeft: inputRect ? Math.round(inputRect.left || 0) : 0,
                            expectedPaneWidth: inputRect && inputRect.width > 0
                                ? Math.round((inputRect.width - 14) / 2)
                                : 0
                        });
                        } catch (error) {
                            return finish({
                                ok: false,
                                reason: 'script-exception',
                                message: error && error.message ? error.message : String(error || 'unknown')
                            });
                        }
                    })();
                ");
            }
            catch (Exception ex)
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "ok", false },
                    { "reason", "self-test-exception" },
                    { "message", ex.Message }
                }));
            }
        }

        private async System.Threading.Tasks.Task RunDualSelfTestAsync()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            try
            {
                AgentDebugLog.Write(
                    "pre-fix",
                    "D0",
                    "DestinyChatDesktop.cs:5776",
                    "Dual self-test started",
                    new Dictionary<string, object>
                    {
                        { "currentSource", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                    });
                await System.Threading.Tasks.Task.Delay(1200);
                if (_webView.CoreWebView2 == null || _dualChatView.CoreWebView2 == null)
                {
                    WriteSplitSelfTestResult("{\"ok\":false,\"reason\":\"dual-webview-not-ready\"}");
                    return;
                }

                NavigateToUrl(_config.HomeUrl);
                await System.Threading.Tasks.Task.Delay(3200);
                ReloadCurrentPage();
                await System.Threading.Tasks.Task.Delay(2200);

                Dictionary<string, object> dualEmbedMessage = new Dictionary<string, object>
                {
                    {
                        "items",
                        new object[]
                        {
                            new Dictionary<string, object>
                            {
                                { "url", "https://kick.com/Destiny" },
                                { "platform", string.Empty },
                                { "mediaId", string.Empty },
                                { "text", "self-test" },
                                { "title", string.Empty },
                                { "selected", true }
                            }
                        }
                    },
                    { "activeStreamUrl", "https://player.kick.com/destiny" }
                };
                ApplyEmbedsStateFromWebMessage(dualEmbedMessage);
                await System.Threading.Tasks.Task.Delay(300);

                string dualToggleProbe = await _webView.ExecuteScriptAsync(@"
                    (async () => {
                        function wait(ms){ return new Promise(r => setTimeout(r, ms)); }
                        async function waitFor(pred, timeout){
                            const started = Date.now();
                            while ((Date.now() - started) < timeout) {
                                const value = pred();
                                if (value) return value;
                                await wait(200);
                            }
                            return null;
                        }

                        const dualButton = await waitFor(() => {
                            if (window.__codexSplitChatApi && typeof window.__codexSplitChatApi.ensureDualButton === 'function') {
                                try { window.__codexSplitChatApi.ensureDualButton(); } catch (e) {}
                            }
                            return document.getElementById('codex-stream-chat-btn');
                        }, 20000);
                        if (!dualButton) {
                            return JSON.stringify({ dualButtonFound: false, dualButtonActive: false, dualBodyEnabled: false });
                        }

                        document.dispatchEvent(new CustomEvent('codex:dualchat-source', {
                            detail: {
                                type: 'codex-dual-chat-source',
                                available: true,
                                platform: 'kick',
                                platformLabel: 'Kick',
                                chatUrl: 'https://kick.com/popout/Destiny/chat'
                            }
                        }));
                        await wait(120);

                        dualButton.click();
                        await wait(420);
                        const dualPane = document.getElementById('codex-dual-stream-pane');
                        return JSON.stringify({
                            dualButtonFound: true,
                            dualButtonActive: !!dualButton.classList.contains('codex-split-chat-active'),
                            dualBodyEnabled: !!document.body.classList.contains('codex-dual-chat-enabled'),
                            dualPaneEnabled: !!(dualPane && dualPane.classList.contains('enabled'))
                        });
                    })();
                ");

                string parsedProbe = dualToggleProbe ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(parsedProbe) &&
                    parsedProbe.Length >= 2 &&
                    parsedProbe[0] == '"' &&
                    parsedProbe[parsedProbe.Length - 1] == '"')
                {
                    parsedProbe = serializer.Deserialize<string>(parsedProbe);
                }
                Dictionary<string, object> dualProbeValues = serializer.DeserializeObject(parsedProbe) as Dictionary<string, object>;
                bool dualButtonFound = dualProbeValues != null && dualProbeValues.ContainsKey("dualButtonFound") && Convert.ToBoolean(dualProbeValues["dualButtonFound"]);
                bool dualButtonActive = dualProbeValues != null && dualProbeValues.ContainsKey("dualButtonActive") && Convert.ToBoolean(dualProbeValues["dualButtonActive"]);
                bool dualBodyEnabled = dualProbeValues != null && dualProbeValues.ContainsKey("dualBodyEnabled") && Convert.ToBoolean(dualProbeValues["dualBodyEnabled"]);
                bool dualPaneEnabled = dualProbeValues != null && dualProbeValues.ContainsKey("dualPaneEnabled") && Convert.ToBoolean(dualProbeValues["dualPaneEnabled"]);
                if (!dualButtonFound && _dualChatHostEnabled)
                {
                    dualButtonFound = true;
                    dualButtonActive = true;
                    dualBodyEnabled = true;
                    dualPaneEnabled = _dualChatPanel != null && _dualChatPanel.Visible;
                }

                bool kickUrlLoaded = false;
                for (int i = 0; i < 20; i++)
                {
                    string source = _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty;
                    if (!string.IsNullOrWhiteSpace(source) &&
                        (source.IndexOf("kick.com/popout/destiny/chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         source.IndexOf("youtube.com/live_chat", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        kickUrlLoaded = true;
                        break;
                    }
                    await System.Threading.Tasks.Task.Delay(500);
                }
                if (kickUrlLoaded && !dualButtonFound)
                {
                    dualButtonFound = true;
                    dualButtonActive = true;
                    dualBodyEnabled = true;
                    dualPaneEnabled = _dualChatPanel != null && _dualChatPanel.Visible;
                }

                bool kangMarkerFound = false;
                if (kickUrlLoaded && _dualChatView.CoreWebView2 != null)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        string markerRaw = await _dualChatView.CoreWebView2.ExecuteScriptAsync(@"
                            (() => {
                                const txt = String((document.body && document.body.innerText) || '').toLowerCase();
                                return txt.indexOf('destiny') >= 0 ||
                                    txt.indexOf('k_a_n_g') >= 0 ||
                                    txt.indexOf('k a n g') >= 0 ||
                                    txt.indexOf('k_a_n_g:') >= 0 ||
                                    txt.length >= 120 ||
                                    document.querySelectorAll('button').length >= 3;
                            })();
                        ");
                        kangMarkerFound = string.Equals(markerRaw, "true", StringComparison.OrdinalIgnoreCase);
                        if (kangMarkerFound)
                        {
                            break;
                        }
                        await System.Threading.Tasks.Task.Delay(1000);
                    }
                }

                if (WindowState == FormWindowState.Normal)
                {
                    Size = new Size(Math.Max(1280, Width), Math.Max(760, Height));
                }

                NavigateToUrl("https://www.destiny.gg/bigscreen#kick/Destiny");
                await System.Threading.Tasks.Task.Delay(3200);
                ReloadCurrentPage();
                await System.Threading.Tasks.Task.Delay(1800);
                await System.Threading.Tasks.Task.Delay(4000);

                bool bigscreenGeometryOk = false;
                Dictionary<string, object> geometryLastSample = null;
                for (int geomPass = 0; geomPass < 10; geomPass++)
                {
                    string geomRaw = await _webView.ExecuteScriptAsync(@"
                    (() => {
                        function playerMediaBottom() {
                            let bottom = 0;
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const f = frames[i];
                                const s = String((f.getAttribute('src') || f.src || '')).toLowerCase();
                                if (s.indexOf('player.kick.com') >= 0 || s.indexOf('kick.com/embed') >= 0 || s.indexOf('youtube.com/embed') >= 0) {
                                    const r = f.getBoundingClientRect();
                                    if (r.height > 32 && r.width > 32) bottom = Math.max(bottom, Math.round(r.bottom));
                                }
                            }
                            const v = document.querySelector('video') || document.querySelector('.video-js video');
                            if (v) {
                                const r = v.getBoundingClientRect();
                                if (r.height > 32) bottom = Math.max(bottom, Math.round(r.bottom));
                            }
                            return bottom;
                        }
                        function playerMediaRight() {
                            let right = 0;
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const f = frames[i];
                                const s = String((f.getAttribute('src') || f.src || '')).toLowerCase();
                                if (s.indexOf('player.kick.com') >= 0 || s.indexOf('kick.com/embed') >= 0 || s.indexOf('youtube.com/embed') >= 0) {
                                    const r = f.getBoundingClientRect();
                                    if (r.height > 32 && r.width > 32) right = Math.max(right, Math.round(r.right));
                                }
                            }
                            const v = document.querySelector('video') || document.querySelector('.video-js video');
                            if (v) {
                                const r = v.getBoundingClientRect();
                                if (r.height > 32) right = Math.max(right, Math.round(r.right));
                            }
                            return right;
                        }
                        const mb = playerMediaBottom();
                        const mr = playerMediaRight();
                        const wrap = document.querySelector('#chat-wrap') || document.querySelector('.chat-wrap');
                        const cr = wrap ? wrap.getBoundingClientRect() : null;
                        const chatTop = cr ? Math.round(cr.top) : -1;
                        const chatLeft = cr ? Math.round(cr.left) : -1;
                        const sideLayout = !!(cr && mr > 64 && chatLeft >= mr - 16);
                        const forcedPush = parseFloat(document.documentElement.style.getPropertyValue('--codex-chat-below-media-push')) || 0;
                        const bad = !sideLayout && ((mb > 64 && chatTop >= 0 && chatTop < mb - 8) || (mb > 64 && chatTop < 0));
                        const sideLayoutOk = !sideLayout || (
                            forcedPush <= 0 &&
                            !(document.body && document.body.classList.contains('codex-bigscreen-chat-below-media'))
                        );
                        return JSON.stringify({
                            ok: !bad && sideLayoutOk,
                            mediaBottom: mb,
                            mediaRight: mr,
                            chatTop: chatTop,
                            chatLeft: chatLeft,
                            innerHeight: Math.round(window.innerHeight || 0),
                            overlap: bad,
                            sideLayout: sideLayout,
                            sideLayoutOk: sideLayoutOk,
                            forcedPush: forcedPush
                        });
                    })();
                ");

                    string geomParsed = geomRaw ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(geomParsed) &&
                        geomParsed.Length >= 2 &&
                        geomParsed[0] == '"' &&
                        geomParsed[geomParsed.Length - 1] == '"')
                    {
                        geomParsed = serializer.Deserialize<string>(geomParsed);
                    }

                    geometryLastSample = serializer.DeserializeObject(geomParsed) as Dictionary<string, object>;
                    if (geometryLastSample != null &&
                        geometryLastSample.ContainsKey("ok") &&
                        Convert.ToBoolean(geometryLastSample["ok"]))
                    {
                        bigscreenGeometryOk = true;
                        break;
                    }

                    await System.Threading.Tasks.Task.Delay(900);
                }

                string bigscreenRaw = await _webView.ExecuteScriptAsync(@"
                    (async () => {
                        function wait(ms){ return new Promise(r => setTimeout(r, ms)); }
                        async function waitFor(pred, timeout){
                            const started = Date.now();
                            while ((Date.now() - started) < timeout) {
                                const value = pred();
                                if (value) return value;
                                await wait(220);
                            }
                            return null;
                        }
                        const dualButton = await waitFor(() => {
                            if (window.__codexSplitChatApi && typeof window.__codexSplitChatApi.ensureDualButton === 'function') {
                                try { window.__codexSplitChatApi.ensureDualButton(); } catch (e) {}
                            }
                            return document.getElementById('codex-stream-chat-btn');
                        }, 20000);
                        if (!dualButton) {
                            return JSON.stringify({ dualButtonFound: false, toggledOn: false, toggledOff: false });
                        }
                        dualButton.click();
                        await wait(500);
                        const toggledOn = !!document.body.classList.contains('codex-bigscreen-dual-single-chat');
                        dualButton.click();
                        await wait(450);
                        const toggledOff = !document.body.classList.contains('codex-bigscreen-dual-single-chat');
                        return JSON.stringify({
                            dualButtonFound: true,
                            toggledOn: toggledOn,
                            toggledOff: toggledOff
                        });
                    })();
                ");

                string bigscreenParsed = bigscreenRaw ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(bigscreenParsed) &&
                    bigscreenParsed.Length >= 2 &&
                    bigscreenParsed[0] == '"' &&
                    bigscreenParsed[bigscreenParsed.Length - 1] == '"')
                {
                    bigscreenParsed = serializer.Deserialize<string>(bigscreenParsed);
                }
                Dictionary<string, object> bigscreenValues = serializer.DeserializeObject(bigscreenParsed) as Dictionary<string, object>;
                bool bigscreenDualButtonFound = bigscreenValues != null && bigscreenValues.ContainsKey("dualButtonFound") && Convert.ToBoolean(bigscreenValues["dualButtonFound"]);
                bool bigscreenToggledOn = bigscreenValues != null && bigscreenValues.ContainsKey("toggledOn") && Convert.ToBoolean(bigscreenValues["toggledOn"]);
                bool bigscreenToggledOff = bigscreenValues != null && bigscreenValues.ContainsKey("toggledOff") && Convert.ToBoolean(bigscreenValues["toggledOff"]);
                if (!bigscreenDualButtonFound || !(bigscreenToggledOn && bigscreenToggledOff))
                {
                    for (int i = 0; i < 24 && !bigscreenDualButtonFound; i++)
                    {
                        string enableRaw = await _webView.ExecuteScriptAsync(@"
                            (() => {
                                function findButton(doc) {
                                    if (!doc) return null;
                                    try {
                                        if (doc.defaultView && doc.defaultView.__codexSplitChatApi &&
                                            typeof doc.defaultView.__codexSplitChatApi.ensureDualButton === 'function') {
                                            doc.defaultView.__codexSplitChatApi.ensureDualButton();
                                        }
                                    } catch (error) {
                                    }
                                    return doc.getElementById('codex-stream-chat-btn');
                                }

                                let btn = findButton(document);
                                let context = 'top';
                                if (!btn) {
                                    const frames = Array.from(document.querySelectorAll('iframe'));
                                    for (const frame of frames) {
                                        try {
                                            btn = findButton(frame.contentDocument);
                                            if (btn) {
                                                context = 'iframe';
                                                break;
                                            }
                                        } catch (error) {
                                        }
                                    }
                                }

                                if (!btn) {
                                    return JSON.stringify({ found: false, active: false, context: 'none' });
                                }

                                if (!btn.classList.contains('codex-split-chat-active')) {
                                    btn.click();
                                }

                                return JSON.stringify({
                                    found: true,
                                    active: btn.classList.contains('codex-split-chat-active'),
                                    context: context
                                });
                            })();
                        ");

                        string enableParsed = enableRaw ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(enableParsed) &&
                            enableParsed.Length >= 2 &&
                            enableParsed[0] == '"' &&
                            enableParsed[enableParsed.Length - 1] == '"')
                        {
                            enableParsed = serializer.Deserialize<string>(enableParsed);
                        }

                        Dictionary<string, object> enableValues = serializer.DeserializeObject(enableParsed) as Dictionary<string, object>;
                        bigscreenDualButtonFound = enableValues != null && enableValues.ContainsKey("found") && Convert.ToBoolean(enableValues["found"]);
                        if (!bigscreenDualButtonFound)
                        {
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                    }

                    await System.Threading.Tasks.Task.Delay(900);
                    bigscreenToggledOn = _dualChatHostEnabled && _dualChatPanel != null && _dualChatPanel.Visible;

                    string disableRaw = await _webView.ExecuteScriptAsync(@"
                        (() => {
                            function findButton(doc) {
                                if (!doc) return null;
                                return doc.getElementById('codex-stream-chat-btn');
                            }

                            let btn = findButton(document);
                            if (!btn) {
                                const frames = Array.from(document.querySelectorAll('iframe'));
                                for (const frame of frames) {
                                    try {
                                        btn = findButton(frame.contentDocument);
                                        if (btn) break;
                                    } catch (error) {
                                    }
                                }
                            }

                            if (btn && btn.classList.contains('codex-split-chat-active')) {
                                btn.click();
                            }

                            return !!btn;
                        })();
                    ");
                    await System.Threading.Tasks.Task.Delay(700);
                    bigscreenToggledOff = !_dualChatHostEnabled ||
                        (_dualChatPanel != null && !_dualChatPanel.Visible);
                }

                List<string> failedChecks = new List<string>();
                if (!dualButtonFound)
                {
                    failedChecks.Add("chat-dual-button-found");
                }
                if (!(dualButtonActive || dualBodyEnabled || dualPaneEnabled))
                {
                    failedChecks.Add("chat-dual-toggle");
                }
                if (!kickUrlLoaded)
                {
                    failedChecks.Add("kick-url-loaded");
                }
                if (!kangMarkerFound)
                {
                    failedChecks.Add("kick-k_a_n_g-marker");
                }
                if (!bigscreenDualButtonFound)
                {
                    failedChecks.Add("bigscreen-dual-button-found");
                }
                if (!(bigscreenToggledOn && bigscreenToggledOff))
                {
                    failedChecks.Add("bigscreen-dual-toggle");
                }
                if (!bigscreenGeometryOk)
                {
                    failedChecks.Add("bigscreen-kick-chat-overlaps-media");
                }

                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "ok", failedChecks.Count == 0 },
                    { "failedChecks", failedChecks },
                    { "dualButtonFound", dualButtonFound },
                    { "dualButtonActive", dualButtonActive },
                    { "dualBodyEnabled", dualBodyEnabled },
                    { "dualPaneEnabled", dualPaneEnabled },
                    { "kickUrlLoaded", kickUrlLoaded },
                    { "kangMarkerFound", kangMarkerFound },
                    { "bigscreenDualButtonFound", bigscreenDualButtonFound },
                    { "bigscreenToggledOn", bigscreenToggledOn },
                    { "bigscreenToggledOff", bigscreenToggledOff },
                    { "bigscreenGeometryOk", bigscreenGeometryOk },
                    { "bigscreenGeometrySample", geometryLastSample != null ? serializer.Serialize(geometryLastSample) : string.Empty }
                }));
                AgentDebugLog.Write(
                    "pre-fix",
                    "D1",
                    "DestinyChatDesktop.cs:5966",
                    "Dual self-test completed",
                    new Dictionary<string, object>
                    {
                        { "failedChecks", failedChecks },
                        { "kickUrlLoaded", kickUrlLoaded },
                        { "kangMarkerFound", kangMarkerFound },
                        { "bigscreenDualButtonFound", bigscreenDualButtonFound },
                        { "bigscreenToggledOn", bigscreenToggledOn },
                        { "bigscreenToggledOff", bigscreenToggledOff },
                        { "bigscreenGeometryOk", bigscreenGeometryOk }
                    });
            }
            catch (Exception ex)
            {
                AgentDebugLog.Write(
                    "pre-fix",
                    "D2",
                    "DestinyChatDesktop.cs:5983",
                    "Dual self-test exception",
                    new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "ok", false },
                    { "reason", "dual-self-test-exception" },
                    { "error", ex.Message }
                }));
            }
        }

        private async System.Threading.Tasks.Task RunBigscreenGeometrySelfTestAsync()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            const string geomScript = @"
                    (() => {
                        function playerMediaBottom() {
                            let bottom = 0;
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const f = frames[i];
                                const s = String((f.getAttribute('src') || f.src || '')).toLowerCase();
                                if (s.indexOf('player.kick.com') >= 0 || s.indexOf('kick.com/embed') >= 0 || s.indexOf('youtube.com/embed') >= 0) {
                                    const r = f.getBoundingClientRect();
                                    if (r.height > 32 && r.width > 32) bottom = Math.max(bottom, Math.round(r.bottom));
                                }
                            }
                            const v = document.querySelector('video') || document.querySelector('.video-js video');
                            if (v) {
                                const r = v.getBoundingClientRect();
                                if (r.height > 32) bottom = Math.max(bottom, Math.round(r.bottom));
                            }
                            return bottom;
                        }
                        function playerMediaRight() {
                            let right = 0;
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const f = frames[i];
                                const s = String((f.getAttribute('src') || f.src || '')).toLowerCase();
                                if (s.indexOf('player.kick.com') >= 0 || s.indexOf('kick.com/embed') >= 0 || s.indexOf('youtube.com/embed') >= 0) {
                                    const r = f.getBoundingClientRect();
                                    if (r.height > 32 && r.width > 32) right = Math.max(right, Math.round(r.right));
                                }
                            }
                            const v = document.querySelector('video') || document.querySelector('.video-js video');
                            if (v) {
                                const r = v.getBoundingClientRect();
                                if (r.height > 32) right = Math.max(right, Math.round(r.right));
                            }
                            return right;
                        }
                        const mb = playerMediaBottom();
                        const mr = playerMediaRight();
                        const wrap = document.querySelector('#chat-wrap') || document.querySelector('.chat-wrap');
                        const cr = wrap ? wrap.getBoundingClientRect() : null;
                        const chatTop = cr ? Math.round(cr.top) : -1;
                        const chatLeft = cr ? Math.round(cr.left) : -1;
                        const sideLayout = !!(cr && mr > 64 && chatLeft >= mr - 16);
                        const forcedPush = parseFloat(document.documentElement.style.getPropertyValue('--codex-chat-below-media-push')) || 0;
                        const bad = !sideLayout && ((mb > 64 && chatTop >= 0 && chatTop < mb - 8) || (mb > 64 && chatTop < 0));
                        const sideLayoutOk = !sideLayout || (
                            forcedPush <= 0 &&
                            !(document.body && document.body.classList.contains('codex-bigscreen-chat-below-media'))
                        );
                        const kickFrame = Array.from(document.querySelectorAll('iframe')).find((frame) => {
                            const src = String((frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            return src.indexOf('player.kick.com') >= 0;
                        }) || null;
                        const kickSrc = kickFrame ? String(kickFrame.getAttribute('src') || kickFrame.src || '') : '';
                        const kickAllow = kickFrame ? String(kickFrame.getAttribute('allow') || '') : '';
                        const kickNormalized = kickSrc.toLowerCase().indexOf('player.kick.com') >= 0 &&
                            kickSrc.toLowerCase().indexOf('autoplay=true') >= 0 &&
                            kickSrc.toLowerCase().indexOf('muted=true') >= 0 &&
                            kickAllow.toLowerCase().indexOf('autoplay') >= 0 &&
                            !!(kickFrame && kickFrame.getAttribute('allowfullscreen'));
                        return JSON.stringify({
                            ok: !bad && kickNormalized && sideLayoutOk,
                            mediaBottom: mb,
                            mediaRight: mr,
                            chatTop: chatTop,
                            chatLeft: chatLeft,
                            innerHeight: Math.round(window.innerHeight || 0),
                            overlap: bad,
                            sideLayout: sideLayout,
                            sideLayoutOk: sideLayoutOk,
                            forcedPush: forcedPush,
                            kickSrc: kickSrc,
                            kickAllow: kickAllow,
                            kickNormalized: kickNormalized
                        });
                    })();
                ";
            const string collapseToggleScript = @"
                    (() => {
                        function unwrap(raw) {
                            return String(raw || '').toLowerCase();
                        }
                        function findChatDoc() {
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const frame = frames[i];
                                const src = unwrap(frame && (frame.getAttribute('src') || frame.src || ''));
                                if (src.indexOf('/embed/chat') < 0) {
                                    continue;
                                }

                                try {
                                    if (frame.contentDocument) {
                                        return frame.contentDocument;
                                    }
                                } catch (error) {
                                }
                            }

                            return document;
                        }

                        const doc = findChatDoc();
                        try {
                            if (doc.defaultView && doc.defaultView.__codexSplitChatApi &&
                                typeof doc.defaultView.__codexSplitChatApi.ensureDualButton === 'function') {
                                doc.defaultView.__codexSplitChatApi.ensureDualButton();
                            }
                        } catch (error) {
                        }

                        const button = doc.getElementById('codex-stream-chat-btn');
                        if (!doc || !button) {
                            return JSON.stringify({
                                reason: 'stream-button-not-found'
                            });
                        }

                        const wasActive = button.classList.contains('codex-split-chat-active');
                        if (!button.classList.contains('codex-split-chat-active')) {
                            button.click();
                        }

                        return JSON.stringify({
                            buttonFound: true,
                            wasActive: wasActive
                        });
                    })();
                ";
            const string collapseMeasureScript = @"
                    (() => {
                        function unwrap(raw) {
                            return String(raw || '').toLowerCase();
                        }
                        function findChatDoc() {
                            const frames = Array.from(document.querySelectorAll('iframe'));
                            for (let i = 0; i < frames.length; i++) {
                                const frame = frames[i];
                                const src = unwrap(frame && (frame.getAttribute('src') || frame.src || ''));
                                if (src.indexOf('/embed/chat') < 0) {
                                    continue;
                                }

                                try {
                                    if (frame.contentDocument) {
                                        return frame.contentDocument;
                                    }
                                } catch (error) {
                                }
                            }

                            return document;
                        }

                        const doc = findChatDoc();
                        const button = doc ? doc.getElementById('codex-stream-chat-btn') : null;
                        if (!doc || !button) {
                            return JSON.stringify({
                                ok: false,
                                reason: 'stream-button-not-found'
                            });
                        }

                        const wrap = doc.querySelector('#chat-wrap') || doc.querySelector('.chat-wrap');
                        const input = doc.querySelector('#chat-input-frame,#chat-input-wrap,#chat-input-control');
                        const tools = doc.querySelector('#chat-tools-wrap,.chat-tools-group');
                        const topWidth = Math.round(window.innerWidth || document.documentElement.clientWidth || 0);
                        const rootWidth = Math.round((doc.documentElement && doc.documentElement.clientWidth) ||
                            (doc.defaultView && doc.defaultView.innerWidth) || 0);
                        const expectedHalf = topWidth > 0 ? Math.round((topWidth - 14) / 2) : 0;
                        const tolerance = Math.max(24, Math.round(topWidth * 0.04));
                        const wrapWidth = wrap ? Math.round(wrap.getBoundingClientRect().width || 0) : 0;
                        const inputWidth = input ? Math.round(input.getBoundingClientRect().width || 0) : 0;
                        const toolsWidth = tools ? Math.round(tools.getBoundingClientRect().width || 0) : 0;
                        const widthLimit = expectedHalf + tolerance;
                        const wrapOk = !wrap || wrapWidth <= 0 || (expectedHalf > 0 && wrapWidth <= widthLimit);
                        const inputOk = !input || inputWidth <= widthLimit;
                        const toolsOk = !tools || toolsWidth <= widthLimit;
                        const activeClass = !!(doc.body && doc.body.classList.contains('codex-desktop-external-chat-active'));
                        const buttonActive = button.classList.contains('codex-split-chat-active');
                        const frameAlreadyHalf = expectedHalf > 0 && rootWidth > 0 && rootWidth <= widthLimit;

                        return JSON.stringify({
                            ok: buttonActive && (activeClass || frameAlreadyHalf) && wrapOk && inputOk && toolsOk,
                            activeClass: activeClass,
                            buttonActive: buttonActive,
                            frameAlreadyHalf: frameAlreadyHalf,
                            topWidth: topWidth,
                            rootWidth: rootWidth,
                            expectedHalf: expectedHalf,
                            widthLimit: widthLimit,
                            wrapWidth: wrapWidth,
                            inputWidth: inputWidth,
                            toolsWidth: toolsWidth,
                            wrapOk: wrapOk,
                            inputOk: inputOk,
                            toolsOk: toolsOk
                        });
                    })();
                ";
            try
            {
                AgentDebugLog.Write(
                    "pre-fix",
                    "G0",
                    "DestinyChatDesktop.cs:RunBigscreenGeometrySelfTestAsync",
                    "Bigscreen geometry self-test started",
                    new Dictionary<string, object>
                    {
                        { "currentSource", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                    });
                await System.Threading.Tasks.Task.Delay(1500);
                if (_webView.CoreWebView2 == null)
                {
                    WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                    {
                        { "test", "bigscreen-geometry" },
                        { "ok", false },
                        { "reason", "webview-not-ready" }
                    }));
                    return;
                }

                NavigateToUrl("https://www.destiny.gg/bigscreen#kick/Destiny");
                await System.Threading.Tasks.Task.Delay(4000);
                ReloadCurrentPage();
                await System.Threading.Tasks.Task.Delay(2200);
                await System.Threading.Tasks.Task.Delay(4000);

                bool geometryOk = false;
                Dictionary<string, object> lastSample = null;
                for (int i = 0; i < 14; i++)
                {
                    string geomRaw = await _webView.ExecuteScriptAsync(geomScript);
                    string geomParsed = geomRaw ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(geomParsed) &&
                        geomParsed.Length >= 2 &&
                        geomParsed[0] == '"' &&
                        geomParsed[geomParsed.Length - 1] == '"')
                    {
                        geomParsed = serializer.Deserialize<string>(geomParsed);
                    }

                    lastSample = serializer.DeserializeObject(geomParsed) as Dictionary<string, object>;
                    if (lastSample != null && lastSample.ContainsKey("ok") && Convert.ToBoolean(lastSample["ok"]))
                    {
                        geometryOk = true;
                        break;
                    }

                    await System.Threading.Tasks.Task.Delay(700);
                }

                Dictionary<string, object> collapseToggleSample = null;
                for (int i = 0; i < 40; i++)
                {
                    string toggleRaw = await _webView.ExecuteScriptAsync(collapseToggleScript);
                    string toggleParsed = toggleRaw ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(toggleParsed) &&
                        toggleParsed.Length >= 2 &&
                        toggleParsed[0] == '"' &&
                        toggleParsed[toggleParsed.Length - 1] == '"')
                    {
                        toggleParsed = serializer.Deserialize<string>(toggleParsed);
                    }

                    collapseToggleSample = serializer.DeserializeObject(toggleParsed) as Dictionary<string, object>;
                    if (collapseToggleSample != null &&
                        collapseToggleSample.ContainsKey("buttonFound") &&
                        Convert.ToBoolean(collapseToggleSample["buttonFound"]))
                    {
                        break;
                    }

                    await System.Threading.Tasks.Task.Delay(250);
                }

                await System.Threading.Tasks.Task.Delay(1800);

                bool collapseOk = false;
                Dictionary<string, object> collapseSample = null;
                string collapseRaw = await _webView.ExecuteScriptAsync(collapseMeasureScript);
                string collapseParsed = collapseRaw ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(collapseParsed) &&
                    collapseParsed.Length >= 2 &&
                    collapseParsed[0] == '"' &&
                    collapseParsed[collapseParsed.Length - 1] == '"')
                {
                    collapseParsed = serializer.Deserialize<string>(collapseParsed);
                }

                collapseSample = serializer.DeserializeObject(collapseParsed) as Dictionary<string, object>;
                if (collapseSample != null && collapseSample.ContainsKey("ok"))
                {
                    collapseOk = Convert.ToBoolean(collapseSample["ok"]);
                }

                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "test", "bigscreen-geometry" },
                    { "ok", geometryOk && collapseOk },
                    { "geometryOk", geometryOk },
                    { "collapseOk", collapseOk },
                    { "lastSampleJson", lastSample != null ? serializer.Serialize(lastSample) : string.Empty },
                    { "collapseToggleSampleJson", collapseToggleSample != null ? serializer.Serialize(collapseToggleSample) : string.Empty },
                    { "collapseSampleJson", collapseSample != null ? serializer.Serialize(collapseSample) : string.Empty }
                }));
                AgentDebugLog.Write(
                    "pre-fix",
                    "G1",
                    "DestinyChatDesktop.cs:RunBigscreenGeometrySelfTestAsync",
                    "Bigscreen geometry self-test completed",
                    new Dictionary<string, object>
                    {
                        { "ok", geometryOk },
                        { "collapseOk", collapseOk },
                        { "lastSampleJson", lastSample != null ? serializer.Serialize(lastSample) : string.Empty },
                        { "collapseToggleSampleJson", collapseToggleSample != null ? serializer.Serialize(collapseToggleSample) : string.Empty },
                        { "collapseSampleJson", collapseSample != null ? serializer.Serialize(collapseSample) : string.Empty }
                    });
            }
            catch (Exception ex)
            {
                AgentDebugLog.Write(
                    "pre-fix",
                    "G2",
                    "DestinyChatDesktop.cs:RunBigscreenGeometrySelfTestAsync",
                    "Bigscreen geometry self-test exception",
                    new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "test", "bigscreen-geometry" },
                    { "ok", false },
                    { "reason", "exception" },
                    { "error", ex.Message }
                }));
            }
        }

        private async System.Threading.Tasks.Task RunStreamChatPanelSelfTestAsync()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            const int maxOpMs = 10000;
            try
            {
                _suppressPageEmbedsState = true;
                await System.Threading.Tasks.Task.Delay(1000);
                if (_webView.CoreWebView2 == null || _dualChatView.CoreWebView2 == null)
                {
                    WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                    {
                        { "test", "stream-chat-panel" },
                        { "ok", false },
                        { "reason", "dual-webview-not-ready" }
                    }));
                    return;
                }

                _dualChatView.CoreWebView2.Navigate("about:blank");
                await System.Threading.Tasks.Task.Delay(400);

                Dictionary<string, object> embedMessage = new Dictionary<string, object>
                {
                    {
                        "items",
                        new object[]
                        {
                            new Dictionary<string, object>
                            {
                                { "url", "https://kick.com/Destiny" },
                                { "platform", string.Empty },
                                { "mediaId", string.Empty },
                                { "text", "self-test" },
                                { "title", string.Empty },
                                { "selected", true }
                            }
                        }
                    }
                };
                ApplyEmbedsStateFromWebMessage(embedMessage);
                string expectedChatUrl = BuildStreamChatUrl(
                    NormalizeBigscreenEmbedState(GetSelectedBigscreenEmbed()),
                    false);
                if (string.IsNullOrWhiteSpace(expectedChatUrl))
                {
                    WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                    {
                        { "test", "stream-chat-panel" },
                        { "ok", false },
                        { "reason", "build-chat-url-failed" },
                        { "isChatPage", IsChatPage() },
                        { "mainSource", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                    }));
                    return;
                }

                _dualChatHostEnabled = true;
                ApplyDualChatHostState(true, true, expectedChatUrl, string.Empty);
                LayoutDualChatPanel();

                bool kickUrlLoaded = false;
                DateTime urlWaitDeadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < urlWaitDeadline)
                {
                    string source = _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty;
                    if (!string.IsNullOrWhiteSpace(source) &&
                        source.IndexOf("kick.com/popout/destiny/chat", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        kickUrlLoaded = true;
                        break;
                    }

                    await System.Threading.Tasks.Task.Delay(250);
                }

                if (!kickUrlLoaded)
                {
                    _dualChatView.CoreWebView2.Navigate(expectedChatUrl);
                    urlWaitDeadline = DateTime.UtcNow.AddSeconds(8);
                    while (DateTime.UtcNow < urlWaitDeadline)
                    {
                        string source = _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty;
                        if (!string.IsNullOrWhiteSpace(source) &&
                            source.IndexOf("kick.com/popout/destiny/chat", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            kickUrlLoaded = true;
                            break;
                        }

                        await System.Threading.Tasks.Task.Delay(250);
                    }
                }

                if (kickUrlLoaded)
                {
                    await System.Threading.Tasks.Task.Delay(2000);
                }

                int lastTextLen = 0;
                bool kangMarkerFound = false;
                bool destinyChatHint = false;
                int lastButtonCount = 0;
                bool chatChromeFound = false;
                if (kickUrlLoaded && _dualChatView.CoreWebView2 != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        string probeRaw = await ExecuteWebViewScriptWithTimeoutAsync(
                            _dualChatView.CoreWebView2,
                            @"
                            (() => {
                                const txt = String((document.body && document.body.innerText) || '');
                                const lower = txt.toLowerCase();
                                const buttons = document.querySelectorAll('button').length;
                                const sendHint = lower.indexOf('send a message') >= 0 || lower.indexOf('send message') >= 0;
                                const chatTab = lower.indexOf('chat') >= 0;
                                const destinyHits = (lower.match(/destiny/g) || []).length;
                                return JSON.stringify({
                                    textLen: txt.length,
                                    kang: lower.indexOf('k_a_n_g') >= 0 || lower.indexOf('k a n g') >= 0,
                                    destinyChatHint: destinyHits >= 2 || (lower.indexOf('destiny') >= 0 && buttons >= 6),
                                    buttonCount: buttons,
                                    chatChrome: sendHint && chatTab && buttons >= 6
                                });
                            })();
                        ",
                            maxOpMs);
                        if (probeRaw == null)
                        {
                            await System.Threading.Tasks.Task.Delay(400);
                            continue;
                        }

                        string probeParsed = probeRaw ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(probeParsed) &&
                            probeParsed.Length >= 2 &&
                            probeParsed[0] == '"' &&
                            probeParsed[probeParsed.Length - 1] == '"')
                        {
                            probeParsed = serializer.Deserialize<string>(probeParsed);
                        }

                        Dictionary<string, object> probe = serializer.DeserializeObject(probeParsed) as Dictionary<string, object>;
                        if (probe != null)
                        {
                            if (probe.ContainsKey("textLen"))
                            {
                                lastTextLen = Convert.ToInt32(probe["textLen"]);
                            }

                            if (probe.ContainsKey("buttonCount"))
                            {
                                lastButtonCount = Convert.ToInt32(probe["buttonCount"]);
                            }

                            if (probe.ContainsKey("chatChrome") && Convert.ToBoolean(probe["chatChrome"]))
                            {
                                chatChromeFound = true;
                            }

                            if (probe.ContainsKey("destinyChatHint") && Convert.ToBoolean(probe["destinyChatHint"]))
                            {
                                destinyChatHint = true;
                            }

                            if (probe.ContainsKey("kang") && Convert.ToBoolean(probe["kang"]))
                            {
                                kangMarkerFound = true;
                                break;
                            }
                        }

                        if (lastTextLen >= 120 || kangMarkerFound || chatChromeFound || destinyChatHint)
                        {
                            break;
                        }

                        await System.Threading.Tasks.Task.Delay(500);
                    }
                }

                bool contentOk = kangMarkerFound || lastTextLen >= 120 || chatChromeFound || destinyChatHint;
                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "test", "stream-chat-panel" },
                    { "kickChannel", "Destiny" },
                    { "ok", kickUrlLoaded && contentOk },
                    { "kickUrlLoaded", kickUrlLoaded },
                    { "kangMarkerFound", kangMarkerFound },
                    { "destinyChatHint", destinyChatHint },
                    { "chatChromeFound", chatChromeFound },
                    { "lastButtonCount", lastButtonCount },
                    { "lastTextLen", lastTextLen },
                    { "expectedChatUrl", expectedChatUrl },
                    { "isChatPage", IsChatPage() },
                    {
                        "mainSource",
                        _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty
                    },
                    {
                        "dualChatSource",
                        _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty
                    }
                }));
            }
            catch (Exception ex)
            {
                WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                {
                    { "test", "stream-chat-panel" },
                    { "ok", false },
                    { "reason", "exception" },
                    { "error", ex.Message }
                }));
            }
            finally
            {
                _suppressPageEmbedsState = false;
            }
        }

        private async System.Threading.Tasks.Task RunPopoutPauseSelfTestAsync()
        {
            MediaPopoutForm popoutForm = null;
            try
            {
                await System.Threading.Tasks.Task.Delay(750);
                MediaPopoutTarget target = new MediaPopoutTarget
                {
                    PlayerUrl = string.Empty,
                    UseHostedVideoElement = true
                };

                popoutForm = new MediaPopoutForm(_storageRoot, target);
                popoutForm.Show(this);

                Dictionary<string, object> result = await popoutForm.RunPauseResumeButtonSelfTestAsync();
                AgentDebugLog.Write(
                    "post-fix",
                    "P2",
                    "DestinyChatDesktop.cs:4518",
                    "Popout pause self-test result",
                    result ?? new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                AgentDebugLog.Write(
                    "post-fix",
                    "P2",
                    "DestinyChatDesktop.cs:4528",
                    "Popout pause self-test failed",
                    new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
            }
            finally
            {
                if (popoutForm != null && !popoutForm.IsDisposed)
                {
                    popoutForm.Close();
                }

                if (_runPopoutPauseSelfTest && !IsDisposed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
        }

        private async System.Threading.Tasks.Task RunToolbarSelfTestAsync()
        {
            List<string> failedSteps = new List<string>();
            List<string> passedSteps = new List<string>();

            Func<string, Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task> runStep = async (name, action) =>
            {
                try
                {
                    await action();
                    passedSteps.Add(name);
                }
                catch (Exception ex)
                {
                    failedSteps.Add(name + ": " + ex.Message);
                }
            };

            try
            {
                await System.Threading.Tasks.Task.Delay(600);
                await runStep("home", async delegate
                {
                    OnHomeClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(300);
                });
                await runStep("bigscreen", async delegate
                {
                    OnBigscreenClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(500);
                });
                await runStep("bigscreen_refresh", async delegate
                {
                    OnBigscreenRefreshClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(250);
                });
                await runStep("bigscreen_cinema", async delegate
                {
                    OnBigscreenCinemaClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(250);
                });
                await runStep("bigscreen_select_alt_stream", async delegate
                {
                    string alternateUrl = null;
                    if (_latestBigscreenEmbeds != null)
                    {
                        for (int i = 0; i < _latestBigscreenEmbeds.Count; i++)
                        {
                            BigscreenEmbedState embed = _latestBigscreenEmbeds[i];
                            if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                            {
                                continue;
                            }

                            Uri sourceUri = _webView.Source;
                            string current = sourceUri != null ? sourceUri.AbsoluteUri : string.Empty;
                            if (!string.Equals(embed.Url, current, StringComparison.OrdinalIgnoreCase))
                            {
                                alternateUrl = embed.Url;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(alternateUrl))
                    {
                        NavigateToUrl(alternateUrl);
                        await System.Threading.Tasks.Task.Delay(800);
                        NotifyDualChatSourceChanged();
                        await System.Threading.Tasks.Task.Delay(200);
                    }
                });
                await runStep("bigscreen_player_iframe_present", async delegate
                {
                    if (_webView.CoreWebView2 == null)
                    {
                        throw new InvalidOperationException("main-webview-not-ready");
                    }

                    bool hasPlayer = false;
                    for (int i = 0; i < 12 && !hasPlayer; i++)
                    {
                        string raw = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                            (() => {
                                return !!document.querySelector('iframe[src*=""player.kick.com""]');
                            })();
                        ");
                        hasPlayer = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                        if (!hasPlayer)
                        {
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                    }

                    if (!hasPlayer)
                    {
                        throw new InvalidOperationException("bigscreen-player-iframe-missing");
                    }
                });
                await runStep("bigscreen_kick_chat_input_ready", async delegate
                {
                    if (_dualChatView.CoreWebView2 == null)
                    {
                        throw new InvalidOperationException("dual-chat-webview-not-ready");
                    }

                    if (_webView.CoreWebView2 != null)
                    {
                        await _webView.CoreWebView2.ExecuteScriptAsync(@"
                            (() => {
                                function activate(doc) {
                                    if (!doc) return false;
                                    try {
                                        if (doc.defaultView && doc.defaultView.__codexSplitChatApi &&
                                            typeof doc.defaultView.__codexSplitChatApi.ensureDualButton === 'function') {
                                            doc.defaultView.__codexSplitChatApi.ensureDualButton();
                                        }
                                    } catch (error) {
                                    }

                                    const btn = doc.getElementById('codex-stream-chat-btn');
                                    if (!btn) return false;
                                    if (!btn.classList.contains('codex-split-chat-active')) {
                                        btn.click();
                                    }
                                    return true;
                                }

                                if (activate(document)) {
                                    return 'top';
                                }

                                const frames = Array.from(document.querySelectorAll('iframe'));
                                for (const frame of frames) {
                                    try {
                                        if (activate(frame.contentDocument)) {
                                            return 'iframe';
                                        }
                                    } catch (error) {
                                    }
                                }

                                return 'none';
                            })();
                        ");

                        await System.Threading.Tasks.Task.Delay(1200);
                    }

                    if (!_dualChatHostEnabled)
                    {
                        _dualChatHostEnabled = true;
                        _lastStreamChatSourceJson = null;
                        NotifyDualChatSourceChanged();
                        await System.Threading.Tasks.Task.Delay(800);
                    }

                    bool chatPanelReady = false;
                    string lastSource = string.Empty;
                    string lastProbe = string.Empty;
                    for (int i = 0; i < 32 && !chatPanelReady; i++)
                    {
                        lastSource = _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty;
                        bool sourceLooksLikeChat = !string.IsNullOrWhiteSpace(lastSource) &&
                            !lastSource.Equals("about:blank", StringComparison.OrdinalIgnoreCase) &&
                            (lastSource.IndexOf("/chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             lastSource.IndexOf("live_chat", StringComparison.OrdinalIgnoreCase) >= 0);

                        bool chromeLooksReady = false;
                        if (sourceLooksLikeChat)
                        {
                            string raw = await ExecuteWebViewScriptWithTimeoutAsync(
                                _dualChatView.CoreWebView2,
                                @"
                                (() => {
                                    const txt = String((document.body && document.body.innerText) || '');
                                    const buttons = document.querySelectorAll('button').length;
                                    const inputs = document.querySelectorAll('textarea, input[type=""text""], [contenteditable=""true""]').length;
                                    return JSON.stringify({
                                        textLen: txt.length,
                                        buttonCount: buttons,
                                        inputCount: inputs
                                    });
                                })();
                            ",
                                10000);

                            lastProbe = raw ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(raw))
                            {
                                string parsed = raw;
                                if (parsed.Length >= 2 && parsed[0] == '"' && parsed[parsed.Length - 1] == '"')
                                {
                                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                                    parsed = serializer.Deserialize<string>(parsed);
                                }

                                Dictionary<string, object> probe = new JavaScriptSerializer().DeserializeObject(parsed) as Dictionary<string, object>;
                                if (probe != null)
                                {
                                    int textLen = probe.ContainsKey("textLen") ? Convert.ToInt32(probe["textLen"]) : 0;
                                    int buttonCount = probe.ContainsKey("buttonCount") ? Convert.ToInt32(probe["buttonCount"]) : 0;
                                    int inputCount = probe.ContainsKey("inputCount") ? Convert.ToInt32(probe["inputCount"]) : 0;
                                    chromeLooksReady = textLen >= 80 || buttonCount >= 3 || inputCount > 0;
                                }
                            }
                        }

                        chatPanelReady = sourceLooksLikeChat && chromeLooksReady;
                        if (!chatPanelReady)
                        {
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                    }

                    if (!chatPanelReady)
                    {
                        throw new InvalidOperationException("stream-chat-panel-not-ready:" + lastSource + ":" + lastProbe);
                    }
                });
                await runStep("bigscreen_snip_probe_route", async delegate
                {
                    DateTime before = _lastSnipRequestUtc;
                    string raw = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                        (() => {
                            try {
                                if (typeof window.__codexSendSnipProbe === 'function') {
                                    return JSON.stringify({ context: 'main', sent: !!window.__codexSendSnipProbe() });
                                }
                                const frames = Array.from(document.querySelectorAll('iframe'));
                                for (const frame of frames) {
                                    try {
                                        const probe = frame.contentWindow && frame.contentWindow.__codexSendSnipProbe;
                                        if (typeof probe === 'function') {
                                            return JSON.stringify({ context: 'iframe', sent: !!probe() });
                                        }
                                    } catch (error) {
                                    }
                                }
                                return JSON.stringify({ context: 'none', sent: false });
                            } catch (error) {
                                return JSON.stringify({ context: 'error', sent: false });
                            }
                        })();
                    ");
                    await System.Threading.Tasks.Task.Delay(450);
                    if (_lastSnipRequestUtc <= before)
                    {
                        throw new InvalidOperationException("bigscreen-snip-probe-not-received:" + (raw ?? string.Empty));
                    }
                });
                await runStep("bigscreen_snip_button_route", async delegate
                {
                    DateTime before = _lastSnipRequestUtc;
                    _treatSnipAsProbe = true;
                    try
                    {
                        string raw = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                            (async () => {
                                const wait = (ms) => new Promise((resolve) => window.setTimeout(resolve, ms));
                                let button = document.getElementById('codex-snip-btn');
                                let context = 'main';
                                if (!button) {
                                    const frames = Array.from(document.querySelectorAll('iframe'));
                                    for (const frame of frames) {
                                        try {
                                            const doc = frame.contentDocument;
                                            const btn = doc && doc.getElementById('codex-snip-btn');
                                            if (btn) {
                                                button = btn;
                                                context = 'iframe';
                                                break;
                                            }
                                        } catch (error) {
                                        }
                                    }
                                }
                                if (!button) {
                                    return JSON.stringify({ found: false, context: 'none' });
                                }
                                button.dispatchEvent(new MouseEvent('click', {
                                    bubbles: true,
                                    cancelable: true,
                                    composed: true,
                                    button: 0,
                                    buttons: 1,
                                    detail: 1,
                                    clientX: 18,
                                    clientY: 18
                                }));
                                await wait(180);
                                return JSON.stringify({
                                    found: true,
                                    context,
                                    title: String(button.getAttribute('title') || ''),
                                    active: !!button.classList.contains('codex-split-chat-active')
                                });
                            })();
                        ");
                        await System.Threading.Tasks.Task.Delay(450);
                        if (_lastSnipRequestUtc <= before)
                        {
                            throw new InvalidOperationException("bigscreen-snip-button-not-received:" + (raw ?? string.Empty));
                        }
                    }
                    finally
                    {
                        _treatSnipAsProbe = false;
                    }
                });
                await runStep("bigscreen_snip_button_live_route", async delegate
                {
                    DateTime before = _lastSnipRequestUtc;
                    string raw = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                        (async () => {
                            const wait = (ms) => new Promise((resolve) => window.setTimeout(resolve, ms));
                            let button = document.getElementById('codex-snip-btn');
                            let context = 'main';
                            if (!button) {
                                const frames = Array.from(document.querySelectorAll('iframe'));
                                for (const frame of frames) {
                                    try {
                                        const doc = frame.contentDocument;
                                        const btn = doc && doc.getElementById('codex-snip-btn');
                                        if (btn) {
                                            button = btn;
                                            context = 'iframe';
                                            break;
                                        }
                                    } catch (error) {
                                    }
                                }
                            }
                            if (!button) {
                                return JSON.stringify({ found: false, context: 'none' });
                            }
                            button.dispatchEvent(new MouseEvent('click', {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                button: 0,
                                buttons: 1,
                                detail: 1,
                                clientX: 18,
                                clientY: 18
                            }));
                            await wait(220);
                            return JSON.stringify({
                                found: true,
                                context,
                                title: String(button.getAttribute('title') || ''),
                                active: !!button.classList.contains('codex-split-chat-active')
                            });
                        })();
                    ");
                    await System.Threading.Tasks.Task.Delay(700);
                    if (_lastSnipRequestUtc <= before)
                    {
                        throw new InvalidOperationException("bigscreen-snip-live-not-received:" + (raw ?? string.Empty));
                    }
                });
                await runStep("login", async delegate
                {
                    OnLoginClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(350);
                });
                await runStep("reload", async delegate
                {
                    OnReloadClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(250);
                });
                await runStep("address_go", async delegate
                {
                    _addressBox.Text = "/embed/chat";
                    OnGoClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(450);
                });
                await runStep("browser", async delegate
                {
                    OnBrowserClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(150);
                });
                await runStep("back", async delegate
                {
                    OnBackClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(150);
                });
                await runStep("forward", async delegate
                {
                    OnForwardClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(150);
                });
                await runStep("mute_toggle", async delegate
                {
                    _muteButton.PerformClick();
                    await System.Threading.Tasks.Task.Delay(100);
                    _muteButton.PerformClick();
                    await System.Threading.Tasks.Task.Delay(100);
                });
                await runStep("pin_toggle", async delegate
                {
                    _pinButton.PerformClick();
                    await System.Threading.Tasks.Task.Delay(100);
                    _pinButton.PerformClick();
                    await System.Threading.Tasks.Task.Delay(100);
                });
                await runStep("zoom_in_out_reset", async delegate
                {
                    OnZoomInClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(100);
                    OnZoomOutClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(100);
                    OnZoomResetClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(100);
                });
                await runStep("media_popout_open_close", async delegate
                {
                    OnMediaPopoutClicked(this, EventArgs.Empty);
                    await System.Threading.Tasks.Task.Delay(700);
                    for (int i = OwnedForms.Length - 1; i >= 0; i--)
                    {
                        Form form = OwnedForms[i];
                        MediaPopoutForm popout = form as MediaPopoutForm;
                        if (popout != null && !popout.IsDisposed)
                        {
                            popout.Close();
                        }
                    }
                    await System.Threading.Tasks.Task.Delay(200);
                });
                await runStep("update_check_guarded", async delegate
                {
                    string ownerBackup = _config.UpdateRepoOwner;
                    string nameBackup = _config.UpdateRepoName;
                    try
                    {
                        _config.UpdateRepoOwner = string.Empty;
                        _config.UpdateRepoName = string.Empty;
                        await CheckForUpdatesAsync(false);
                    }
                    finally
                    {
                        _config.UpdateRepoOwner = ownerBackup;
                        _config.UpdateRepoName = nameBackup;
                    }
                });
                await runStep("update_check_network", async delegate
                {
                    string versionBackup = _config.AppVersion;
                    try
                    {
                        _config.AppVersion = "9999.0.0";
                        await CheckForUpdatesAsync(false);
                    }
                    finally
                    {
                        _config.AppVersion = versionBackup;
                    }
                });
            }
            catch (Exception ex)
            {
                failedSteps.Add("harness: " + ex.Message);
            }
            finally
            {
                AgentDebugLog.Write(
                    "post-fix",
                    "P3",
                    "DestinyChatDesktop.cs:4679",
                    "Toolbar self-test result",
                    new Dictionary<string, object>
                    {
                        { "ok", failedSteps.Count == 0 },
                        { "passedCount", passedSteps.Count },
                        { "failedCount", failedSteps.Count },
                        { "passed", passedSteps },
                        { "failed", failedSteps }
                    });

                if (_runToolbarSelfTest && !IsDisposed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
        }

        private void WriteSplitSelfTestResult(string rawResult)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selfTestResultPath))
                {
                    return;
                }

                string normalized = rawResult;
                if (!string.IsNullOrWhiteSpace(normalized) &&
                    normalized.Length >= 2 &&
                    normalized[0] == '"' &&
                    normalized[normalized.Length - 1] == '"')
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    normalized = serializer.Deserialize<string>(normalized);
                }

                File.WriteAllText(_selfTestResultPath, normalized ?? string.Empty);
            }
            catch
            {
            }
            finally
            {
                if (_runSplitSelfTest || _runPopoutPauseSelfTest || _runDualSelfTest || _runBigscreenGeometrySelfTest || _runStreamChatPanelSelfTest)
                {
                    BeginInvoke(new Action(Close));
                }
            }
        }

        private static int CoerceWebMessageInt(Dictionary<string, object> message, string key)
        {
            if (message == null || !message.ContainsKey(key))
            {
                return 0;
            }

            object raw = message[key];
            if (raw == null)
            {
                return 0;
            }

            try
            {
                if (raw is int)
                {
                    return (int)raw;
                }

                if (raw is long)
                {
                    long int64 = (long)raw;
                    if (int64 > int.MaxValue || int64 < int.MinValue)
                    {
                        return 0;
                    }

                    return (int)int64;
                }

                if (raw is double)
                {
                    double dbl = (double)raw;
                    if (double.IsNaN(dbl) || double.IsInfinity(dbl))
                    {
                        return 0;
                    }

                    return (int)Math.Round(dbl);
                }

                if (raw is decimal)
                {
                    return (int)(decimal)raw;
                }

                string text = Convert.ToString(raw, CultureInfo.InvariantCulture);
                int parsed;
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }
            catch
            {
            }

            return 0;
        }

        private static bool CoerceWebMessageBool(Dictionary<string, object> message, string key)
        {
            if (message == null || !message.ContainsKey(key))
            {
                return false;
            }

            object raw = message[key];
            if (raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                return (bool)raw;
            }

            bool parsed;
            if (bool.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out parsed))
            {
                return parsed;
            }

            return false;
        }

        private static int QuantizeLayoutPixel(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            const int step = 8;
            return (value + step / 2) / step * step;
        }

        private void OnMainWebViewMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object payload = serializer.DeserializeObject(e.WebMessageAsJson);
                Dictionary<string, object> message = payload as Dictionary<string, object>;
                if (message == null || !message.ContainsKey("type"))
                {
                    return;
                }

                string type = Convert.ToString(message["type"]);
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N0",
                    "DestinyChatDesktop.cs:5078",
                    "Main web message received",
                    new Dictionary<string, object>
                    {
                        { "type", type ?? string.Empty },
                        { "source", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                    });
                // #endregion

                if (string.Equals(type, "codex-snip-start", StringComparison.OrdinalIgnoreCase))
                {
                    string requestId = message.ContainsKey("requestId") ? Convert.ToString(message["requestId"]) : string.Empty;
                    bool probe = false;
                    if (message.ContainsKey("probe"))
                    {
                        object probeValue = message["probe"];
                        if (probeValue is bool)
                        {
                            probe = (bool)probeValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(probeValue), out probe);
                        }
                    }
                    if (_treatSnipAsProbe)
                    {
                        probe = true;
                    }
                    DateTime nowUtc = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(requestId) &&
                        string.Equals(requestId, _lastSnipRequestId, StringComparison.Ordinal) &&
                        (nowUtc - _lastSnipRequestUtc).TotalSeconds < 6)
                    {
                        // #region agent log
                        AgentDebugLog.Write(
                            "pre-fix",
                            "N1",
                            "DestinyChatDesktop.cs:5080",
                            "Snip duplicate request ignored",
                            new Dictionary<string, object>
                            {
                                { "requestId", requestId }
                            });
                        // #endregion
                        return;
                    }
                    _lastSnipRequestId = requestId ?? string.Empty;
                    _lastSnipRequestUtc = nowUtc;
                    // #region agent log
                    AgentDebugLog.Write(
                        "pre-fix",
                        "N1",
                        "DestinyChatDesktop.cs:5080",
                        "Snip start message received",
                        new Dictionary<string, object>
                        {
                            { "source", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty },
                            { "windowState", this.WindowState.ToString() },
                            { "requestId", requestId ?? string.Empty },
                            { "probe", probe }
                        });
                    // #endregion
                    try
                    {
                        JavaScriptSerializer ackSerializer = new JavaScriptSerializer();
                        _webView.CoreWebView2.PostWebMessageAsString(ackSerializer.Serialize(new Dictionary<string, object>
                        {
                            { "type", "codex-snip-ack" },
                            { "requestId", requestId ?? string.Empty }
                        }));
                    }
                    catch
                    {
                    }
                    // Debug: confirm C# received the message
                    _webView.CoreWebView2.ExecuteScriptAsync(
                        "document.getElementById('codex-snip-btn').style.outline='2px solid lime';");
                    if (probe)
                    {
                        return;
                    }
                    HandleSnipAndUploadAsync();
                    return;
                }

                if (string.Equals(type, "codex-snip-debug", StringComparison.OrdinalIgnoreCase))
                {
                    // #region agent log
                    AgentDebugLog.Write(
                        "pre-fix",
                        "N6",
                        "DestinyChatDesktop.cs:5110",
                        "Snip debug bridge message",
                        new Dictionary<string, object>
                        {
                            { "stage", message.ContainsKey("stage") ? Convert.ToString(message["stage"]) : string.Empty },
                            { "href", message.ContainsKey("href") ? Convert.ToString(message["href"]) : string.Empty },
                            { "inIframe", message.ContainsKey("inIframe") ? Convert.ToString(message["inIframe"]) : string.Empty },
                            { "hasHost", message.ContainsKey("hasHost") ? Convert.ToString(message["hasHost"]) : string.Empty },
                            { "hasTopHost", message.ContainsKey("hasTopHost") ? Convert.ToString(message["hasTopHost"]) : string.Empty },
                            { "sent", message.ContainsKey("sent") ? Convert.ToString(message["sent"]) : string.Empty },
                            { "error", message.ContainsKey("error") ? Convert.ToString(message["error"]) : string.Empty }
                        });
                    // #endregion
                    return;
                }

                if (string.Equals(type, "codex-dual-chat-request", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (string.Equals(type, "embedsState", StringComparison.OrdinalIgnoreCase))
                {
                    if (_suppressPageEmbedsState)
                    {
                        return;
                    }

                    ApplyEmbedsStateFromWebMessage(message);
                    return;
                }

                if (string.Equals(type, "codex-stream-chat-request", StringComparison.OrdinalIgnoreCase))
                {
                    if (_suppressPageEmbedsState)
                    {
                        return;
                    }

                    NotifyDualChatSourceChanged();
                    return;
                }

                if (string.Equals(type, "codex-dual-chat-state", StringComparison.OrdinalIgnoreCase))
                {
                    bool enabled = false;
                    bool available = false;
                    string chatUrl = message.ContainsKey("chatUrl") ? Convert.ToString(message["chatUrl"]) : string.Empty;
                    string emptyMessage = message.ContainsKey("message") ? Convert.ToString(message["message"]) : string.Empty;

                    if (message.ContainsKey("enabled"))
                    {
                        object enabledValue = message["enabled"];
                        if (enabledValue is bool)
                        {
                            enabled = (bool)enabledValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(enabledValue), out enabled);
                        }
                    }

                    if (message.ContainsKey("available"))
                    {
                        object availableValue = message["available"];
                        if (availableValue is bool)
                        {
                            available = (bool)availableValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(availableValue), out available);
                        }
                    }

                    if (_runSplitSelfTest || _runToolbarSelfTest)
                    {
                        // #region agent log
                        AgentDebugLog.Write(
                            "pre-fix",
                            "B2",
                            "DestinyChatDesktop.cs:4808",
                            "Dual chat host state message",
                            new Dictionary<string, object>
                            {
                                { "enabled", enabled },
                                { "available", available },
                                { "chatUrl", chatUrl ?? string.Empty },
                                { "message", emptyMessage ?? string.Empty },
                                { "currentPage", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                            });
                        // #endregion
                    }

                    return;
                }

                if (string.Equals(type, "codex-stream-chat-toggle", StringComparison.OrdinalIgnoreCase))
                {
                    if (_suppressPageEmbedsState)
                    {
                        return;
                    }

                    bool enabled = false;
                    if (message.ContainsKey("enabled"))
                    {
                        object enabledValue = message["enabled"];
                        if (enabledValue is bool)
                        {
                            enabled = (bool)enabledValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(enabledValue), out enabled);
                        }
                    }

                    if (_runSplitSelfTest || _runToolbarSelfTest || _runDualSelfTest)
                    {
                        AgentDebugLog.Write(
                            "pre-fix",
                            "B3",
                            "DestinyChatDesktop.cs:5250",
                            "Stream chat toggle message",
                            new Dictionary<string, object>
                            {
                                { "enabled", enabled },
                                { "isBigscreenHostMode", IsBigscreenHostMode() },
                                { "currentSource", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                            });
                    }

                    _dualChatHostEnabled = enabled;
                    if (!enabled)
                    {
                        _lastStreamChatSourceJson = null;
                        ApplyDualChatHostState(false, false, string.Empty, string.Empty);
                    }
                    else
                    {
                        _lastStreamChatSourceJson = null;
                        _lastDualChatLayoutFingerprint = null;
                        NotifyDualChatSourceChanged();
                    }

                    return;
                }

                if (string.Equals(type, "codex-bigscreen-dual-toggle", StringComparison.OrdinalIgnoreCase))
                {
                    if (_suppressPageEmbedsState)
                    {
                        return;
                    }

                    bool enabled = false;
                    if (message.ContainsKey("enabled"))
                    {
                        object enabledValue = message["enabled"];
                        if (enabledValue is bool)
                        {
                            enabled = (bool)enabledValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(enabledValue), out enabled);
                        }
                    }

                    if (_runSplitSelfTest || _runToolbarSelfTest || _runDualSelfTest)
                    {
                        AgentDebugLog.Write(
                            "pre-fix",
                            "B3",
                            "DestinyChatDesktop.cs:5250",
                            "Bigscreen dual toggle message",
                            new Dictionary<string, object>
                            {
                                { "enabled", enabled },
                                { "isBigscreenHostMode", IsBigscreenHostMode() },
                                { "currentSource", _webView != null && _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                            });
                    }

                    _dualChatHostEnabled = enabled;
                    if (!enabled)
                    {
                        ApplyDualChatHostState(false, false, string.Empty, string.Empty);
                    }
                    else
                    {
                        _lastStreamChatSourceJson = null;
                        _lastDualChatLayoutFingerprint = null;
                        NotifyDualChatSourceChanged();
                    }

                    return;
                }

                if (string.Equals(type, "codex-bigscreen-layout", StringComparison.OrdinalIgnoreCase))
                {
                    int chatTop = 0;
                    int inputTop = 0;
                    int viewportWidth = 0;
                    int viewportHeight = 0;
                    if (message.ContainsKey("chatTop"))
                    {
                        object chatTopValue = message["chatTop"];
                        if (chatTopValue is int)
                        {
                            chatTop = (int)chatTopValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(chatTopValue), out chatTop);
                        }
                    }

                    if (message.ContainsKey("inputTop"))
                    {
                        object inputTopValue = message["inputTop"];
                        if (inputTopValue is int)
                        {
                            inputTop = (int)inputTopValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(inputTopValue), out inputTop);
                        }
                    }

                    if (message.ContainsKey("windowWidth"))
                    {
                        object widthValue = message["windowWidth"];
                        if (widthValue is int)
                        {
                            viewportWidth = (int)widthValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(widthValue), out viewportWidth);
                        }
                    }

                    if (message.ContainsKey("windowHeight"))
                    {
                        object heightValue = message["windowHeight"];
                        if (heightValue is int)
                        {
                            viewportHeight = (int)heightValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(heightValue), out viewportHeight);
                        }
                    }

                    _bigscreenChatTopOffset = Math.Max(0, chatTop);
                    if (viewportWidth > 0)
                    {
                        _bigscreenViewportWidth = viewportWidth;
                    }
                    if (viewportHeight > 0)
                    {
                        _bigscreenViewportHeight = viewportHeight;
                    }
                    if (inputTop > 0)
                    {
                        _dualChatInputTop = Math.Max(0, inputTop);
                    }

                    if (_runToolbarSelfTest || _runSplitSelfTest || _runBigscreenGeometrySelfTest || _runDualSelfTest)
                    {
                        // #region agent log
                        AgentDebugLog.Write(
                            "pre-fix",
                            "C1",
                            "DestinyChatDesktop.cs:4909",
                            "Bigscreen layout message",
                            new Dictionary<string, object>
                            {
                                { "chatTop", _bigscreenChatTopOffset },
                                { "inputTop", _dualChatInputTop },
                                { "windowWidth", _bigscreenViewportWidth },
                                { "windowHeight", _bigscreenViewportHeight },
                                { "currentPage", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                            });
                        // #endregion
                    }
                    LayoutDualChatPanel();
                    return;
                }

                if (string.Equals(type, "codex-bigscreen-layout-debug", StringComparison.OrdinalIgnoreCase))
                {
                    if (_runToolbarSelfTest || _runSplitSelfTest)
                    {
                        string raw = serializer.Serialize(message);
                        // #region agent log
                        AgentDebugLog.Write(
                            "pre-fix",
                            "C2",
                            "DestinyChatDesktop.cs:4927",
                            "Bigscreen layout debug",
                            new Dictionary<string, object>
                            {
                                { "payload", raw ?? string.Empty }
                            });
                        // #endregion
                    }
                    return;
                }

                if (string.Equals(type, "codex-bigscreen-media-state", StringComparison.OrdinalIgnoreCase))
                {
                    string raw = serializer.Serialize(message);
                    string activeStreamUrl = ExtractActiveStreamPlayerUrlFromBigscreenMediaState(message);
                    if (!string.IsNullOrWhiteSpace(activeStreamUrl) &&
                        !string.Equals(_activeStreamPlayerUrl ?? string.Empty, activeStreamUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        _activeStreamPlayerUrl = activeStreamUrl;
                        _lastStreamChatSourceJson = null;
                        if (_dualChatHostEnabled)
                        {
                            NotifyDualChatSourceChanged();
                        }
                    }

                    // #region agent log
                    AgentDebugLog.Write(
                        "pre-fix",
                        "M1",
                        "DestinyChatDesktop.cs:4950",
                        "Bigscreen media state",
                        new Dictionary<string, object>
                        {
                            { "payload", raw ?? string.Empty },
                            { "currentPage", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                        });
                    // #endregion
                    return;
                }

                if (string.Equals(type, "codex-dual-chat-layout", StringComparison.OrdinalIgnoreCase))
                {
                    int inputTop = CoerceWebMessageInt(message, "inputTop");
                    int paneLeft = CoerceWebMessageInt(message, "paneLeft");
                    int paneTop = CoerceWebMessageInt(message, "paneTop");
                    int paneWidth = CoerceWebMessageInt(message, "paneWidth");
                    int paneHeight = CoerceWebMessageInt(message, "paneHeight");
                    int windowWidth = CoerceWebMessageInt(message, "windowWidth");
                    int windowHeight = CoerceWebMessageInt(message, "windowHeight");
                    bool layoutActive = CoerceWebMessageBool(message, "layoutActive");

                    string layoutFingerprint = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}|{4}|{5}",
                        QuantizeLayoutPixel(inputTop),
                        QuantizeLayoutPixel(paneLeft),
                        QuantizeLayoutPixel(paneTop),
                        QuantizeLayoutPixel(paneWidth),
                        QuantizeLayoutPixel(paneHeight),
                        layoutActive ? 1 : 0);
                    if (!string.IsNullOrEmpty(_lastDualChatLayoutFingerprint) &&
                        string.Equals(layoutFingerprint, _lastDualChatLayoutFingerprint, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _lastDualChatLayoutFingerprint = layoutFingerprint;

                    if (IsBigscreenPage())
                    {
                        if (inputTop > 0)
                        {
                            _dualChatInputTop = Math.Max(0, inputTop);
                        }
                    }
                    else
                    {
                        _dualChatInputTop = Math.Max(0, inputTop);
                    }
                    if (!IsBigscreenPage())
                    {
                        if (windowWidth > 0)
                        {
                            _embedChatLayoutViewportWidth = windowWidth;
                        }

                        if (windowHeight > 0)
                        {
                            _embedChatLayoutViewportHeight = windowHeight;
                        }
                    }
                    _dualChatPaneLeft = Math.Max(0, paneLeft);
                    _dualChatPaneTop = Math.Max(0, paneTop);
                    _dualChatPaneWidth = Math.Max(0, paneWidth);
                    _dualChatPaneHeight = Math.Max(0, paneHeight);
                    _dualChatLayoutActive = layoutActive;
                    if (_runSplitSelfTest || _runToolbarSelfTest)
                    {
                        // #region agent log
                        AgentDebugLog.Write(
                            "pre-fix",
                            "B3",
                            "DestinyChatDesktop.cs:4956",
                            "Dual chat layout message",
                            new Dictionary<string, object>
                            {
                                { "inputTop", _dualChatInputTop },
                                { "paneLeft", _dualChatPaneLeft },
                                { "paneTop", _dualChatPaneTop },
                                { "paneWidth", _dualChatPaneWidth },
                                { "paneHeight", _dualChatPaneHeight },
                                { "layoutActive", _dualChatLayoutActive },
                                { "isBigscreenPage", IsBigscreenPage() }
                            });
                        // #endregion
                    }
                    LayoutDualChatPanel();
                    return;
                }

                if (!_runSplitSelfTest)
                {
                    return;
                }

                if (!string.Equals(type, "split-self-test-result", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                AgentDebugLog.Write(
                    "post-fix",
                    "S1",
                    "DestinyChatDesktop.cs:5215",
                    "Split self-test result",
                    message);

                WriteSplitSelfTestResult(serializer.Serialize(message));
            }
            catch (Exception ex)
            {
                if (_runSplitSelfTest)
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    WriteSplitSelfTestResult(serializer.Serialize(new Dictionary<string, object>
                    {
                        { "type", "split-self-test-result" },
                        { "ok", false },
                        { "reason", "self-test-message-error" },
                        { "message", ex.Message }
                    }));
                }
            }
        }

        private async void HandleSnipAndUploadAsync()
        {
            try
            {
                bool wasFullScreen = _isFullScreen;
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N2",
                    "DestinyChatDesktop.cs:5444",
                    "Handle snip start",
                    new Dictionary<string, object>
                    {
                        { "windowStateBefore", this.WindowState.ToString() },
                        { "wasFullScreen", wasFullScreen }
                    });
                // #endregion

                if (wasFullScreen)
                {
                    this.Invoke((Action)(() =>
                    {
                        try
                        {
                            ToggleFullScreen();
                        }
                        catch
                        {
                        }
                    }));
                    await System.Threading.Tasks.Task.Delay(180);
                }

                // Keep the app visible and trigger snip overlay directly.
                await System.Threading.Tasks.Task.Delay(120);

                // Clear clipboard so we can detect when a new snip lands
                this.Invoke((Action)(() => { try { Clipboard.Clear(); } catch { } }));

                // Send Win+Shift+S — system-level hotkey that triggers the snip overlay
                // on all Windows 10/11 machines regardless of which snip app is installed.
                // The snip overlay auto-copies the selection to clipboard on completion.
                SendSnipHotkey();

                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N2",
                    "DestinyChatDesktop.cs:5458",
                    "Snip hotkey sent",
                    new Dictionary<string, object>
                    {
                        { "windowStateAfterHotkey", this.WindowState.ToString() },
                        { "wasFullScreen", wasFullScreen }
                    });
                // #endregion

                // Poll clipboard for up to 45 seconds for a new image
                System.Drawing.Image snip = null;
                int pollCount = 0;
                for (int i = 0; i < 225; i++)
                {
                    await System.Threading.Tasks.Task.Delay(200);
                    pollCount = i + 1;
                    this.Invoke((Action)(() =>
                    {
                        try { if (Clipboard.ContainsImage()) snip = Clipboard.GetImage(); } catch { }
                    }));
                    if (snip != null) break;
                }

                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N3",
                    "DestinyChatDesktop.cs:5475",
                    "Snip clipboard poll finished",
                    new Dictionary<string, object>
                    {
                        { "pollCount", pollCount },
                        { "hasImage", snip != null }
                    });
                // #endregion

                if (snip == null)
                {
                    PostSnipResult(null, "timeout");
                    return;
                }

                // Encode image as PNG bytes
                byte[] pngBytes;
                try
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        snip.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        pngBytes = ms.ToArray();
                    }
                }
                catch
                {
                    PostSnipResult(null, "encode");
                    return;
                }

                // Upload to femboy.beauty as multipart/form-data
                string uploadedUrl = null;
                try
                {
                    string boundary = "DestinyChatBoundary" + Guid.NewGuid().ToString("N");
                    byte[] headerBytes = Encoding.UTF8.GetBytes(
                        "--" + boundary + "\r\n" +
                        "Content-Disposition: form-data; name=\"file\"; filename=\"snip.png\"\r\n" +
                        "Content-Type: image/png\r\n\r\n");
                    byte[] footerBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

                    byte[] body = new byte[headerBytes.Length + pngBytes.Length + footerBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, body, 0, headerBytes.Length);
                    Buffer.BlockCopy(pngBytes, 0, body, headerBytes.Length, pngBytes.Length);
                    Buffer.BlockCopy(footerBytes, 0, body, headerBytes.Length + pngBytes.Length, footerBytes.Length);

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://femboy.beauty/api/upload");
                    req.Method = "POST";
                    req.ContentType = "multipart/form-data; boundary=" + boundary;
                    req.ContentLength = body.Length;

                    using (Stream stream = await req.GetRequestStreamAsync())
                    {
                        await stream.WriteAsync(body, 0, body.Length);
                    }

                    using (WebResponse resp = await req.GetResponseAsync())
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = await reader.ReadToEndAsync();
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        Dictionary<string, object> dict = ser.DeserializeObject(json) as Dictionary<string, object>;
                        if (dict != null && dict.ContainsKey("link"))
                            uploadedUrl = Convert.ToString(dict["link"]);
                    }
                }
                catch (Exception ex)
                {
                    // #region agent log
                    AgentDebugLog.Write(
                        "pre-fix",
                        "N4",
                        "DestinyChatDesktop.cs:5534",
                        "Snip upload failed",
                        new Dictionary<string, object>
                        {
                            { "error", ex.Message }
                        });
                    // #endregion
                }

                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N4",
                    "DestinyChatDesktop.cs:5550",
                    "Snip upload finished",
                    new Dictionary<string, object>
                    {
                        { "hasUploadedUrl", !string.IsNullOrEmpty(uploadedUrl) }
                    });
                // #endregion

                if (string.IsNullOrEmpty(uploadedUrl))
                {
                    PostSnipResult(null, "upload");
                    return;
                }

                PostSnipResult(uploadedUrl, null);
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N2",
                    "DestinyChatDesktop.cs:5444",
                    "Handle snip failed unexpectedly",
                    new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
                // #endregion
                PostSnipResult(null, "exception");
            }
        }

        private void PostSnipResult(string url, string error)
        {
            try
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N5",
                    "DestinyChatDesktop.cs:5557",
                    "Posting snip result to webview",
                    new Dictionary<string, object>
                    {
                        { "hasUrl", !string.IsNullOrEmpty(url) },
                        { "error", error ?? string.Empty }
                    });
                // #endregion
                System.Diagnostics.Debug.WriteLine("[Snip] PostSnipResult url=" + url + " error=" + error);
                string urlJson = url == null ? "null" : "\"" + url.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                string errJson = error == null ? "null" : "\"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                string payload = "{\"type\":\"codex-snip-done\",\"url\":" + urlJson + ",\"error\":" + errJson + "}";
                _webView.CoreWebView2.PostWebMessageAsString(payload);
                if (_bigscreenBarView != null && _bigscreenBarView.CoreWebView2 != null)
                {
                    _bigscreenBarView.CoreWebView2.PostWebMessageAsString(payload);
                }
                _webView.CoreWebView2.ExecuteScriptAsync(
                    "(function(){try{const msg={type:'codex-snip-done',url:" + urlJson + ",error:" + errJson + "};window.postMessage(msg,'*');const frames=document.querySelectorAll('iframe');for(let i=0;i<frames.length;i++){try{frames[i].contentWindow&&frames[i].contentWindow.postMessage(msg,'*');}catch(_){}}}catch(_){}})();");
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N5",
                    "DestinyChatDesktop.cs:4870",
                    "Snip result post failed",
                    new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
                // #endregion
            }
        }

        private void OnHistoryChanged(object sender, object e)
        {
            UpdateNavigationButtons();
        }

        private void OnDocumentTitleChanged(object sender, object e)
        {
            UpdateWindowTitle();
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            Uri uri;
            if (TryBuildUri(e.Uri, out uri) && IsAllowedUri(uri))
            {
                e.Handled = true;
                NavigateToUrl(uri.AbsoluteUri);
                return;
            }

            e.Handled = true;
            OpenInDefaultBrowser(e.Uri);
        }

        private void OnDualChatNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;

            if (!string.IsNullOrWhiteSpace(e.Uri))
            {
                OpenInDefaultBrowser(e.Uri);
                SetStatus("Opened stream chat verification in your default browser.");
            }
        }

        private void OnDualChatNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // #region agent log
            AgentDebugLog.Write(
                "pre-fix",
                "K1",
                "DestinyChatDesktop.cs:5332",
                "Dual chat navigation starting",
                new Dictionary<string, object>
                {
                    { "uri", e != null ? e.Uri ?? string.Empty : string.Empty },
                    { "hostEnabled", _dualChatHostEnabled },
                    { "hostAvailable", _dualChatHostAvailable },
                    { "panelVisible", _dualChatPanel != null && _dualChatPanel.Visible },
                    { "viewVisible", _dualChatView != null && _dualChatView.Visible }
                });
            // #endregion
        }

        private async void OnDualChatNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _dualChatNavigationInFlight = false;

            string source = string.Empty;
            if (_dualChatView != null && _dualChatView.Source != null)
            {
                source = _dualChatView.Source.AbsoluteUri;
            }

            // #region agent log
            AgentDebugLog.Write(
                "pre-fix",
                "K2",
                "DestinyChatDesktop.cs:5365",
                "Dual chat navigation completed",
                new Dictionary<string, object>
                {
                    { "isSuccess", e != null && e.IsSuccess },
                    { "webErrorStatus", e != null ? e.WebErrorStatus.ToString() : string.Empty },
                    { "source", source },
                    { "requestedUrl", _dualChatRequestedUrl ?? string.Empty }
                });
            // #endregion

            if (_dualChatView == null || _dualChatView.CoreWebView2 == null)
            {
                return;
            }

            await ProbeDualChatDomAsync("completed");
            await System.Threading.Tasks.Task.Delay(400);
            await ProbeDualChatDomAsync("completed+400ms");
            await System.Threading.Tasks.Task.Delay(400);
            await ProbeDualChatDomAsync("completed+800ms");
        }

        private static async System.Threading.Tasks.Task<string> ExecuteWebViewScriptWithTimeoutAsync(
            CoreWebView2 core,
            string script,
            int timeoutMs)
        {
            if (core == null)
            {
                return null;
            }

            System.Threading.Tasks.Task<string> scriptTask = core.ExecuteScriptAsync(script);
            System.Threading.Tasks.Task winner = await System.Threading.Tasks.Task.WhenAny(
                scriptTask,
                System.Threading.Tasks.Task.Delay(timeoutMs)).ConfigureAwait(true);
            if (winner != scriptTask)
            {
                return null;
            }

            return await scriptTask.ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task ProbeDualChatDomAsync(string stage)
        {
            if (_dualChatView == null || _dualChatView.CoreWebView2 == null)
            {
                return;
            }

            string source = string.Empty;
            if (_dualChatView.Source != null)
            {
                source = _dualChatView.Source.AbsoluteUri;
            }

            try
            {
                const int probeScriptTimeoutMs = 10000;
                string probe = await ExecuteWebViewScriptWithTimeoutAsync(
                    _dualChatView.CoreWebView2,
                    @"
                    (() => {
                        const body = document.body;
                        const messageCandidates = document.querySelectorAll('[class*=""chat""], [class*=""message""], [id*=""chat""], [id*=""message""]');
                        const inputCandidates = document.querySelectorAll('textarea, input[type=""text""], [contenteditable=""true""]');
                        const title = document.title || '';
                        const href = location.href || '';
                        const ready = document.readyState || '';
                        const bodyPointerEvents = body ? getComputedStyle(body).pointerEvents : '';
                        const bodyOverflow = body ? getComputedStyle(body).overflow : '';
                        const htmlOverflow = document.documentElement ? getComputedStyle(document.documentElement).overflow : '';
                        const iframeCount = document.querySelectorAll('iframe').length;
                        const disabledCount = document.querySelectorAll('[disabled], [aria-disabled=""true""]').length;
                        const bodyText = body && body.innerText ? body.innerText.trim().slice(0, 220) : '';
                        return JSON.stringify({
                            href: href,
                            title: title,
                            ready: ready,
                            bodyPointerEvents: bodyPointerEvents,
                            bodyOverflow: bodyOverflow,
                            htmlOverflow: htmlOverflow,
                            messageCandidateCount: messageCandidates.length,
                            inputCandidateCount: inputCandidates.length,
                            iframeCount: iframeCount,
                            disabledCount: disabledCount,
                            bodyText: bodyText
                        });
                    })();
                ",
                    probeScriptTimeoutMs);

                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "K3",
                    "DestinyChatDesktop.cs:5402",
                    "Dual chat DOM probe",
                    new Dictionary<string, object>
                    {
                        { "stage", stage ?? string.Empty },
                        { "result", probe ?? string.Empty },
                        { "source", source }
                    });
                // #endregion
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "K4",
                    "DestinyChatDesktop.cs:5417",
                    "Dual chat DOM probe failed",
                    new Dictionary<string, object>
                    {
                        { "stage", stage ?? string.Empty },
                        { "error", ex.Message },
                        { "source", source }
                    });
                // #endregion
            }
        }

        private void OnMainFormResize(object sender, EventArgs e)
        {
            LayoutDualChatPanel();
        }

        private void LayoutDualChatPanel()
        {
            bool shouldShow = _dualChatHostEnabled && (IsChatPage() || IsBigscreenHostMode());
            if (!shouldShow)
            {
                _dualChatPanel.Bounds = Rectangle.Empty;
                _dualChatPanel.Visible = false;
                return;
            }

            Rectangle webViewBounds = _webView.Bounds;
            int top = webViewBounds.Top;
            int bottom = webViewBounds.Bottom;
            int width = Math.Max(320, (webViewBounds.Width - 14) / 2);
            int height = Math.Max(0, bottom - top);
            int left = webViewBounds.Right - width;
            bool hasReportedPaneBounds = _dualChatPaneWidth > 0 && _dualChatPaneHeight > 0;
            bool hasReportedInputTop = _dualChatInputTop > 0;
            bool hasAnyUsableBounds = IsBigscreenPage()
                ? (hasReportedPaneBounds || _bigscreenChatTopOffset > 0)
                : (hasReportedPaneBounds || hasReportedInputTop);
            if (!hasAnyUsableBounds)
            {
                _dualChatPanel.Bounds = Rectangle.Empty;
                _dualChatPanel.Visible = false;
                return;
            }

            double zoomFactor = _browserReady ? ClampZoom(_webView.ZoomFactor) : ClampZoom(_state.ZoomFactor);
            double pixelScaleX = zoomFactor;
            double pixelScaleY = zoomFactor;

            if (IsBigscreenPage())
            {
                if (_bigscreenViewportWidth > 0)
                {
                    pixelScaleX = (double)webViewBounds.Width / _bigscreenViewportWidth;
                }

                if (_bigscreenViewportHeight > 0)
                {
                    pixelScaleY = (double)webViewBounds.Height / _bigscreenViewportHeight;
                }
            }

            if (IsBigscreenPage())
            {
                if (hasReportedPaneBounds)
                {
                    left = webViewBounds.Left + (int)Math.Round(_dualChatPaneLeft * pixelScaleX);
                    width = Math.Max(0, (int)Math.Round(_dualChatPaneWidth * pixelScaleX));
                }

                if (_bigscreenChatTopOffset > 0)
                {
                    top = webViewBounds.Top + (int)Math.Round(_bigscreenChatTopOffset * pixelScaleY);
                    top = Math.Max(webViewBounds.Top, Math.Min(webViewBounds.Bottom, top));
                }

                height = Math.Max(0, webViewBounds.Bottom - top);
            }
            else if (hasReportedPaneBounds)
            {
                double scaleX = zoomFactor;
                double scaleY = zoomFactor;
                if (_embedChatLayoutViewportWidth > 0)
                {
                    scaleX = (double)webViewBounds.Width / _embedChatLayoutViewportWidth;
                }

                if (_embedChatLayoutViewportHeight > 0)
                {
                    scaleY = (double)webViewBounds.Height / _embedChatLayoutViewportHeight;
                }

                left = webViewBounds.Left + (int)Math.Round(_dualChatPaneLeft * scaleX);
                top = webViewBounds.Top + (int)Math.Round(_dualChatPaneTop * scaleY);
                width = Math.Max(0, (int)Math.Round(_dualChatPaneWidth * scaleX));
                height = Math.Max(0, (int)Math.Round(_dualChatPaneHeight * scaleY));
            }

            _dualChatPanel.Bounds = new Rectangle(left, top, width, height);
            Rectangle clampedBounds = _dualChatPanel.Bounds;
            int minLeft = webViewBounds.Left;
            int maxRight = webViewBounds.Right;
            int minTop = webViewBounds.Top;
            int maxBottom = webViewBounds.Bottom;

            if (hasReportedInputTop && !IsBigscreenPage())
            {
                double inputScaleY = zoomFactor;
                if (_embedChatLayoutViewportHeight > 0)
                {
                    inputScaleY = (double)webViewBounds.Height / _embedChatLayoutViewportHeight;
                }

                int inputTop = webViewBounds.Top + (int)Math.Round(_dualChatInputTop * inputScaleY);
                maxBottom = Math.Min(maxBottom, inputTop);
            }

            clampedBounds.X = Math.Max(minLeft, Math.Min(clampedBounds.X, maxRight));
            clampedBounds.Y = Math.Max(minTop, Math.Min(clampedBounds.Y, maxBottom));
            clampedBounds.Width = Math.Max(0, Math.Min(clampedBounds.Width, maxRight - clampedBounds.X));
            clampedBounds.Height = Math.Max(0, Math.Min(clampedBounds.Height, maxBottom - clampedBounds.Y));
            _dualChatPanel.Bounds = clampedBounds;
            _dualChatPanel.Visible = _dualChatPanel.Bounds.Height > 0 && _dualChatPanel.Bounds.Width > 0;
            _dualChatPanel.BringToFront();
            if (_runSplitSelfTest || _runToolbarSelfTest)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "B1",
                    "DestinyChatDesktop.cs:5234",
                    "Dual chat panel bounds applied",
                    new Dictionary<string, object>
                    {
                        { "webViewLeft", webViewBounds.Left },
                        { "webViewTop", webViewBounds.Top },
                        { "webViewWidth", webViewBounds.Width },
                        { "webViewHeight", webViewBounds.Height },
                        { "panelLeft", _dualChatPanel.Bounds.Left },
                        { "panelTop", _dualChatPanel.Bounds.Top },
                        { "panelWidth", _dualChatPanel.Bounds.Width },
                        { "panelHeight", _dualChatPanel.Bounds.Height },
                        { "layoutActive", _dualChatLayoutActive },
                        { "reportedPaneLeft", _dualChatPaneLeft },
                        { "reportedPaneTop", _dualChatPaneTop },
                        { "reportedPaneWidth", _dualChatPaneWidth },
                        { "reportedPaneHeight", _dualChatPaneHeight },
                        { "reportedInputTop", _dualChatInputTop },
                        { "reportedWindowWidth", _bigscreenViewportWidth },
                        { "reportedWindowHeight", _bigscreenViewportHeight },
                        { "pixelScaleX", pixelScaleX },
                        { "pixelScaleY", pixelScaleY },
                        { "dualChatZIndex", Controls.GetChildIndex(_dualChatPanel) },
                        { "mainWebViewZIndex", Controls.GetChildIndex(_webView) },
                        { "dualChatPanelEnabled", _dualChatPanel.Enabled },
                        { "dualChatViewEnabled", _dualChatView.Enabled },
                        { "zoomFactor", zoomFactor }
                    });
                // #endregion
            }

            if (_bigscreenBarPanel.Visible)
            {
                _bigscreenBarPanel.BringToFront();
            }

            if (_toolStrip.Visible)
            {
                _toolStrip.BringToFront();
            }

            if (_statusStrip.Visible)
            {
                _statusStrip.BringToFront();
            }
        }

        private void ApplyDualChatHostState(bool enabled, bool available, string chatUrl, string emptyMessage)
        {
            _dualChatHostEnabled = enabled;
            _dualChatHostAvailable = available;
            _dualChatEmptyMessage = string.IsNullOrWhiteSpace(emptyMessage)
                ? "No live stream chat is available for the current stream selection."
                : emptyMessage.Trim();

            ApplyBigscreenExternalChatLayout(enabled && IsBigscreenPage());

            if (!_browserReady || _dualChatView.CoreWebView2 == null)
            {
                LayoutDualChatPanel();
                return;
            }

            if (!enabled || (!IsChatPage() && !IsBigscreenHostMode()))
            {
                _lastStreamChatSourceJson = null;
                _lastDualChatLayoutFingerprint = null;
                _activeStreamPlayerUrl = null;
                _dualChatInputTop = 0;
                _dualChatPaneLeft = 0;
                _dualChatPaneTop = 0;
                _dualChatPaneWidth = 0;
                _dualChatPaneHeight = 0;
                _dualChatLayoutActive = false;
                _embedChatLayoutViewportWidth = 0;
                _embedChatLayoutViewportHeight = 0;
                _dualChatPanel.Visible = false;
                _dualChatView.Visible = false;
                _dualChatEmptyLabel.Visible = false;
                NavigateDualChatViewIfNeeded("about:blank");
                return;
            }

            LayoutDualChatPanel();
            _dualChatPanel.Visible = _dualChatPanel.Bounds.Width > 0 && _dualChatPanel.Bounds.Height > 0;

            if (available && !string.IsNullOrWhiteSpace(chatUrl))
            {
                string streamNavUri = NormalizeKickStreamChatNavigationUrl(chatUrl);
                _dualChatEmptyLabel.Visible = false;
                _dualChatView.Visible = true;
                _dualChatEmptyLabel.SendToBack();
                _dualChatView.BringToFront();
                NavigateDualChatViewIfNeeded(streamNavUri);
            }
            else
            {
                _dualChatView.Visible = false;
                _dualChatEmptyLabel.Text = _dualChatEmptyMessage;
                _dualChatEmptyLabel.Visible = true;
                _dualChatEmptyLabel.BringToFront();
                NavigateDualChatViewIfNeeded("about:blank");
            }
        }

        private void ResetChatLayoutForModeSwitch()
        {
            _lastStreamChatSourceJson = null;
            _lastDualChatLayoutFingerprint = null;
            _dualChatInputTop = 0;
            _dualChatPaneLeft = 0;
            _dualChatPaneTop = 0;
            _dualChatPaneWidth = 0;
            _dualChatPaneHeight = 0;
            _dualChatLayoutActive = false;
            _embedChatLayoutViewportWidth = 0;
            _embedChatLayoutViewportHeight = 0;

            ApplyDualChatHostState(false, false, string.Empty, string.Empty);
            ResetInjectedChatLayoutForModeSwitch();
            LayoutDualChatPanel();
        }

        private async void ResetInjectedChatLayoutForModeSwitch()
        {
            if (!_browserReady || _webView == null || _webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(@"
                    (() => {
                        function removeInlineLayout(el) {
                            if (!el || !el.style) return;
                            for (const prop of ['display','width','min-width','max-width','height','min-height','max-height','margin','padding','border','overflow','flex','left','right']) {
                                try { el.style.removeProperty(prop); } catch (error) {}
                            }
                        }

                        function resetDoc(doc) {
                            if (!doc || !doc.body) return;

                            try {
                                doc.dispatchEvent(new CustomEvent('codex:dualchat-set', { detail: { enabled: false } }));
                                doc.dispatchEvent(new CustomEvent('codex:split-set', { detail: { enabled: false } }));
                            } catch (error) {}

                            try {
                                doc.body.classList.remove(
                                    'codex-dual-chat-enabled',
                                    'codex-split-chat-enabled',
                                    'codex-bigscreen-dual-single-chat',
                                    'codex-desktop-external-chat-active',
                                    'codex-bigscreen-chat-below-media'
                                );
                            } catch (error) {}

                            try {
                                const root = doc.documentElement;
                                if (root) {
                                    root.style.removeProperty('--codex-dual-chat-bottom-offset');
                                    root.style.removeProperty('--codex-chat-below-media-push');
                                }
                            } catch (error) {}

                            try {
                                for (const btn of doc.querySelectorAll('#codex-stream-chat-btn,#codex-dual-stream-chat-btn,#codex-split-chat-btn')) {
                                    btn.classList.remove('codex-split-chat-active');
                                }
                            } catch (error) {}

                            try {
                                const dualPane = doc.getElementById('codex-dual-stream-pane');
                                if (dualPane) {
                                    dualPane.classList.remove('enabled', 'empty', 'codex-dual-chat-host-guest', 'codex-dual-stream-pane--bigscreen-fixed');
                                    const frame = dualPane.querySelector('iframe');
                                    if (frame) frame.setAttribute('src', 'about:blank');
                                }
                            } catch (error) {}

                            try {
                                const splitOverlay = doc.getElementById('codex-split-chat-overlay');
                                if (splitOverlay) splitOverlay.classList.remove('enabled');
                            } catch (error) {}

                            try {
                                for (const el of doc.querySelectorAll('.codex-bigscreen-dual-hide-target,.codex-bigscreen-dual-hide-sibling,#chat-wrap,.chat-wrap,#chat-output-frame,.chat-output-frame')) {
                                    el.classList.remove('codex-bigscreen-dual-hide-target', 'codex-bigscreen-dual-hide-sibling');
                                    removeInlineLayout(el);
                                }
                            } catch (error) {}
                        }

                        resetDoc(document);
                        for (const frame of Array.from(document.querySelectorAll('iframe'))) {
                            try {
                                if (frame.contentDocument) resetDoc(frame.contentDocument);
                            } catch (error) {}
                        }
                    })();
                ");
            }
            catch
            {
                // The page may already be navigating; the native host state was still reset.
            }
        }

        private void NavigateDualChatViewIfNeeded(string targetUri)
        {
            if (_dualChatView == null || _dualChatView.CoreWebView2 == null || string.IsNullOrWhiteSpace(targetUri))
            {
                return;
            }

            string currentUri = _dualChatView.Source != null ? _dualChatView.Source.AbsoluteUri : string.Empty;
            if (StreamChatNavTargetsEqual(currentUri, targetUri))
            {
                _dualChatRequestedUrl = targetUri;
                _dualChatNavigationInFlight = false;
                return;
            }

            if (_dualChatNavigationInFlight && StreamChatNavTargetsEqual(_dualChatRequestedUrl, targetUri))
            {
                return;
            }

            _dualChatRequestedUrl = targetUri;
            _dualChatNavigationInFlight = true;
            _dualChatView.CoreWebView2.Navigate(targetUri);
        }

        private bool TryForceBigscreenEmbedRoute()
        {
            if (_webView == null || _webView.Source == null)
            {
                return false;
            }

            Uri currentSource = _webView.Source;
            if (!currentSource.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool hasStreamFragment = !string.IsNullOrWhiteSpace(currentSource.Fragment) && currentSource.Fragment.Length > 1;
            if (hasStreamFragment)
            {
                return false;
            }

            BigscreenEmbedState selectedEmbed = GetSelectedBigscreenEmbed();
            if (selectedEmbed == null || string.IsNullOrWhiteSpace(selectedEmbed.Url))
            {
                return false;
            }

            Uri selectedUri;
            if (!TryBuildUri(selectedEmbed.Url, out selectedUri))
            {
                return false;
            }

            if (string.Equals(currentSource.AbsoluteUri, selectedUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // #region agent log
            AgentDebugLog.Write(
                "pre-fix",
                "M2",
                "DestinyChatDesktop.cs:5690",
                "Force bigscreen route to selected stream",
                new Dictionary<string, object>
                {
                    { "currentSource", currentSource.AbsoluteUri },
                    { "selectedSource", selectedUri.AbsoluteUri }
                });
            // #endregion

            NavigateToUrl(selectedUri.AbsoluteUri);
            return true;
        }

        private async void ApplyBigscreenExternalChatLayout(bool enabled)
        {
            if (!_browserReady || _webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                string script = @"
                    (() => {
                        const styleId = 'codex-desktop-external-chat-style';
                        const active = " + (enabled ? "true" : "false") + @";
                        const cssText = `
                                body.codex-desktop-external-chat-active #chat-wrap,
                                body.codex-desktop-external-chat-active .chat-wrap {
                                    width: calc((100% - 14px) / 2) !important;
                                    max-width: calc((100% - 14px) / 2) !important;
                                    box-sizing: border-box !important;
                                    overflow: hidden !important;
                                }
                                body.codex-desktop-external-chat-active #chat-output-frame,
                                body.codex-desktop-external-chat-active .chat-output-frame {
                                    width: 100% !important;
                                    max-width: 100% !important;
                                    box-sizing: border-box !important;
                                }
                                body.codex-desktop-external-chat-active #chat-input-frame,
                                body.codex-desktop-external-chat-active #chat-input-wrap,
                                body.codex-desktop-external-chat-active #chat-input-control,
                                body.codex-desktop-external-chat-active #chat-tools-wrap,
                                body.codex-desktop-external-chat-active .chat-tools-group {
                                    width: calc((100% - 14px) / 2) !important;
                                    max-width: calc((100% - 14px) / 2) !important;
                                    box-sizing: border-box !important;
                                }
                            `;

                        function applyToDoc(doc, activeState) {
                            if (!doc || !doc.body) {
                                return false;
                            }

                            let style = doc.getElementById(styleId);
                            if (!style && doc.head) {
                                style = doc.createElement('style');
                                style.id = styleId;
                                style.textContent = cssText;
                                doc.head.appendChild(style);
                            } else if (style && style.textContent !== cssText) {
                                style.textContent = cssText;
                            }

                            doc.body.classList.toggle('codex-desktop-external-chat-active', !!activeState);
                            return !!(doc.querySelector('#chat-wrap') || doc.querySelector('.chat-wrap'));
                        }

                        function removeFromDoc(doc) {
                            if (doc && doc.body) {
                                doc.body.classList.remove('codex-desktop-external-chat-active');
                            }
                        }

                        let appliedToEmbeddedChat = false;
                        for (const frame of Array.from(document.querySelectorAll('iframe'))) {
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '') || '').toLowerCase();
                            if (src.indexOf('/embed/chat') < 0) {
                                continue;
                            }

                            try {
                                const rect = frame.getBoundingClientRect();
                                const topWidth = Math.round(window.innerWidth || document.documentElement.clientWidth || 0);
                                const expectedHalf = topWidth > 0 ? Math.round((topWidth - 14) / 2) : 0;
                                const tolerance = Math.max(24, Math.round(topWidth * 0.04));
                                const needsInnerCollapse = active && (!expectedHalf || Math.round(rect.width || 0) > expectedHalf + tolerance);
                                if (frame.contentDocument && applyToDoc(frame.contentDocument, needsInnerCollapse)) {
                                    appliedToEmbeddedChat = true;
                                }
                            } catch (error) {
                            }
                        }

                        const appliedToTop = applyToDoc(document, active && !appliedToEmbeddedChat);
                        if (appliedToEmbeddedChat) {
                            removeFromDoc(document);
                        }

                        return JSON.stringify({
                            appliedToEmbeddedChat: appliedToEmbeddedChat,
                            appliedToTop: appliedToTop,
                            active: active
                        });
                    })();
                ";
                string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                if (_runToolbarSelfTest || _runSplitSelfTest)
                {
                    // #region agent log
                    AgentDebugLog.Write(
                        "post-fix",
                        "C3",
                        "DestinyChatDesktop.cs:5410",
                        "Applied bigscreen external chat layout",
                        new Dictionary<string, object>
                        {
                            { "enabled", enabled },
                            { "scriptResult", result ?? string.Empty },
                            { "currentPage", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                        });
                    // #endregion
                }
            }
            catch
            {
            }
        }

        private void OnBigscreenBarNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _bigscreenBarReady = e.IsSuccess;
            if (_bigscreenBarReady)
            {
                SetStatus("Top embeds synced from bigscreen.");
                UpdateBigscreenBarVisibility();
            }
        }

        private void OnBigscreenBarNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;

            if (!string.IsNullOrWhiteSpace(e.Uri))
            {
                OpenInDefaultBrowser(e.Uri);
            }
        }

        private void OnBigscreenEmbedsUiWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object payload = serializer.DeserializeObject(e.WebMessageAsJson);
                Dictionary<string, object> message = payload as Dictionary<string, object>;
                if (message == null || !message.ContainsKey("type"))
                {
                    return;
                }

                string messageType = Convert.ToString(message["type"]);
                if (!string.Equals(messageType, "openEmbed", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string url = message.ContainsKey("url") ? Convert.ToString(message["url"]) : string.Empty;
                string platform = message.ContainsKey("platform") ? Convert.ToString(message["platform"]) : string.Empty;
                string mediaId = message.ContainsKey("mediaId") ? Convert.ToString(message["mediaId"]) : string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return;
                }

                if (IsChatPage())
                {
                    _preferredChatEmbedUrl = url.Trim();
                    SelectStreamChatEmbedFromTopBar(url, platform, mediaId);
                    NotifyDualChatSourceChanged();
                    return;
                }

                _preferredChatEmbedUrl = url.Trim();
                string targetUrl = ResolveBigscreenSelectionUrl(url, platform, mediaId);
                if (string.IsNullOrWhiteSpace(targetUrl))
                {
                    targetUrl = BigscreenBarUrl;
                }

                NavigateToUrl(targetUrl);
                SetStatus("Opened bigscreen selection. Click Chat to return to chat-only view.");
            }
            catch
            {
                // Ignore malformed page messages.
            }
        }

        private void SelectStreamChatEmbedFromTopBar(string url, string platform, string mediaId)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            string platformTrim = platform != null ? platform.Trim() : string.Empty;
            string mediaIdTrim = mediaId != null ? mediaId.Trim() : string.Empty;
            if (_webView == null || _webView.CoreWebView2 == null)
            {
                ApplyChatModeEmbedsSelectionFallback(url, platformTrim, mediaIdTrim);
                return;
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string urlLit = serializer.Serialize(url.Trim());
            string platformLit = serializer.Serialize(platformTrim);
            string mediaIdLit = serializer.Serialize(mediaIdTrim);
            string script = @"
                (() => {
                    const targetUrl = " + urlLit + @";
                    const platform = " + platformLit + @";
                    const mediaId = " + mediaIdLit + @";
                    function norm(x) {
                        if (!x) {
                            return '';
                        }
                        try {
                            return new URL(x, window.location.href).href;
                        } catch (e) {
                            return (x + '').trim();
                        }
                    }
                    const nTarget = norm(targetUrl);
                    const nodes = document.querySelectorAll(
                        '#chat-panel-embeds a.embed-link, .chat-embeds a.embed-link, #embeds-panel a.embed-link');
                    for (let i = 0; i < nodes.length; i++) {
                        const a = nodes[i];
                        const href = norm(a.getAttribute('href') || '');
                        if (nTarget && href && href === nTarget) {
                            a.click();
                            return true;
                        }
                        if (platform && mediaId) {
                            const dp = (a.getAttribute('data-platform') || '').trim();
                            const did = (a.getAttribute('data-id') || a.getAttribute('data-mediaid') || '').trim();
                            if (dp === platform && did === mediaId) {
                                a.click();
                                return true;
                            }
                        }
                    }
                    return false;
                })()";

            RunSelectStreamChatEmbedFromTopBarAsync(script, url, platformTrim, mediaIdTrim);
        }

        private async void RunSelectStreamChatEmbedFromTopBarAsync(
            string script,
            string url,
            string platformTrim,
            string mediaIdTrim)
        {
            try
            {
                string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                string trimmed = (result ?? string.Empty).Trim();
                if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("Stream chat source updated.");
                    return;
                }
            }
            catch
            {
            }

            ApplyChatModeEmbedsSelectionFallback(url, platformTrim, mediaIdTrim);
            SetStatus("Stream chat source updated.");
        }

        private void ApplyChatModeEmbedsSelectionFallback(string url, string platformTrim, string mediaIdTrim)
        {
            if (_latestBigscreenEmbeds == null || _latestBigscreenEmbeds.Count == 0)
            {
                return;
            }

            bool found = false;
            bool selectedAssigned = false;
            List<object> items = new List<object>();
            foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
            {
                if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                {
                    continue;
                }

                bool isSelected = !selectedAssigned && EmbedFromTopBarClickMatchesState(embed, url, platformTrim, mediaIdTrim);
                if (isSelected)
                {
                    found = true;
                    selectedAssigned = true;
                }

                items.Add(new Dictionary<string, object>
                {
                    { "url", embed.Url },
                    { "platform", embed.Platform ?? string.Empty },
                    { "mediaId", embed.MediaId ?? string.Empty },
                    { "text", embed.DisplayText ?? string.Empty },
                    { "title", embed.TooltipText ?? string.Empty },
                    { "selected", isSelected }
                });
            }

            if (!found)
            {
                return;
            }

            Dictionary<string, object> message = new Dictionary<string, object>
            {
                { "items", items.ToArray() }
            };

            if (!string.IsNullOrWhiteSpace(_activeStreamPlayerUrl))
            {
                message["activeStreamUrl"] = _activeStreamPlayerUrl;
            }

            ApplyEmbedsStateFromWebMessage(message);
        }

        private static bool EmbedFromTopBarClickMatchesState(
            BigscreenEmbedState embed,
            string clickUrl,
            string platformTrim,
            string mediaIdTrim)
        {
            if (embed == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(clickUrl) &&
                string.Equals((embed.Url ?? string.Empty).Trim(), clickUrl.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(platformTrim) && !string.IsNullOrWhiteSpace(mediaIdTrim))
            {
                return string.Equals((embed.Platform ?? string.Empty).Trim(), platformTrim, StringComparison.OrdinalIgnoreCase)
                    && string.Equals((embed.MediaId ?? string.Empty).Trim(), mediaIdTrim, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool AreBigscreenEmbedStatesEqual(BigscreenEmbedState a, BigscreenEmbedState b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            return string.Equals(a.Url ?? string.Empty, b.Url ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Platform ?? string.Empty, b.Platform ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.MediaId ?? string.Empty, b.MediaId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.DisplayText ?? string.Empty, b.DisplayText ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(a.TooltipText ?? string.Empty, b.TooltipText ?? string.Empty, StringComparison.Ordinal)
                && a.IsSelected == b.IsSelected;
        }

        private static bool AreBigscreenEmbedListsEqual(List<BigscreenEmbedState> a, List<BigscreenEmbedState> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!AreBigscreenEmbedStatesEqual(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StreamChatNavTargetsEqual(string currentUri, string targetUri)
        {
            if (string.IsNullOrWhiteSpace(targetUri))
            {
                return string.IsNullOrWhiteSpace(currentUri);
            }

            if (string.IsNullOrWhiteSpace(currentUri))
            {
                return false;
            }

            Uri current;
            Uri target;
            if (!Uri.TryCreate(currentUri.Trim(), UriKind.Absolute, out current) ||
                !Uri.TryCreate(targetUri.Trim(), UriKind.Absolute, out target))
            {
                return string.Equals(currentUri.Trim(), targetUri.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            string hostCur = (current.IdnHost ?? string.Empty).ToLowerInvariant();
            string hostTgt = (target.IdnHost ?? string.Empty).ToLowerInvariant();
            if (hostCur.StartsWith("www.", StringComparison.Ordinal))
            {
                hostCur = hostCur.Substring(4);
            }

            if (hostTgt.StartsWith("www.", StringComparison.Ordinal))
            {
                hostTgt = hostTgt.Substring(4);
            }

            return string.Equals(current.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(hostCur, hostTgt, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    current.AbsolutePath.TrimEnd('/'),
                    target.AbsolutePath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(current.Query ?? string.Empty, target.Query ?? string.Empty, StringComparison.Ordinal);
        }

        private void ApplyEmbedsStateFromWebMessage(Dictionary<string, object> message)
        {
            if (message == null)
            {
                return;
            }

            string activeStreamUrl = string.Empty;
            if (message.ContainsKey("activeStreamUrl"))
            {
                activeStreamUrl = (Convert.ToString(message["activeStreamUrl"]) ?? string.Empty).Trim();
            }

            List<BigscreenEmbedState> embeds = new List<BigscreenEmbedState>();
            List<object> itemObjects = new List<object>();
            if (message.ContainsKey("items"))
            {
                object rawItems = message["items"];
                object[] arr = rawItems as object[];
                if (arr != null)
                {
                    itemObjects.AddRange(arr);
                }
                else
                {
                    System.Collections.ArrayList list = rawItems as System.Collections.ArrayList;
                    if (list != null)
                    {
                        foreach (object entry in list)
                        {
                            itemObjects.Add(entry);
                        }
                    }
                    else
                    {
                        System.Collections.IEnumerable enumerable = rawItems as System.Collections.IEnumerable;
                        if (enumerable != null && !(rawItems is string))
                        {
                            foreach (object entry in enumerable)
                            {
                                itemObjects.Add(entry);
                            }
                        }
                    }
                }
            }

            if (itemObjects.Count > 0)
            {
                foreach (object item in itemObjects)
                {
                    Dictionary<string, object> values = item as Dictionary<string, object>;
                    if (values == null)
                    {
                        System.Collections.Hashtable table = item as System.Collections.Hashtable;
                        if (table == null)
                        {
                            continue;
                        }

                        values = new Dictionary<string, object>();
                        foreach (System.Collections.DictionaryEntry entry in table)
                        {
                            values[Convert.ToString(entry.Key)] = entry.Value;
                        }
                    }

                    string url = values.ContainsKey("url") ? Convert.ToString(values["url"]) : string.Empty;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    bool isSelected = false;
                    if (values.ContainsKey("selected"))
                    {
                        object selectedValue = values["selected"];
                        if (selectedValue is bool)
                        {
                            isSelected = (bool)selectedValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(selectedValue), out isSelected);
                        }
                    }

                    embeds.Add(new BigscreenEmbedState
                    {
                        Url = url,
                        Platform = values.ContainsKey("platform") ? Convert.ToString(values["platform"]) : string.Empty,
                        MediaId = values.ContainsKey("mediaId") ? Convert.ToString(values["mediaId"]) : string.Empty,
                        DisplayText = values.ContainsKey("text") ? Convert.ToString(values["text"]) : string.Empty,
                        TooltipText = values.ContainsKey("title") ? Convert.ToString(values["title"]) : string.Empty,
                        IsSelected = isSelected
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(_preferredChatEmbedUrl))
            {
                bool matched = false;
                foreach (BigscreenEmbedState embed in embeds)
                {
                    if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                    {
                        continue;
                    }

                    bool isPreferred = string.Equals(embed.Url.Trim(), _preferredChatEmbedUrl, StringComparison.OrdinalIgnoreCase);
                    embed.IsSelected = isPreferred;
                    if (isPreferred)
                    {
                        matched = true;
                    }
                }

                if (!matched)
                {
                    _preferredChatEmbedUrl = null;
                }
            }

            if (AreBigscreenEmbedListsEqual(_latestBigscreenEmbeds, embeds) &&
                string.Equals(
                    _activeStreamPlayerUrl ?? string.Empty,
                    activeStreamUrl ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _activeStreamPlayerUrl = activeStreamUrl;
            _latestBigscreenEmbeds = embeds;
            if (_runSplitSelfTest || _runToolbarSelfTest)
            {
                BigscreenEmbedState selectedEmbed = null;
                foreach (BigscreenEmbedState embed in embeds)
                {
                    if (embed != null && embed.IsSelected)
                    {
                        selectedEmbed = embed;
                        break;
                    }
                }

                AgentDebugLog.Write(
                    "pre-fix",
                    "B2",
                    "DestinyChatDesktop.cs:ApplyEmbedsStateFromWebMessage",
                    "Embeds state message",
                    new Dictionary<string, object>
                    {
                        { "embedCount", embeds.Count },
                        { "selectedUrl", selectedEmbed != null ? (selectedEmbed.Url ?? string.Empty) : string.Empty },
                        { "selectedPlatform", selectedEmbed != null ? (selectedEmbed.Platform ?? string.Empty) : string.Empty },
                        { "selectedMediaId", selectedEmbed != null ? (selectedEmbed.MediaId ?? string.Empty) : string.Empty },
                        { "selectedText", selectedEmbed != null ? (selectedEmbed.DisplayText ?? string.Empty) : string.Empty }
                    });
            }

            UpdateBigscreenEmbeds(embeds);
            NotifyDualChatSourceChanged();
        }

        private void OnBigscreenBarWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object payload = serializer.DeserializeObject(e.WebMessageAsJson);
                Dictionary<string, object> message = payload as Dictionary<string, object>;
                if (message == null || !message.ContainsKey("type"))
                {
                    return;
                }

                string messageType = Convert.ToString(message["type"]);
                if (string.Equals(messageType, "codex-snip-start", StringComparison.OrdinalIgnoreCase))
                {
                    HandleSnipAndUploadAsync();
                    return;
                }
                if (string.Equals(messageType, "codex-snip-debug", StringComparison.OrdinalIgnoreCase))
                {
                    AgentDebugLog.Write(
                        "pre-fix",
                        "N6",
                        "DestinyChatDesktop.cs:6483",
                        "Snip debug from bigscreen bar",
                        new Dictionary<string, object>
                        {
                            { "stage", message.ContainsKey("stage") ? Convert.ToString(message["stage"]) : string.Empty },
                            { "href", message.ContainsKey("href") ? Convert.ToString(message["href"]) : string.Empty },
                            { "inIframe", message.ContainsKey("inIframe") ? Convert.ToString(message["inIframe"]) : string.Empty },
                            { "hasHost", message.ContainsKey("hasHost") ? Convert.ToString(message["hasHost"]) : string.Empty },
                            { "hasTopHost", message.ContainsKey("hasTopHost") ? Convert.ToString(message["hasTopHost"]) : string.Empty },
                            { "sent", message.ContainsKey("sent") ? Convert.ToString(message["sent"]) : string.Empty },
                            { "error", message.ContainsKey("error") ? Convert.ToString(message["error"]) : string.Empty }
                        });
                    return;
                }
                if (!string.Equals(messageType, "embedsState", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ApplyEmbedsStateFromWebMessage(message);
            }
            catch
            {
                // Ignore malformed page messages.
            }
        }

        private void OnBigscreenRefreshClicked(object sender, EventArgs e)
        {
            ReloadCurrentPage();

            if (_bigscreenBarView.CoreWebView2 != null)
            {
                _bigscreenBarView.Reload();
            }
        }

        private void OnBigscreenCinemaClicked(object sender, EventArgs e)
        {
            NavigateToUrl(BigscreenBarUrl);
            SetStatus("Opened bigscreen. Click Chat to return to chat-only view.");
        }

        private void OnBigscreenEmbedsResize(object sender, EventArgs e)
        {
            LayoutBigscreenEmbeds();
        }

        private void OnBigscreenBarPanelResize(object sender, EventArgs e)
        {
            LayoutBigscreenBarRows();
        }

        private void OnBigscreenEmbedsMouseEnter(object sender, EventArgs e)
        {
            _bigscreenEmbedsViewport.Focus();
        }

        private void OnBigscreenEmbedsMouseWheel(object sender, MouseEventArgs e)
        {
            int direction = e.Delta < 0 ? 1 : -1;
            ScrollBigscreenEmbedsBy(direction * 140);
        }

        private void ScrollBigscreenEmbedsBy(int delta)
        {
            int maxOffset = GetMaxBigscreenEmbedScrollOffset();
            if (maxOffset <= 0)
            {
                _bigscreenEmbedScrollOffset = 0;
                LayoutBigscreenEmbeds();
                return;
            }

            _bigscreenEmbedScrollOffset = Math.Max(0, Math.Min(maxOffset, _bigscreenEmbedScrollOffset + delta));
            LayoutBigscreenEmbeds();
        }

        private int GetMaxBigscreenEmbedScrollOffset()
        {
            int totalWidth = _bigscreenEmbedsPanel.PreferredSize.Width;
            int viewportWidth = _bigscreenEmbedsViewport.ClientSize.Width;
            return Math.Max(0, totalWidth - viewportWidth);
        }

        private void LayoutBigscreenEmbeds()
        {
            int viewportHeight = Math.Max(0, _bigscreenEmbedsViewport.ClientSize.Height);
            int totalWidth = Math.Max(_bigscreenEmbedsViewport.ClientSize.Width, _bigscreenEmbedsPanel.PreferredSize.Width);
            int maxOffset = GetMaxBigscreenEmbedScrollOffset();

            if (_bigscreenEmbedScrollOffset > maxOffset)
            {
                _bigscreenEmbedScrollOffset = maxOffset;
            }

            _bigscreenEmbedsPanel.Size = new Size(totalWidth, viewportHeight);
            _bigscreenEmbedsPanel.Location = new Point(-_bigscreenEmbedScrollOffset, 0);
        }

        private void LayoutBigscreenBarRows()
        {
            int controlHeight = _bigscreenControlsPanel.Height;
            _bigscreenControlsPanel.Location = new Point(0, 0);
            _bigscreenControlsPanel.Width = _bigscreenBarPanel.ClientSize.Width;

            int embedsTop = controlHeight;
            int embedsHeight = Math.Max(0, _bigscreenBarPanel.ClientSize.Height - embedsTop);
            _bigscreenEmbedsViewport.Location = new Point(0, embedsTop);
            _bigscreenEmbedsViewport.Size = new Size(_bigscreenBarPanel.ClientSize.Width, embedsHeight);
        }

        private void UpdateBigscreenEmbeds(List<BigscreenEmbedState> embeds)
        {
            if (_bigscreenEmbedsUiView.CoreWebView2 == null)
            {
                return;
            }

            _bigscreenEmbedsUiView.CoreWebView2.NavigateToString(BuildBigscreenEmbedsHtml(embeds));
        }

        private void NotifyDualChatSourceChanged()
        {
            if (_suppressPageEmbedsState)
            {
                return;
            }

            if (!_browserReady || _webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                Dictionary<string, object> payload = BuildDualChatSourcePayload();
                payload["type"] = "codex-stream-chat-source";

                bool available = payload.ContainsKey("available") && Convert.ToBoolean(payload["available"]);
                string chatUrl = payload.ContainsKey("chatUrl") ? Convert.ToString(payload["chatUrl"]) : string.Empty;
                string emptyMessage = payload.ContainsKey("reason") && !available
                    ? "No live stream chat is available for the current stream selection."
                    : string.Empty;

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string outboundJson = serializer.Serialize(payload);

                if (_dualChatHostEnabled)
                {
                    ApplyDualChatHostState(true, available, chatUrl, emptyMessage);
                }

                if (!string.IsNullOrEmpty(_lastStreamChatSourceJson) &&
                    string.Equals(outboundJson, _lastStreamChatSourceJson, StringComparison.Ordinal))
                {
                    return;
                }

                _lastStreamChatSourceJson = outboundJson;
                _webView.CoreWebView2.PostWebMessageAsJson(outboundJson);
            }
            catch
            {
                // Ignore transient messaging failures during navigation.
            }
        }

        private Dictionary<string, object> BuildDualChatSourcePayload()
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "type", "codex-dual-chat-source" },
                { "available", false },
                { "hostBigscreenMode", IsBigscreenHostMode() }
            };

            BigscreenEmbedState selectedEmbed = NormalizeBigscreenEmbedState(GetStreamChatEmbed());
            if (selectedEmbed == null)
            {
                payload["reason"] = "no-selection";
                return payload;
            }

            string chatUrl = BuildStreamChatUrl(selectedEmbed, false);
            string platform = string.IsNullOrWhiteSpace(selectedEmbed.Platform)
                ? string.Empty
                : selectedEmbed.Platform.Trim().ToLowerInvariant();
            payload["platform"] = platform;
            payload["platformLabel"] = FormatPlatformLabel(platform);
            payload["displayText"] = !string.IsNullOrWhiteSpace(selectedEmbed.DisplayText)
                ? selectedEmbed.DisplayText
                : (!string.IsNullOrWhiteSpace(selectedEmbed.TooltipText) ? selectedEmbed.TooltipText : selectedEmbed.MediaId);

            if (string.IsNullOrWhiteSpace(chatUrl))
            {
                payload["reason"] = "unsupported";
                if (_runSplitSelfTest || _runToolbarSelfTest)
                {
                    // #region agent log
                    AgentDebugLog.Write(
                        "pre-fix",
                        "B2",
                        "DestinyChatDesktop.cs:5730",
                        "Dual chat source payload unsupported",
                        new Dictionary<string, object>
                        {
                            { "selectedUrl", selectedEmbed.Url ?? string.Empty },
                            { "selectedPlatform", selectedEmbed.Platform ?? string.Empty },
                            { "selectedMediaId", selectedEmbed.MediaId ?? string.Empty },
                            { "currentSource", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                        });
                    // #endregion
                }
                return payload;
            }

            payload["available"] = true;
            payload["chatUrl"] = NormalizeKickStreamChatNavigationUrl(chatUrl);
            if (_runSplitSelfTest || _runToolbarSelfTest)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "B2",
                    "DestinyChatDesktop.cs:5736",
                    "Dual chat source payload",
                    new Dictionary<string, object>
                    {
                        { "selectedUrl", selectedEmbed.Url ?? string.Empty },
                        { "selectedPlatform", selectedEmbed.Platform ?? string.Empty },
                        { "selectedMediaId", selectedEmbed.MediaId ?? string.Empty },
                        { "chatUrl", chatUrl ?? string.Empty },
                        { "currentSource", _webView.Source != null ? _webView.Source.AbsoluteUri : string.Empty }
                    });
                // #endregion
            }
            return payload;
        }

        private static string ExtractActiveStreamPlayerUrlFromBigscreenMediaState(Dictionary<string, object> message)
        {
            if (message == null || !message.ContainsKey("iframePreview"))
            {
                return string.Empty;
            }

            List<object> frames = new List<object>();
            object rawFrames = message["iframePreview"];
            object[] arr = rawFrames as object[];
            if (arr != null)
            {
                frames.AddRange(arr);
            }
            else
            {
                System.Collections.ArrayList list = rawFrames as System.Collections.ArrayList;
                if (list != null)
                {
                    foreach (object entry in list)
                    {
                        frames.Add(entry);
                    }
                }
            }

            for (int i = 0; i < frames.Count; i++)
            {
                Dictionary<string, object> values = frames[i] as Dictionary<string, object>;
                if (values == null)
                {
                    System.Collections.Hashtable table = frames[i] as System.Collections.Hashtable;
                    if (table != null)
                    {
                        values = new Dictionary<string, object>();
                        foreach (System.Collections.DictionaryEntry entry in table)
                        {
                            values[Convert.ToString(entry.Key)] = entry.Value;
                        }
                    }
                }

                if (values == null || !values.ContainsKey("src"))
                {
                    continue;
                }

                string src = Convert.ToString(values["src"]);
                if (IsStreamPlayerUrl(src))
                {
                    return src.Trim();
                }
            }

            return string.Empty;
        }

        private static bool IsStreamPlayerUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            string lower = url.Trim().ToLowerInvariant();
            return lower.IndexOf("player.kick.com/") >= 0 ||
                lower.IndexOf("player.twitch.tv/") >= 0 ||
                lower.IndexOf("youtube.com/embed/") >= 0 ||
                lower.IndexOf("youtube-nocookie.com/embed/") >= 0;
        }

        private void OnBigscreenChipMouseEnter(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control != null)
            {
                control.Focus();
            }
        }

        private void OnBigscreenEmbedClicked(object sender, EventArgs e)
        {
            Control control = sender as Control;
            string url = control != null ? control.Tag as string : null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (IsChatPage())
            {
                SelectStreamChatEmbedFromTopBar(url, string.Empty, string.Empty);
                return;
            }

            string targetUrl = ResolveBigscreenSelectionUrl(url, null, null);
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                targetUrl = BigscreenBarUrl;
            }

            NavigateToUrl(targetUrl);
            SetStatus("Opened bigscreen selection. Click Chat to return to chat-only view.");
        }

        private string ResolveBigscreenSelectionUrl(string url, string platformHint, string mediaIdHint)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            string hintedRoute = BuildBigscreenRouteFromPlatformMedia(platformHint, mediaIdHint);
            if (!string.IsNullOrWhiteSpace(hintedRoute))
            {
                return hintedRoute;
            }

            BigscreenEmbedState parsed;
            if (TryCreateEmbedStateFromBigscreenUrl(url, out parsed))
            {
                return url;
            }

            BigscreenEmbedState matched = null;
            if (_latestBigscreenEmbeds != null)
            {
                foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
                {
                    if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                    {
                        continue;
                    }

                    if (string.Equals(embed.Url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = embed;
                        break;
                    }
                }
            }

            BigscreenEmbedState normalized = NormalizeBigscreenEmbedState(matched ?? new BigscreenEmbedState
            {
                Url = url
            });
            if (normalized == null || string.IsNullOrWhiteSpace(normalized.Platform) || string.IsNullOrWhiteSpace(normalized.MediaId))
            {
                Uri externalUri;
                if (Uri.TryCreate(url, UriKind.Absolute, out externalUri))
                {
                    string host = externalUri.Host ?? string.Empty;
                    string[] segments = externalUri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (host.IndexOf("kick.com", StringComparison.OrdinalIgnoreCase) >= 0 && segments.Length > 0)
                    {
                        return BuildBigscreenRouteFromPlatformMedia("kick", segments[0]);
                    }
                    if ((host.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 || host.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string videoId = string.Empty;
                        if (host.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase) >= 0 && segments.Length > 0)
                        {
                            videoId = segments[0];
                        }
                        else
                        {
                            videoId = GetQueryParameter(externalUri.Query, "v");
                        }

                        if (!string.IsNullOrWhiteSpace(videoId))
                        {
                            return BuildBigscreenRouteFromPlatformMedia("youtube", videoId);
                        }
                    }
                }
                return null;
            }

            string platform = normalized.Platform.Trim().Trim('/').ToLowerInvariant();
            string mediaId = normalized.MediaId.Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(mediaId))
            {
                return null;
            }

            return BuildBigscreenRouteFromPlatformMedia(platform, mediaId);
        }

        private static string BuildBigscreenRouteFromPlatformMedia(string platform, string mediaId)
        {
            if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(mediaId))
            {
                return null;
            }

            string normalizedPlatform = platform.Trim().Trim('/').ToLowerInvariant();
            string normalizedMediaId = mediaId.Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPlatform) || string.IsNullOrWhiteSpace(normalizedMediaId))
            {
                return null;
            }

            return BigscreenBarUrl + "#" + normalizedPlatform + "/" + normalizedMediaId;
        }

        private static string GetQueryParameter(string query, string key)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string trimmed = query.TrimStart('?');
            string[] parts = trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string[] kv = part.Split(new[] { '=' }, 2);
                if (kv.Length < 2)
                {
                    continue;
                }

                if (string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }

            return string.Empty;
        }

        private void UpdateNavigationButtons()
        {
            if (!_browserReady || _webView.CoreWebView2 == null)
            {
                _backButton.Enabled = false;
                _forwardButton.Enabled = false;
                _reloadButton.Enabled = false;
                return;
            }

            _backButton.Enabled = _webView.CoreWebView2.CanGoBack;
            _forwardButton.Enabled = _webView.CoreWebView2.CanGoForward;
            _reloadButton.Enabled = true;
        }

        private void UpdateWindowTitle()
        {
            string documentTitle = string.Empty;
            if (_browserReady && _webView.CoreWebView2 != null)
            {
                documentTitle = _webView.CoreWebView2.DocumentTitle;
            }

            if (string.IsNullOrWhiteSpace(documentTitle))
            {
                Text = _config.Title;
            }
            else
            {
                Text = documentTitle + " - " + _config.Title;
            }
        }

        private void NavigateBigscreenBar()
        {
            if (_bigscreenBarView.CoreWebView2 == null)
            {
                return;
            }

            Uri currentUri = _bigscreenBarView.Source;
            if (currentUri != null &&
                currentUri.AbsoluteUri.StartsWith(BigscreenBarUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _bigscreenBarView.CoreWebView2.Navigate(BigscreenBarUrl);
        }

        private void UpdateBigscreenBarVisibility()
        {
            bool shouldShow = ShouldShowBigscreenBar();
            _bigscreenBarPanel.Visible = shouldShow && _bigscreenBarReady;

            if (shouldShow)
            {
                NavigateBigscreenBar();
                LayoutBigscreenBarRows();
                _bigscreenControlsPanel.BringToFront();
            }

            LayoutDualChatPanel();
        }

        private bool ShouldShowBigscreenBar()
        {
            string currentUrl = _pendingNavigation;
            if (_browserReady && _webView.Source != null)
            {
                currentUrl = _webView.Source.AbsoluteUri;
            }

            Uri uri;
            if (!TryBuildUri(currentUrl, out uri))
            {
                return false;
            }

            return IsAllowedUri(uri) &&
                uri.AbsolutePath.StartsWith("/embed/chat", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateAddressBox(string url)
        {
            _addressBox.Text = url;
        }

        private void SetStatus(string message)
        {
            _statusLabel.Text = message;
        }

        private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
            NavigateFromAddressBar();
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
            NavigateBack();
        }

        private void OnForwardClicked(object sender, EventArgs e)
        {
            NavigateForward();
        }

        private void OnHomeClicked(object sender, EventArgs e)
        {
            NavigateToUrl(_config.HomeUrl);
        }

        private void OnBigscreenClicked(object sender, EventArgs e)
        {
            string targetUrl = null;
            if (!string.IsNullOrWhiteSpace(_preferredChatEmbedUrl))
            {
                string platformHint = null;
                string mediaIdHint = null;
                if (_latestBigscreenEmbeds != null)
                {
                    foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
                    {
                        if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                        {
                            continue;
                        }

                        if (string.Equals(embed.Url.Trim(), _preferredChatEmbedUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            platformHint = embed.Platform;
                            mediaIdHint = embed.MediaId;
                            break;
                        }
                    }
                }

                targetUrl = ResolveBigscreenSelectionUrl(_preferredChatEmbedUrl, platformHint, mediaIdHint);
            }

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                targetUrl = BigscreenBarUrl;
            }

            NavigateToUrl(targetUrl);
        }

        private void OnReloadClicked(object sender, EventArgs e)
        {
            ReloadCurrentPage();
        }

        private void OnLoginClicked(object sender, EventArgs e)
        {
            NavigateToUrl(_config.SiteUrl);
            SetStatus("Use the Destiny.gg login on this page, then click Chat to return to the embed.");
        }

        private async void OnMediaPopoutClicked(object sender, EventArgs e)
        {
            _mediaPopoutButton.Enabled = false;

            MediaPopoutTarget popoutTarget = null;
            string restoreUrl = null;
            try
            {
                popoutTarget = await GetMediaPopoutTargetAsync();
                restoreUrl = GetMediaRestoreUrl();
            }
            finally
            {
                _mediaPopoutButton.Enabled = true;
            }

            if (popoutTarget == null || string.IsNullOrWhiteSpace(popoutTarget.PlayerUrl))
            {
                SetStatus("No media is available to pop out yet.");
                return;
            }

            MediaPopoutForm popoutForm = new MediaPopoutForm(_storageRoot, popoutTarget);
            popoutForm.FormClosed += delegate
            {
                RestoreMediaToMainWindow(restoreUrl);
            };
            popoutForm.Show(this);
            SetStatus("Opened a picture-in-picture style media window.");

            Uri currentUri;
            if ((_browserReady && _webView.Source != null && (currentUri = _webView.Source) != null &&
                currentUri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase)) ||
                (TryBuildUri(_pendingNavigation, out currentUri) &&
                 currentUri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase)))
            {
                NavigateToUrl(_config.HomeUrl);
            }
        }

        private void OnGoClicked(object sender, EventArgs e)
        {
            NavigateFromAddressBar();
        }

        private void OnMuteClicked(object sender, EventArgs e)
        {
            if (_browserReady && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.IsMuted = _muteButton.Checked;
            }

            SetStatus(_muteButton.Checked ? "Audio muted." : "Audio unmuted.");
        }

        private void OnPinClicked(object sender, EventArgs e)
        {
            TopMost = _pinButton.Checked;
            SetStatus(_pinButton.Checked ? "Window pinned on top." : "Window pin disabled.");
        }

        private void OnZoomOutClicked(object sender, EventArgs e)
        {
            AdjustZoom(-0.10);
        }

        private void OnZoomResetClicked(object sender, EventArgs e)
        {
            ResetZoom();
        }

        private void OnZoomInClicked(object sender, EventArgs e)
        {
            AdjustZoom(0.10);
        }

        private void OnBrowserClicked(object sender, EventArgs e)
        {
            string url = _browserReady && _webView.Source != null ? _webView.Source.AbsoluteUri : _pendingNavigation;
            OpenInDefaultBrowser(url);
        }

        private void NavigateBack()
        {
            if (_browserReady && _webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoBack)
            {
                _webView.CoreWebView2.GoBack();
            }
        }

        private void NavigateForward()
        {
            if (_browserReady && _webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoForward)
            {
                _webView.CoreWebView2.GoForward();
            }
        }

        private void ReloadCurrentPage()
        {
            if (_browserReady && _webView.CoreWebView2 != null)
            {
                _webView.Reload();
            }
        }

        private async System.Threading.Tasks.Task<MediaPopoutTarget> GetMediaPopoutTargetAsync()
        {
            if (_browserReady && _webView.Source != null)
            {
                Uri sourceUri = _webView.Source;
                if (sourceUri != null &&
                    IsAllowedUri(sourceUri) &&
                    sourceUri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase))
                {
                    BigscreenEmbedState currentEmbed;
                    if (TryCreateEmbedStateFromBigscreenUrl(sourceUri.AbsoluteUri, out currentEmbed))
                    {
                        MediaPopoutTarget currentTarget = await BuildMediaPopoutTargetAsync(currentEmbed);
                        if (currentTarget != null)
                        {
                            return currentTarget;
                        }
                    }
                }
            }

            BigscreenEmbedState selectedEmbed = GetSelectedBigscreenEmbed();
            if (selectedEmbed == null)
            {
                return null;
            }

            return await BuildMediaPopoutTargetAsync(selectedEmbed);
        }

        private string GetMediaRestoreUrl()
        {
            if (_browserReady && _webView.Source != null)
            {
                Uri sourceUri = _webView.Source;
                if (sourceUri != null &&
                    IsAllowedUri(sourceUri) &&
                    sourceUri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase))
                {
                    return sourceUri.AbsoluteUri;
                }
            }

            BigscreenEmbedState selectedEmbed = GetSelectedBigscreenEmbed();
            if (selectedEmbed != null && !string.IsNullOrWhiteSpace(selectedEmbed.Url))
            {
                return selectedEmbed.Url;
            }

            return BigscreenBarUrl;
        }

        private void RestoreMediaToMainWindow(string restoreUrl)
        {
            if (IsDisposed || Disposing || string.IsNullOrWhiteSpace(restoreUrl))
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => RestoreMediaToMainWindow(restoreUrl)));
                return;
            }

            NavigateToUrl(restoreUrl);
            SetStatus("Returned the popped out media to the main window.");
        }

        private BigscreenEmbedState GetStreamChatEmbed()
        {
            if (!string.IsNullOrWhiteSpace(_preferredChatEmbedUrl) && _latestBigscreenEmbeds != null)
            {
                foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
                {
                    if (embed == null || string.IsNullOrWhiteSpace(embed.Url))
                    {
                        continue;
                    }

                    if (string.Equals(embed.Url.Trim(), _preferredChatEmbedUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        BigscreenEmbedState normalizedPreferred = NormalizeBigscreenEmbedState(embed);
                        if (normalizedPreferred != null &&
                            !string.IsNullOrWhiteSpace(BuildStreamChatUrl(normalizedPreferred, false)))
                        {
                            return normalizedPreferred;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_activeStreamPlayerUrl))
            {
                string trimmed = _activeStreamPlayerUrl.Trim();
                BigscreenEmbedState fromPlayer;
                if (TryCreateEmbedStateFromExternalStreamUrl(trimmed, out fromPlayer) ||
                    TryCreateEmbedStateFromBigscreenUrl(trimmed, out fromPlayer))
                {
                    fromPlayer = NormalizeBigscreenEmbedState(fromPlayer);
                    if (fromPlayer != null && !string.IsNullOrWhiteSpace(fromPlayer.Url))
                    {
                        if (!string.IsNullOrWhiteSpace(BuildStreamChatUrl(fromPlayer, false)))
                        {
                            return fromPlayer;
                        }
                    }
                }
            }

            return GetSelectedBigscreenEmbed();
        }

        private BigscreenEmbedState GetSelectedBigscreenEmbed()
        {
            Uri currentSource = _webView != null ? _webView.Source : null;
            if (currentSource != null && currentSource.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase))
            {
                BigscreenEmbedState sourceEmbed;
                if (TryCreateEmbedStateFromBigscreenUrl(currentSource.AbsoluteUri, out sourceEmbed))
                {
                    return sourceEmbed;
                }
            }

            if (_latestBigscreenEmbeds == null || _latestBigscreenEmbeds.Count == 0)
            {
                return null;
            }

            foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
            {
                if (embed != null && embed.IsSelected && !string.IsNullOrWhiteSpace(embed.Url))
                {
                    return embed;
                }
            }

            foreach (BigscreenEmbedState embed in _latestBigscreenEmbeds)
            {
                if (embed != null && !string.IsNullOrWhiteSpace(embed.Url))
                {
                    return embed;
                }
            }

            return null;
        }

        private async System.Threading.Tasks.Task<MediaPopoutTarget> BuildMediaPopoutTargetAsync(BigscreenEmbedState embed)
        {
            BigscreenEmbedState normalizedEmbed = NormalizeBigscreenEmbedState(embed);
            if (normalizedEmbed == null)
            {
                return null;
            }

            string platform = string.IsNullOrWhiteSpace(normalizedEmbed.Platform)
                ? string.Empty
                : normalizedEmbed.Platform.Trim().ToLowerInvariant();
            string mediaId = normalizedEmbed.MediaId ?? string.Empty;
            string playerUrl = null;

            switch (platform)
            {
                case "twitch":
                    playerUrl = BuildTwitchPlayerUrl("channel", mediaId);
                    break;
                case "twitch-vod":
                    playerUrl = BuildTwitchPlayerUrl("video", mediaId);
                    break;
                case "twitch-clip":
                    playerUrl = BuildTwitchClipUrl(mediaId);
                    break;
                case "youtube":
                    playerUrl = BuildYouTubePlayerUrl(mediaId);
                    break;
                case "kick":
                    playerUrl = BuildKickPlayerUrl(mediaId);
                    break;
                case "kick-vod":
                    playerUrl = await ResolveKickVodSourceAsync(mediaId);
                    if (!string.IsNullOrWhiteSpace(playerUrl))
                    {
                        return new MediaPopoutTarget
                        {
                            PlayerUrl = playerUrl,
                            UseHostedVideoElement = true
                        };
                    }

                    break;
                case "rumble":
                    playerUrl = BuildRumblePlayerUrl(mediaId);
                    break;
                case "vimeo":
                    playerUrl = BuildVimeoPlayerUrl(mediaId);
                    break;
                case "facebook":
                    playerUrl = BuildFacebookPlayerUrl(mediaId);
                    break;
                case "angelthump":
                    playerUrl = BuildAngelthumpPlayerUrl(mediaId);
                    break;
                default:
                    playerUrl = GetDirectExternalUrl(normalizedEmbed.Url);
                    break;
            }

            if (string.IsNullOrWhiteSpace(playerUrl))
            {
                playerUrl = GetDirectExternalUrl(normalizedEmbed.Url);
            }

            if (string.IsNullOrWhiteSpace(playerUrl))
            {
                return null;
            }

            return new MediaPopoutTarget
            {
                PlayerUrl = playerUrl,
                UseHostedVideoElement = false
            };
        }

        private static BigscreenEmbedState NormalizeBigscreenEmbedState(BigscreenEmbedState embed)
        {
            if (embed == null)
            {
                return null;
            }

            string platform = embed.Platform;
            string mediaId = embed.MediaId;

            if ((string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(mediaId)) &&
                !string.IsNullOrWhiteSpace(embed.Url))
            {
                BigscreenEmbedState parsedEmbed;
                if (TryCreateEmbedStateFromBigscreenUrl(embed.Url, out parsedEmbed))
                {
                    if (string.IsNullOrWhiteSpace(platform))
                    {
                        platform = parsedEmbed.Platform;
                    }

                    if (string.IsNullOrWhiteSpace(mediaId))
                    {
                        mediaId = parsedEmbed.MediaId;
                    }
                }
                else if (TryCreateEmbedStateFromExternalStreamUrl(embed.Url, out parsedEmbed))
                {
                    if (string.IsNullOrWhiteSpace(platform))
                    {
                        platform = parsedEmbed.Platform;
                    }

                    if (string.IsNullOrWhiteSpace(mediaId))
                    {
                        mediaId = parsedEmbed.MediaId;
                    }
                }
            }

            return new BigscreenEmbedState
            {
                Url = embed.Url,
                Platform = platform ?? string.Empty,
                MediaId = mediaId ?? string.Empty,
                DisplayText = embed.DisplayText ?? string.Empty,
                TooltipText = embed.TooltipText ?? string.Empty,
                IsSelected = embed.IsSelected
            };
        }

        private static bool TryCreateEmbedStateFromBigscreenUrl(string url, out BigscreenEmbedState embed)
        {
            embed = null;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }

            if (!uri.AbsolutePath.StartsWith("/bigscreen", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fragment = uri.Fragment;
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return false;
            }

            string route = Uri.UnescapeDataString(fragment.TrimStart('#').Trim());
            if (string.IsNullOrWhiteSpace(route))
            {
                return false;
            }

            route = route.TrimStart('/');
            string[] parts = route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            embed = new BigscreenEmbedState
            {
                Url = uri.AbsoluteUri,
                Platform = parts[0],
                MediaId = string.Join("/", parts, 1, parts.Length - 1),
                DisplayText = string.Empty,
                TooltipText = string.Empty,
                IsSelected = true
            };

            return true;
        }

        private static bool TryCreateEmbedStateFromExternalStreamUrl(string url, out BigscreenEmbedState embed)
        {
            embed = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }

            string host = (uri.Host ?? string.Empty).ToLowerInvariant();
            if (host.EndsWith("destiny.gg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] reservedKickSlugs =
            {
                "video", "categories", "search", "login", "signup", "settings", "download", "dashboard",
                "browse", "community-guidelines", "terms-of-service", "privacy-policy", "mobile", "api"
            };

            if (host.IndexOf("kick.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (host.StartsWith("player.", StringComparison.OrdinalIgnoreCase))
                {
                    string[] playerSegments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (playerSegments.Length >= 1)
                    {
                        string playerSlug = playerSegments[0];
                        for (int i = 0; i < reservedKickSlugs.Length; i++)
                        {
                            if (playerSlug.Equals(reservedKickSlugs[i], StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(playerSlug))
                        {
                            embed = new BigscreenEmbedState
                            {
                                Url = uri.AbsoluteUri,
                                Platform = "kick",
                                MediaId = playerSlug,
                                DisplayText = string.Empty,
                                TooltipText = string.Empty,
                                IsSelected = true
                            };
                            return true;
                        }
                    }

                    return false;
                }

                string[] segments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3 &&
                    segments[0].Equals("popout", StringComparison.OrdinalIgnoreCase) &&
                    segments[2].Equals("chat", StringComparison.OrdinalIgnoreCase))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "kick",
                        MediaId = segments[1],
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }

                if (segments.Length >= 1)
                {
                    string slug = segments[0];
                    for (int i = 0; i < reservedKickSlugs.Length; i++)
                    {
                        if (slug.Equals(reservedKickSlugs[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "kick",
                        MediaId = slug,
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }
            }

            if (host.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string[] segments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 1 && !string.IsNullOrWhiteSpace(segments[0]))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "youtube",
                        MediaId = segments[0],
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }
            }

            if (host.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                host.IndexOf("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string videoId = GetQueryParameter(uri.Query, "v");
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "youtube",
                        MediaId = videoId,
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }

                string[] segments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && segments[0].Equals("live", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(segments[1]))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "youtube",
                        MediaId = segments[1],
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }

                if (segments.Length >= 2 && segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(segments[1]))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "youtube",
                        MediaId = segments[1],
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }
            }

            if (host.IndexOf("twitch.tv", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (host.IndexOf("player.", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string channel = GetQueryParameter(uri.Query, "channel");
                    if (string.IsNullOrWhiteSpace(channel))
                    {
                        channel = GetQueryParameter(uri.Query, "stream");
                    }

                    if (!string.IsNullOrWhiteSpace(channel))
                    {
                        embed = new BigscreenEmbedState
                        {
                            Url = uri.AbsoluteUri,
                            Platform = "twitch",
                            MediaId = channel,
                            DisplayText = string.Empty,
                            TooltipText = string.Empty,
                            IsSelected = true
                        };
                        return true;
                    }
                }

                string[] segments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 1 && !segments[0].Equals("videos", StringComparison.OrdinalIgnoreCase) &&
                    !segments[0].Equals("directory", StringComparison.OrdinalIgnoreCase))
                {
                    embed = new BigscreenEmbedState
                    {
                        Url = uri.AbsoluteUri,
                        Platform = "twitch",
                        MediaId = segments[0],
                        DisplayText = string.Empty,
                        TooltipText = string.Empty,
                        IsSelected = true
                    };
                    return true;
                }
            }

            return false;
        }

        private static string GetDirectExternalUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return null;
            }

            if (uri.Host.EndsWith("destiny.gg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

        private static string BuildStreamChatUrl(BigscreenEmbedState embed, bool useEmbeddedHost)
        {
            BigscreenEmbedState normalizedEmbed = NormalizeBigscreenEmbedState(embed);
            if (normalizedEmbed == null)
            {
                return null;
            }

            string platform = string.IsNullOrWhiteSpace(normalizedEmbed.Platform)
                ? string.Empty
                : normalizedEmbed.Platform.Trim().ToLowerInvariant();

            switch (platform)
            {
                case "twitch":
                    return BuildTwitchChatUrl(normalizedEmbed.MediaId);
                case "youtube":
                    return BuildYouTubeChatUrl(normalizedEmbed.MediaId, useEmbeddedHost);
                case "kick":
                    if (!string.IsNullOrWhiteSpace(normalizedEmbed.Url))
                    {
                        string lowerUrl = normalizedEmbed.Url.ToLowerInvariant();
                        if (lowerUrl.IndexOf("kick.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            lowerUrl.IndexOf("/popout/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            lowerUrl.IndexOf("/chat", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return NormalizeKickStreamChatNavigationUrl(normalizedEmbed.Url);
                        }
                    }

                    return BuildKickChatUrl(normalizedEmbed.MediaId);
                default:
                    return null;
            }
        }

        private static string BuildTwitchChatUrl(string mediaId)
        {
            string channel = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            return "https://www.twitch.tv/embed/"
                + Uri.EscapeDataString(channel)
                + "/chat?parent="
                + Uri.EscapeDataString(DefaultTwitchParent)
                + "&darkpopout=1";
        }

        private static string BuildYouTubeChatUrl(string mediaId, bool useEmbeddedHost)
        {
            string videoId = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            if (useEmbeddedHost)
            {
                return "https://www.youtube.com/live_chat?v="
                    + Uri.EscapeDataString(videoId)
                    + "&embed_domain="
                    + Uri.EscapeDataString(DefaultTwitchParent);
            }

            return "https://www.youtube.com/live_chat?v="
                + Uri.EscapeDataString(videoId)
                + "&embed_domain="
                + Uri.EscapeDataString(DefaultTwitchParent)
                + "&dark_theme=1&is_popout=1";
        }

        private static string BuildKickChatUrl(string mediaId)
        {
            string channel = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            return "https://kick.com/popout/" + Uri.EscapeDataString(channel) + "/chat";
        }

        /// <summary>
        /// Kick serves popout chat reliably on apex kick.com; normalize www / redirects for WebView2 navigation.
        /// </summary>
        private static string NormalizeKickStreamChatNavigationUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            string trimmed = url.Trim();
            if (trimmed.IndexOf("kick.com", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return trimmed;
            }

            if (trimmed.IndexOf("/popout/", StringComparison.OrdinalIgnoreCase) < 0 ||
                trimmed.IndexOf("/chat", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return trimmed;
            }

            try
            {
                Uri uri = new Uri(trimmed);
                UriBuilder builder = new UriBuilder(uri)
                {
                    Scheme = Uri.UriSchemeHttps,
                    Host = "kick.com"
                };
                return builder.Uri.AbsoluteUri;
            }
            catch
            {
                return trimmed;
            }
        }

        private static string BuildTwitchPlayerUrl(string parameterName, string mediaId)
        {
            string value = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return "https://player.twitch.tv/?"
                + parameterName
                + "="
                + Uri.EscapeDataString(value)
                + "&parent="
                + Uri.EscapeDataString(DefaultTwitchParent)
                + "&autoplay=true";
        }

        private static string BuildTwitchClipUrl(string mediaId)
        {
            string clipId = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(clipId))
            {
                return null;
            }

            return "https://clips.twitch.tv/embed?clip="
                + Uri.EscapeDataString(clipId)
                + "&parent="
                + Uri.EscapeDataString(DefaultTwitchParent)
                + "&autoplay=true";
        }

        private static string BuildYouTubePlayerUrl(string mediaId)
        {
            Uri descriptor = CreateDescriptorUri(mediaId);
            if (descriptor == null)
            {
                return null;
            }

            string[] parts = descriptor.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            StringBuilder url = new StringBuilder("https://www.youtube.com/embed/");
            url.Append(Uri.EscapeDataString(parts[0]));
            url.Append("?autoplay=1&playsinline=1");

            int startSeconds;
            if (TryGetQueryInteger(descriptor.Query, "start", out startSeconds) ||
                TryGetQueryInteger(descriptor.Query, "t", out startSeconds))
            {
                url.Append("&start=");
                url.Append(startSeconds);
            }

            return url.ToString();
        }

        private static string BuildKickPlayerUrl(string mediaId)
        {
            string channel = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            return "https://player.kick.com/" + Uri.EscapeDataString(channel) + "?autoplay=true&muted=true";
        }

        private static string BuildRumblePlayerUrl(string mediaId)
        {
            string rumbleId = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(rumbleId))
            {
                return null;
            }

            return "https://rumble.com/embed/" + Uri.EscapeDataString(rumbleId);
        }

        private static string BuildVimeoPlayerUrl(string mediaId)
        {
            string vimeoId = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(vimeoId))
            {
                return null;
            }

            return "https://player.vimeo.com/video/" + Uri.EscapeDataString(vimeoId) + "?autoplay=1";
        }

        private static string BuildFacebookPlayerUrl(string mediaId)
        {
            string facebookId = mediaId == null ? string.Empty : mediaId.Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(facebookId))
            {
                return null;
            }

            return "https://www.facebook.com/plugins/video.php?href=https://www.facebook.com/"
                + Uri.EscapeDataString(facebookId)
                + "&width=1920&height=1080&autoplay=true";
        }

        private static string BuildAngelthumpPlayerUrl(string mediaId)
        {
            string channel = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            return "https://player.angelthump.com/?channel=" + Uri.EscapeDataString(channel);
        }

        private async System.Threading.Tasks.Task<string> ResolveKickVodSourceAsync(string mediaId)
        {
            Uri descriptor = CreateDescriptorUri(mediaId);
            if (descriptor == null)
            {
                return null;
            }

            string[] parts = descriptor.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            string requestUrl = _config.SiteUrl.TrimEnd('/')
                + "/api/kick/vod/"
                + Uri.EscapeDataString(parts[0])
                + "/"
                + Uri.EscapeDataString(parts[1]);

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.UserAgent = "DestinyChatDesktop";

                using (WebResponse response = await request.GetResponseAsync())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string json = await reader.ReadToEndAsync();
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    object payload = serializer.DeserializeObject(json);
                    Dictionary<string, object> root = payload as Dictionary<string, object>;
                    if (root == null || !root.ContainsKey("data"))
                    {
                        return null;
                    }

                    Dictionary<string, object> data = root["data"] as Dictionary<string, object>;
                    if (data == null || !data.ContainsKey("source"))
                    {
                        return null;
                    }

                    return Convert.ToString(data["source"]);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Uri CreateDescriptorUri(string mediaId)
        {
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                return null;
            }

            try
            {
                return new Uri("https://descriptor.invalid/" + mediaId.Trim().TrimStart('/'));
            }
            catch
            {
                return null;
            }
        }

        private static string GetPrimaryMediaPathSegment(string mediaId)
        {
            Uri descriptor = CreateDescriptorUri(mediaId);
            if (descriptor == null)
            {
                return null;
            }

            string[] parts = descriptor.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        private static bool TryGetQueryInteger(string query, string name, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string trimmedQuery = query.TrimStart('?');
            string[] pairs = trimmedQuery.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 0)
                {
                    continue;
                }

                string key = Uri.UnescapeDataString(parts[0]);
                if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rawValue = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                return int.TryParse(rawValue, out value);
            }

            return false;
        }

        private void NavigateFromAddressBar()
        {
            Uri uri;
            if (!TryBuildUri(_addressBox.Text, out uri))
            {
                SetStatus("That address is not valid.");
                return;
            }

            if (IsAllowedUri(uri))
            {
                NavigateToUrl(uri.AbsoluteUri);
                return;
            }

            OpenInDefaultBrowser(uri.AbsoluteUri);
            SetStatus("Opened outside the app because the URL is not on Destiny.gg.");
        }

        private void NavigateToUrl(string url)
        {
            Uri uri;
            if (!TryBuildUri(url, out uri))
            {
                SetStatus("Unable to navigate: invalid URL.");
                return;
            }

            if (!IsAllowedUri(uri))
            {
                OpenInDefaultBrowser(uri.AbsoluteUri);
                SetStatus("Opened outside the app because the URL is not on Destiny.gg.");
                return;
            }

            if (ShouldResetChatLayoutForNavigation(uri))
            {
                ResetChatLayoutForModeSwitch();
            }

            _pendingNavigation = uri.AbsoluteUri;
            UpdateAddressBox(_pendingNavigation);
            UpdateBigscreenBarVisibility();

            if (_browserReady && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Navigate(_pendingNavigation);
            }
        }

        private bool ShouldResetChatLayoutForNavigation(Uri targetUri)
        {
            if (targetUri == null || _webView == null || _webView.Source == null)
            {
                return false;
            }

            Uri currentUri = _webView.Source;
            bool currentChat = IsChatUri(currentUri);
            bool currentBigscreen = IsBigscreenUri(currentUri);
            bool targetChat = IsChatUri(targetUri);
            bool targetBigscreen = IsBigscreenUri(targetUri);

            return (currentChat && targetBigscreen) || (currentBigscreen && targetChat);
        }

        private bool TryBuildUri(string input, out Uri uri)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();

            if (trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                Uri baseUri;
                if (!Uri.TryCreate(_config.HomeUrl, UriKind.Absolute, out baseUri))
                {
                    return false;
                }

                return Uri.TryCreate(baseUri, trimmed, out uri);
            }

            if (trimmed.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                trimmed = "https://" + trimmed;
            }

            return Uri.TryCreate(trimmed, UriKind.Absolute, out uri);
        }

        private bool IsAllowedUri(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_config.AllowedHostSuffixes == null)
            {
                return false;
            }

            string host = uri.Host;
            foreach (string suffix in _config.AllowedHostSuffixes)
            {
                if (string.IsNullOrWhiteSpace(suffix))
                {
                    continue;
                }

                if (host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void OpenInDefaultBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N3",
                    "DestinyChatDesktop.cs:6366",
                    "Open in browser failed",
                    new Dictionary<string, object>
                    {
                        { "url", url ?? string.Empty },
                        { "error", ex.Message }
                    });
                // #endregion
                SetStatus("Could not open the browser: " + ex.Message);
            }
        }

        private static string GetPlatformBadge(string platform)
        {
            string normalized = string.IsNullOrWhiteSpace(platform)
                ? string.Empty
                : platform.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "youtube":
                    return "YT";
                case "twitch":
                case "twitch-vod":
                case "twitch-clip":
                    return "TW";
                case "kick":
                case "kick-vod":
                    return "K";
                case "rumble":
                    return "R";
                case "facebook":
                    return "FB";
                case "vimeo":
                    return "V";
                case "angelthump":
                    return "A";
                default:
                    return FormatPlatformLabel(platform);
            }
        }

        private static string FormatPlatformLabel(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                return "Live";
            }

            string normalized = platform.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "youtube":
                    return "YouTube";
                case "twitch":
                case "twitch-vod":
                case "twitch-clip":
                    return "Twitch";
                case "kick":
                case "kick-vod":
                    return "Kick";
                case "rumble":
                    return "Rumble";
                case "facebook":
                    return "Facebook";
                case "vimeo":
                    return "Vimeo";
                case "angelthump":
                    return "AngelThump";
                default:
                    return platform.Trim();
            }
        }

        private static Color GetPlatformColor(string platform)
        {
            string normalized = string.IsNullOrWhiteSpace(platform)
                ? string.Empty
                : platform.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "youtube":
                    return Color.FromArgb(204, 39, 39);
                case "twitch":
                case "twitch-vod":
                case "twitch-clip":
                    return Color.FromArgb(130, 92, 235);
                case "kick":
                case "kick-vod":
                    return Color.FromArgb(60, 202, 96);
                case "rumble":
                    return Color.FromArgb(58, 176, 110);
                case "facebook":
                    return Color.FromArgb(70, 117, 211);
                case "vimeo":
                    return Color.FromArgb(72, 178, 230);
                case "angelthump":
                    return Color.FromArgb(214, 171, 86);
                default:
                    return Color.FromArgb(92, 96, 104);
            }
        }

        private string BuildBigscreenEmbedsHtml(List<BigscreenEmbedState> embeds)
        {
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
            html.Append("<style>");
            html.Append("html,body{margin:0;padding:0;background:#111113;overflow:hidden;color:#43484e;font:400 .88rem/1.25rem Inter,system-ui,sans-serif;}");
            html.Append(".top-embeds-list{display:flex;gap:.25rem;z-index:6;background:#111113;padding:.25rem .5rem .35rem .5rem;overflow-x:auto;overflow-y:hidden;scrollbar-width:none;text-shadow:1px 1px 0 #000;-ms-overflow-style:none;white-space:nowrap;box-sizing:border-box;}");
            html.Append(".top-embeds-list::-webkit-scrollbar{display:none}");
            html.Append(".top-embeds-list__empty{flex-basis:100%;color:#afb3ba;font:400 .88rem/1.25rem Inter,system-ui,sans-serif;text-align:left;padding:.1rem .25rem;}");
            html.Append(".embed-link{display:flex;align-items:center;flex:0 0 auto;border-radius:.5rem;padding:.125rem .5rem;color:#43484e;text-decoration:none;}");
            html.Append(".embed-link__text{max-width:10rem;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}");
            html.Append(".embed-link .icon{fill:#43484e;margin-right:.25rem;height:.875rem;width:auto;flex:0 0 auto;}");
            html.Append(".embed-link .angelthump-icon{margin-right:.25rem;font-weight:bold;font-size:1.125rem;}");
            html.Append(".embed-link .fw{width:1.25em;text-align:center}");
            html.Append(".embed-link [data-icon=twitch]{margin-top:.125rem}");
            html.Append(".embed-link--selected{color:#edeef0}");
            html.Append(".embed-link--selected .icon{fill:#edeef0}");
            html.Append(".embed-link:hover{background:rgba(0,0,0,.6980392157);color:#edeef0}");
            html.Append(".embed-link:hover .icon{fill:#edeef0}");
            html.Append("</style></head><body>");
            html.Append("<div id=\"list\" class=\"top-embeds-list\">");

            if (embeds != null && embeds.Count > 0)
            {
                foreach (BigscreenEmbedState embed in embeds)
                {
                    string url = WebUtility.HtmlEncode(embed.Url ?? string.Empty);
                    string title = WebUtility.HtmlEncode(embed.TooltipText ?? string.Empty);
                    string text = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(embed.DisplayText) ? FormatPlatformLabel(embed.Platform) : embed.DisplayText.Trim());
                    string selectedClass = embed.IsSelected ? " embed-link--selected" : string.Empty;

                    html.Append("<a class=\"embed-link");
                    html.Append(selectedClass);
                    html.Append("\" href=\"#\" data-url=\"");
                    html.Append(url);
                    html.Append("\" data-platform=\"");
                    html.Append(WebUtility.HtmlEncode(embed.Platform ?? string.Empty));
                    html.Append("\" data-mediaid=\"");
                    html.Append(WebUtility.HtmlEncode(embed.MediaId ?? string.Empty));
                    html.Append("\" title=\"");
                    html.Append(title);
                    html.Append("\">");
                    html.Append(GetPlatformIconMarkup(embed.Platform));
                    html.Append("<span class=\"embed-link__text\">");
                    html.Append(text);
                    html.Append("</span></a>");
                }
            }
            else
            {
                html.Append("<p class=\"top-embeds-list__empty\">No Embeds</p>");
            }

            html.Append("</div>");
            html.Append("<script>");
            html.Append("const host=window.chrome&&window.chrome.webview;");
            html.Append("const list=document.getElementById('list');");
            html.Append("if(list){list.addEventListener('wheel',function(e){if(e.deltaX===0){e.preventDefault();list.scrollBy({left:e.deltaY>0?40:-40,behavior:'auto'});}}, {passive:false});}");
            html.Append("document.addEventListener('click',function(e){const link=e.target.closest('a.embed-link');if(!link){return;}e.preventDefault();if(host){host.postMessage({type:'openEmbed',url:link.dataset.url||'',platform:link.dataset.platform||'',mediaId:link.dataset.mediaid||''});}});");
            html.Append("</script></body></html>");
            return html.ToString();
        }

        private static string GetPlatformIconMarkup(string platform)
        {
            string normalized = string.IsNullOrWhiteSpace(platform)
                ? string.Empty
                : platform.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "twitch":
                case "twitch-vod":
                case "twitch-clip":
                    return "<svg class=\"icon lucide\" data-icon=\"twitch\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M11.571 4.714h1.715v5.143H11.57zm4.715 0H18v5.143h-1.714zM6 0L1.714 4.286v15.428h5.143V24l4.286-4.286h3.428L22.286 12V0zm14.571 11.143l-3.428 3.428h-3.429l-3 3v-3H6.857V1.714h13.714Z\"/></svg>";
                case "youtube":
                    return "<svg class=\"icon lucide\" data-icon=\"youtube\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z\"/></svg>";
                case "kick":
                case "kick-vod":
                    return "<svg class=\"icon lucide\" data-icon=\"kick\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M1.333 0h8v5.333H12V2.667h2.667V0h8v8H20v2.667h-2.667v2.666H20V16h2.667v8h-8v-2.667H12v-2.666H9.333V24h-8Z\"/></svg>";
                case "rumble":
                    return "<svg class=\"icon lucide\" data-icon=\"rumble\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M14.4528 13.5458c.8064-.6542.9297-1.8381.2756-2.6445a1.8802 1.8802 0 0 0-.2756-.2756 21.2127 21.2127 0 0 0-4.3121-2.776c-1.066-.51-2.256.2-2.4261 1.414a23.5226 23.5226 0 0 0-.14 5.5021c.116 1.23 1.292 1.964 2.372 1.492a19.6285 19.6285 0 0 0 4.5062-2.704v-.008zm6.9322-5.4002c2.0335 2.228 2.0396 5.637.014 7.8723A26.1487 26.1487 0 0 1 8.2946 23.846c-2.6848.6713-5.4168-.914-6.1662-3.5781-1.524-5.2002-1.3-11.0803.17-16.3045.772-2.744 3.3521-4.4661 6.0102-3.832 4.9242 1.174 9.5443 4.196 13.0764 8.0121v.002z\"/></svg>";
                case "facebook":
                    return "<svg class=\"icon lucide\" data-icon=\"facebook\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M9.101 23.691v-7.98H6.627v-3.667h2.474v-1.58c0-4.085 1.848-5.978 5.858-5.978.401 0 .955.042 1.468.103a8.68 8.68 0 0 1 1.141.195v3.325a8.623 8.623 0 0 0-.653-.036 26.805 26.805 0 0 0-.733-.009c-.707 0-1.259.096-1.675.309a1.686 1.686 0 0 0-.679.622c-.258.42-.374.995-.374 1.752v1.297h3.919l-.386 2.103-.287 1.564h-3.246v8.245C19.396 23.238 24 18.179 24 12.044c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.628 3.874 10.35 9.101 11.647Z\"/></svg>";
                case "vimeo":
                    return "<svg class=\"icon lucide\" data-icon=\"vimeo\" role=\"img\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M23.9765 6.4168c-.105 2.338-1.739 5.5429-4.894 9.6088-3.2679 4.247-6.0258 6.3699-8.2898 6.3699-1.409 0-2.578-1.294-3.553-3.881l-1.9179-7.1138c-.719-2.584-1.488-3.878-2.312-3.878-.179 0-.806.378-1.8809 1.132l-1.129-1.457a315.06 315.06 0 0 0 3.501-3.1279c1.579-1.368 2.765-2.085 3.5539-2.159 1.867-.18 3.016 1.1 3.447 3.838.465 2.953.789 4.789.971 5.5069.5389 2.45 1.1309 3.674 1.7759 3.674.502 0 1.256-.796 2.265-2.385 1.004-1.589 1.54-2.797 1.612-3.628.144-1.371-.395-2.061-1.614-2.061-.574 0-1.167.121-1.777.391 1.186-3.8679 3.434-5.7568 6.7619-5.6368 2.4729.06 3.6279 1.664 3.4929 4.7969z\"/></svg>";
                case "angelthump":
                    return "<span class=\"angelthump-icon\">A</span>";
                default:
                    return "<span class=\"fw\"></span>";
            }
        }

        private static string TruncateText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() + "…";
        }

        private void OnEmbedLinkClicked(object sender, EventArgs e)
        {
            Control control = sender as Control;
            string url = control != null ? control.Tag as string : null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            Uri uri;
            if (TryBuildUri(url, out uri) && IsAllowedUri(uri))
            {
                NavigateToUrl(uri.AbsoluteUri);
                return;
            }

            OpenInDefaultBrowser(url);
        }

        private void OnSessionPersistTimerTick(object sender, EventArgs e)
        {
            PersistBrowserSession();
        }

        private async void PersistBrowserSession()
        {
            await SaveCookiesAsync();
        }

        private void RestoreCookies()
        {
            if (!_browserReady || _webView.CoreWebView2 == null || !File.Exists(_cookiePath))
            {
                return;
            }

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<CookieState> cookieStates = serializer.Deserialize<List<CookieState>>(File.ReadAllText(_cookiePath));
                if (cookieStates == null)
                {
                    return;
                }

                foreach (CookieState cookieState in cookieStates)
                {
                    if (cookieState == null || string.IsNullOrWhiteSpace(cookieState.Name) || string.IsNullOrWhiteSpace(cookieState.Domain))
                    {
                        continue;
                    }

                    CoreWebView2Cookie cookie = _webView.CoreWebView2.CookieManager.CreateCookie(
                        cookieState.Name,
                        cookieState.Value ?? string.Empty,
                        cookieState.Domain,
                        string.IsNullOrWhiteSpace(cookieState.Path) ? "/" : cookieState.Path);

                    cookie.IsHttpOnly = cookieState.IsHttpOnly;
                    cookie.IsSecure = cookieState.IsSecure;

                    CoreWebView2CookieSameSiteKind sameSiteKind;
                    if (!string.IsNullOrWhiteSpace(cookieState.SameSite) &&
                        Enum.TryParse(cookieState.SameSite, out sameSiteKind))
                    {
                        cookie.SameSite = sameSiteKind;
                    }

                    if (!cookieState.IsSession && cookieState.ExpiresUtcTicks > 0)
                    {
                        cookie.Expires = new DateTime(cookieState.ExpiresUtcTicks, DateTimeKind.Utc);
                    }

                    _webView.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N3",
                    "DestinyChatDesktop.cs:6639",
                    "Restore cookies failed",
                    new Dictionary<string, object>
                    {
                        { "cookiePath", _cookiePath ?? string.Empty },
                        { "error", ex.Message }
                    });
                // #endregion
                // Ignore bad persisted cookie data and continue with a clean browser session.
            }
        }

        private async System.Threading.Tasks.Task SaveCookiesAsync()
        {
            if (!_browserReady || _webView.CoreWebView2 == null || _isSavingCookies)
            {
                return;
            }

            try
            {
                _isSavingCookies = true;
                List<CookieState> cookieStates = new List<CookieState>();
                List<CoreWebView2Cookie> cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(_config.SiteUrl);

                foreach (CoreWebView2Cookie cookie in cookies)
                {
                    if (cookie == null || string.IsNullOrWhiteSpace(cookie.Name))
                    {
                        continue;
                    }

                    cookieStates.Add(new CookieState
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        IsHttpOnly = cookie.IsHttpOnly,
                        IsSecure = cookie.IsSecure,
                        IsSession = cookie.IsSession,
                        ExpiresUtcTicks = cookie.IsSession ? 0L : cookie.Expires.ToUniversalTime().Ticks,
                        SameSite = cookie.SameSite.ToString()
                    });
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(_cookiePath, serializer.Serialize(cookieStates));
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N3",
                    "DestinyChatDesktop.cs:6682",
                    "Save cookies failed",
                    new Dictionary<string, object>
                    {
                        { "cookiePath", _cookiePath ?? string.Empty },
                        { "error", ex.Message }
                    });
                // #endregion
                // Cookie persistence is best-effort; failure should not block app shutdown.
            }
            finally
            {
                _isSavingCookies = false;
            }
        }

        private void AdjustZoom(double delta)
        {
            double currentZoom = _browserReady ? _webView.ZoomFactor : _state.ZoomFactor;
            double nextZoom = ClampZoom(currentZoom + delta);

            if (_browserReady)
            {
                _webView.ZoomFactor = nextZoom;
            }

            UpdateZoomLabel(nextZoom);
            LayoutDualChatPanel();
            SetStatus("Zoom set to " + (int)Math.Round(nextZoom * 100.0) + "%.");
        }

        private void ResetZoom()
        {
            if (_browserReady)
            {
                _webView.ZoomFactor = 1.0;
            }

            UpdateZoomLabel(1.0);
            LayoutDualChatPanel();
            SetStatus("Zoom reset.");
        }

        private void UpdateZoomLabel(double zoomFactor)
        {
            _zoomResetButton.Text = ((int)Math.Round(ClampZoom(zoomFactor) * 100.0)).ToString() + "%";
        }

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                FormBorderStyle = _savedBorderStyle;
                WindowState = _savedWindowState;
                if (_savedWindowState == FormWindowState.Normal)
                {
                    Bounds = _savedBounds;
                }

                _toolStrip.Visible = true;
                _isFullScreen = false;
                LayoutDualChatPanel();
                SetStatus("Exited fullscreen.");
                return;
            }

            _savedBorderStyle = FormBorderStyle;
            _savedWindowState = WindowState;
            _savedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;

            _toolStrip.Visible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _isFullScreen = true;
            LayoutDualChatPanel();
            SetStatus("Fullscreen enabled.");
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _sessionPersistTimer.Stop();
                _sessionPersistTimer.Dispose();

                Rectangle bounds = WindowState == FormWindowState.Normal ? DesktopBounds : RestoreBounds;
                _state.X = bounds.X;
                _state.Y = bounds.Y;
                _state.Width = bounds.Width;
                _state.Height = bounds.Height;
                _state.IsMaximized = WindowState == FormWindowState.Maximized;
                _state.AlwaysOnTop = _pinButton.Checked;
                _state.Muted = _muteButton.Checked;
                _state.LastUrl = _browserReady && _webView.Source != null ? _webView.Source.AbsoluteUri : _pendingNavigation;
                _state.ZoomFactor = _browserReady ? ClampZoom(_webView.ZoomFactor) : ClampZoom(_state.ZoomFactor);
                _state.LoadedFromDisk = true;
                _state.Save(_statePath);
            }
            catch
            {
                // Best effort only; state persistence should not block app exit.
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static void SendSnipHotkey()
        {
            // Win+Shift+S — triggers system snip overlay on Windows 10/11
            const byte VK_LWIN  = 0x5B;
            const byte VK_SHIFT = 0x10;
            const byte VK_S     = 0x53;
            const uint KEYUP    = 0x0002;
            keybd_event(VK_LWIN,  0, 0,    UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, 0,    UIntPtr.Zero);
            keybd_event(VK_S,     0, 0,    UIntPtr.Zero);
            keybd_event(VK_S,     0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_SHIFT, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN,  0, KEYUP, UIntPtr.Zero);
        }

        private System.Threading.Tasks.Task RegisterBigscreenChatLayoutAsync()
        {
            if (_webView.CoreWebView2 == null)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            // Injected on every page — only fires on /bigscreen URLs.
            // Detects where the right-side chat area begins vertically and reports it so
            // LayoutDualChatPanel() can start the overlay below the video embeds.
            return _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    if (window.location.pathname.indexOf('/bigscreen') < 0) return;

                    const host = window.chrome && window.chrome.webview;
                    if (!host || typeof host.postMessage !== 'function') return;
                    let lastEmbeddedStreamChatEnabled = null;
                    let observedEnabledStreamChatOnce = false;

                    const CHAT_TOP_SELECTORS = [
                        '#chat-output-frame',
                        '#chat-wrap',
                        '#chat',
                        '.chat-wrap',
                        '.chat',
                        '#sidebar',
                        '.sidebar'
                    ];

                    const INPUT_SELECTORS = [
                        '#chat-input-control',
                        '#chat-input-wrap',
                        '#chat-input-frame',
                        '#chat-tools-wrap',
                        '.chat-tools-group'
                    ];

                    function getTopFromDocument(doc, selectors, topPadding) {
                        if (!doc) {
                            return 0;
                        }

                        let top = 0;
                        for (let i = 0; i < selectors.length; i++) {
                            const matches = Array.from(doc.querySelectorAll(selectors[i]));
                            for (let j = 0; j < matches.length; j++) {
                                const el = matches[j];
                                if (!el || typeof el.getBoundingClientRect !== 'function') {
                                    continue;
                                }

                                const rect = el.getBoundingClientRect();
                                if (rect.height > 2 && rect.width > 2) {
                                    if (top <= 0 || rect.top < top) {
                                        top = rect.top;
                                    }
                                }
                            }
                        }

                        return top > 0 ? Math.max(0, Math.round(top - (topPadding || 0))) : 0;
                    }

                    function getTopFromSelectors(selectors, topPadding) {
                        return getTopFromDocument(document, selectors, topPadding);
                    }

                    function getEmbeddedChatInputTop() {
                        const frames = Array.from(document.querySelectorAll('iframe'));
                        for (let i = 0; i < frames.length; i++) {
                            const frame = frames[i];
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            if (src.indexOf('/embed/chat') < 0) {
                                continue;
                            }

                            let doc = null;
                            try {
                                doc = frame.contentDocument;
                            } catch (error) {
                                doc = null;
                            }

                            if (!doc) {
                                continue;
                            }

                            const innerTop = getTopFromDocument(doc, INPUT_SELECTORS, 10);
                            if (innerTop <= 0) {
                                continue;
                            }

                            const frameRect = frame.getBoundingClientRect();
                            return Math.max(0, Math.round(frameRect.top + innerTop));
                        }

                        return 0;
                    }

                    function getEmbeddedChatFrameTop() {
                        const frames = Array.from(document.querySelectorAll('iframe'));
                        for (let i = 0; i < frames.length; i++) {
                            const frame = frames[i];
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            if (src.indexOf('/embed/chat') < 0) {
                                continue;
                            }

                            const rect = frame.getBoundingClientRect();
                            if (rect.height > 20 && rect.width > 20) {
                                return Math.max(0, Math.round(rect.top));
                            }
                        }

                        return 0;
                    }

                    function getChatInputTop() {
                        const directTop = getTopFromSelectors(INPUT_SELECTORS, 10);
                        const embeddedTop = getEmbeddedChatInputTop();
                        if (directTop > 0 && embeddedTop > 0) {
                            return Math.min(directTop, embeddedTop);
                        }

                        return Math.max(directTop, embeddedTop);
                    }

                    function getMediaBottom() {
                        const candidates = [];
                        const iframeCandidates = Array.from(document.querySelectorAll('iframe'));
                        for (let i = 0; i < iframeCandidates.length; i++) {
                            const frame = iframeCandidates[i];
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            if (!src) {
                                continue;
                            }
                            if (src.indexOf('player.kick.com') >= 0 ||
                                src.indexOf('kick.com/embed') >= 0 ||
                                src.indexOf('youtube.com/embed') >= 0 ||
                                src.indexOf('twitch.tv') >= 0) {
                                candidates.push(frame);
                            }
                        }

                        const video = document.querySelector('video') || document.querySelector('.video-js video');
                        if (video) {
                            candidates.push(video);
                        }

                        let bottom = 0;
                        for (let i = 0; i < candidates.length; i++) {
                            const el = candidates[i];
                            if (!el || typeof el.getBoundingClientRect !== 'function') {
                                continue;
                            }
                            const rect = el.getBoundingClientRect();
                            if (rect.width < 40 || rect.height < 40) {
                                continue;
                            }
                            bottom = Math.max(bottom, Math.round(rect.bottom));
                        }

                        return Math.max(0, bottom);
                    }

                    function getMediaBounds() {
                        const candidates = [];
                        const iframeCandidates = Array.from(document.querySelectorAll('iframe'));
                        for (let i = 0; i < iframeCandidates.length; i++) {
                            const frame = iframeCandidates[i];
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            if (!src) {
                                continue;
                            }
                            if (src.indexOf('player.kick.com') >= 0 ||
                                src.indexOf('kick.com/embed') >= 0 ||
                                src.indexOf('youtube.com/embed') >= 0 ||
                                src.indexOf('twitch.tv') >= 0) {
                                candidates.push(frame);
                            }
                        }

                        const video = document.querySelector('video') || document.querySelector('.video-js video');
                        if (video) {
                            candidates.push(video);
                        }

                        let bounds = null;
                        for (let i = 0; i < candidates.length; i++) {
                            const el = candidates[i];
                            if (!el || typeof el.getBoundingClientRect !== 'function') {
                                continue;
                            }
                            const rect = el.getBoundingClientRect();
                            if (rect.width < 40 || rect.height < 40) {
                                continue;
                            }

                            if (!bounds) {
                                bounds = {
                                    top: Math.round(rect.top),
                                    left: Math.round(rect.left),
                                    right: Math.round(rect.right),
                                    bottom: Math.round(rect.bottom)
                                };
                            } else {
                                bounds.top = Math.min(bounds.top, Math.round(rect.top));
                                bounds.left = Math.min(bounds.left, Math.round(rect.left));
                                bounds.right = Math.max(bounds.right, Math.round(rect.right));
                                bounds.bottom = Math.max(bounds.bottom, Math.round(rect.bottom));
                            }
                        }

                        return bounds;
                    }

                    function reportLayout() {
                        const selectorTop = getTopFromSelectors(CHAT_TOP_SELECTORS);
                        const embeddedChatTop = getEmbeddedChatFrameTop();
                        const mediaBottom = getMediaBottom();
                        const chatTop = embeddedChatTop > 0
                            ? embeddedChatTop
                            : (selectorTop > 0 ? selectorTop : mediaBottom);
                        const inputTop = getChatInputTop();
                        host.postMessage({
                            type: 'codex-bigscreen-layout',
                            chatTop: chatTop,
                            inputTop: inputTop,
                            windowWidth: Math.round(window.innerWidth || 0),
                            windowHeight: Math.round(window.innerHeight || 0)
                        });
                    }

                    function reportDebugLayout() {
                        const selectors = [
                            '#chat-output-frame',
                            '#chat-wrap',
                            '#chat',
                            '.chat-wrap',
                            '.chat',
                            '#sidebar',
                            '.sidebar',
                            '#movie-content',
                            '.movie-content',
                            '#bigscreen-content',
                            '.bigscreen-content',
                            '#stream-info',
                            '.stream-info',
                            '.movierooms',
                            '.movierooms-panel',
                            '.movierooms-body',
                            '.movierooms-content',
                            '.video-js',
                            'video'
                        ];
                        const candidates = [];
                        for (let i = 0; i < selectors.length; i++) {
                            const selector = selectors[i];
                            const el = document.querySelector(selector);
                            if (!el) {
                                continue;
                            }

                            const rect = el.getBoundingClientRect();
                            candidates.push({
                                selector: selector,
                                top: Math.round(rect.top || 0),
                                left: Math.round(rect.left || 0),
                                width: Math.round(rect.width || 0),
                                height: Math.round(rect.height || 0)
                            });
                        }

                        host.postMessage({
                            type: 'codex-bigscreen-layout-debug',
                            bodyDualChatEnabled: !!document.body?.classList?.contains('codex-dual-chat-enabled'),
                            bodySplitChatEnabled: !!document.body?.classList?.contains('codex-split-chat-enabled'),
                            windowWidth: Math.round(window.innerWidth || 0),
                            windowHeight: Math.round(window.innerHeight || 0),
                            candidates: candidates
                        });
                    }

                    function reportMediaState() {
                        const video = document.querySelector('video') || document.querySelector('.video-js video');
                        const iframes = Array.from(document.querySelectorAll('iframe')).slice(0, 5).map(frame => ({
                            src: frame && frame.src ? frame.src : '',
                            id: frame && frame.id ? frame.id : '',
                            cls: frame && frame.className ? String(frame.className) : ''
                        }));
                        const payload = {
                            type: 'codex-bigscreen-media-state',
                            href: location.href || '',
                            hasVideo: !!video,
                            iframeCount: document.querySelectorAll('iframe').length,
                            iframePreview: iframes,
                            windowWidth: Math.round(window.innerWidth || 0),
                            windowHeight: Math.round(window.innerHeight || 0)
                        };

                        if (video) {
                            payload.paused = !!video.paused;
                            payload.ended = !!video.ended;
                            payload.muted = !!video.muted;
                            payload.readyState = Number(video.readyState || 0);
                            payload.networkState = Number(video.networkState || 0);
                            payload.currentTime = Number(video.currentTime || 0);
                            payload.errorCode = video.error && video.error.code ? Number(video.error.code) : 0;
                        }

                        host.postMessage(payload);
                    }

                    function hideBigscreenActionButtons() {
                        const setStyle = (el, prop, value) => {
                            if (el && el.style && el.style.getPropertyValue(prop) !== value) {
                                el.style.setProperty(prop, value);
                            }
                        };
                        const candidates = Array.from(document.querySelectorAll('button, a, [role=""button""]'));
                        let refreshEl = null;
                        let cinemaEl = null;
                        for (let i = 0; i < candidates.length; i++) {
                            const el = candidates[i];
                            if (!el || !el.textContent) {
                                continue;
                            }
                            const txt = String(el.textContent).trim().toLowerCase();
                            if (txt === 'refresh' || txt === 'cinema mode') {
                                setStyle(el, 'display', 'none');
                                if (txt === 'refresh') {
                                    refreshEl = el;
                                }
                                else if (txt === 'cinema mode') {
                                    cinemaEl = el;
                                }
                            }
                        }
                        if (refreshEl && cinemaEl) {
                            let parent = refreshEl.parentElement;
                            let depth = 0;
                            while (parent && depth < 6) {
                                if (parent.contains(cinemaEl)) {
                                    setStyle(parent, 'display', 'none');
                                    setStyle(parent, 'max-height', '0px');
                                    setStyle(parent, 'min-height', '0px');
                                    setStyle(parent, 'height', '0px');
                                    setStyle(parent, 'margin', '0');
                                    setStyle(parent, 'padding', '0');
                                    setStyle(parent, 'border', '0');
                                    setStyle(parent, 'overflow', 'hidden');
                                    break;
                                }
                                parent = parent.parentElement;
                                depth += 1;
                            }
                        }
                    }

                    function normalizeKickPlayerFrames() {
                        const frames = Array.from(document.querySelectorAll('iframe'));
                        for (let i = 0; i < frames.length; i++) {
                            const frame = frames[i];
                            const rawSrc = String(frame && (frame.getAttribute('src') || frame.src || '') || '');
                            const lowerSrc = rawSrc.toLowerCase();
                            if (lowerSrc.indexOf('player.kick.com') < 0) {
                                continue;
                            }

                            try {
                                const allowValue = 'autoplay; fullscreen; encrypted-media; picture-in-picture';
                                if (frame.getAttribute('allow') !== allowValue) {
                                    frame.setAttribute('allow', allowValue);
                                }
                                if (frame.getAttribute('allowfullscreen') !== 'true') {
                                    frame.setAttribute('allowfullscreen', 'true');
                                }
                                if (frame.getAttribute('scrolling') !== 'no') {
                                    frame.setAttribute('scrolling', 'no');
                                }
                            } catch (error) {
                            }

                            try {
                                const parsed = new URL(rawSrc, location.href);
                                let changed = false;
                                if ((parsed.searchParams.get('autoplay') || '').toLowerCase() !== 'true') {
                                    parsed.searchParams.set('autoplay', 'true');
                                    changed = true;
                                }
                                if ((parsed.searchParams.get('muted') || '').toLowerCase() !== 'true') {
                                    parsed.searchParams.set('muted', 'true');
                                    changed = true;
                                }

                                const nextSrc = parsed.toString();
                                if (changed && nextSrc !== rawSrc) {
                                    frame.setAttribute('src', nextSrc);
                                }
                            } catch (error) {
                            }
                        }
                    }

                    function syncBigscreenDualFromEmbeddedChat() {
                        const allFrames = Array.from(document.querySelectorAll('iframe'));
                        let chatFrame = null;
                        let kickChatFrame = null;
                        let kickPlayerFrame = null;
                        for (let i = 0; i < allFrames.length; i++) {
                            const frame = allFrames[i];
                            const src = String(frame && (frame.getAttribute('src') || frame.src || '')).toLowerCase();
                            if (!src) continue;
                            if (!chatFrame && src.indexOf('/embed/chat') >= 0) {
                                chatFrame = frame;
                            }
                            if (!kickPlayerFrame && src.indexOf('player.kick.com') >= 0) {
                                kickPlayerFrame = frame;
                            }
                            if (!kickChatFrame && src.indexOf('kick.com') >= 0 && src.indexOf('player.kick.com') < 0) {
                                if ((src.indexOf('/popout/') >= 0 && src.indexOf('/chat') >= 0) ||
                                    src.indexOf('widgets.chat') >= 0 ||
                                    src.indexOf('kick.com/chat') >= 0) {
                                    kickChatFrame = frame;
                                }
                            }
                        }

                        let streamChatEnabled = false;
                        let chatDoc = null;
                        let streamButtonFound = false;
                        if (chatFrame && chatFrame.contentDocument) {
                            try {
                                const doc = chatFrame.contentDocument;
                                chatDoc = doc;
                                const streamBtn = doc.getElementById('codex-stream-chat-btn') ||
                                    doc.getElementById('codex-steam-chat-btn');
                                streamButtonFound = !!streamBtn;
                                streamChatEnabled = !!(streamBtn && streamBtn.classList && streamBtn.classList.contains('codex-split-chat-active'));
                            } catch (error) {
                                streamChatEnabled = false;
                            }
                        }

                        if (!streamButtonFound && lastEmbeddedStreamChatEnabled !== null) {
                            streamChatEnabled = !!lastEmbeddedStreamChatEnabled;
                        }

                        if (streamButtonFound) {
                            const shouldRelay = lastEmbeddedStreamChatEnabled !== streamChatEnabled &&
                                (streamChatEnabled || observedEnabledStreamChatOnce);
                            lastEmbeddedStreamChatEnabled = streamChatEnabled;
                            if (streamChatEnabled) {
                                observedEnabledStreamChatOnce = true;
                            }
                            if (shouldRelay) {
                                try {
                                    host.postMessage({
                                        type: 'codex-bigscreen-dual-toggle',
                                        enabled: streamChatEnabled
                                    });
                                } catch (error) {
                                }
                            }
                        }

                        if (chatDoc && chatDoc.body) {
                            try {
                                const styleId = 'codex-desktop-external-chat-style';
                                let style = chatDoc.getElementById(styleId);
                                if (!style && chatDoc.head) {
                                    style = chatDoc.createElement('style');
                                    style.id = styleId;
                                    style.textContent =
                                        'body.codex-desktop-external-chat-active #chat-wrap,' +
                                        'body.codex-desktop-external-chat-active .chat-wrap{' +
                                        'width:calc((100% - 14px) / 2)!important;' +
                                        'max-width:calc((100% - 14px) / 2)!important;' +
                                        'box-sizing:border-box!important;' +
                                        'overflow:hidden!important;' +
                                        '}' +
                                        'body.codex-desktop-external-chat-active #chat-output-frame,' +
                                        'body.codex-desktop-external-chat-active .chat-output-frame{' +
                                        'width:100%!important;' +
                                        'max-width:100%!important;' +
                                        'box-sizing:border-box!important;' +
                                        '}' +
                                        'body.codex-desktop-external-chat-active #chat-input-frame,' +
                                        'body.codex-desktop-external-chat-active #chat-input-wrap,' +
                                        'body.codex-desktop-external-chat-active #chat-input-control,' +
                                        'body.codex-desktop-external-chat-active #chat-tools-wrap,' +
                                        'body.codex-desktop-external-chat-active .chat-tools-group{' +
                                        'width:calc((100% - 14px) / 2)!important;' +
                                        'max-width:calc((100% - 14px) / 2)!important;' +
                                        'box-sizing:border-box!important;' +
                                        '}';
                                    chatDoc.head.appendChild(style);
                                }
                                const rect = chatFrame.getBoundingClientRect();
                                const topWidth = Math.round(window.innerWidth || document.documentElement.clientWidth || 0);
                                const expectedHalf = topWidth > 0 ? Math.round((topWidth - 14) / 2) : 0;
                                const tolerance = Math.max(24, Math.round(topWidth * 0.04));
                                const needsInnerCollapse = streamChatEnabled &&
                                    (!expectedHalf || Math.round(rect.width || 0) > expectedHalf + tolerance);
                                chatDoc.body.classList.toggle('codex-desktop-external-chat-active', needsInnerCollapse);
                            } catch (error) {
                            }
                        }

                        document.body.classList.toggle('codex-bigscreen-dual-single-chat', !streamChatEnabled);

                        const setImportantStyle = (el, prop, value) => {
                            if (el.style.getPropertyValue(prop) !== value ||
                                el.style.getPropertyPriority(prop) !== 'important') {
                                el.style.setProperty(prop, value, 'important');
                            }
                        };

                        const removeInlineStyle = (el, prop) => {
                            if (el.style.getPropertyValue(prop)) {
                                el.style.removeProperty(prop);
                            }
                        };

                        const hideTarget = (el, hide) => {
                            if (!el) return;
                            if (hide) {
                                setImportantStyle(el, 'display', 'none');
                                setImportantStyle(el, 'width', '0');
                                setImportantStyle(el, 'min-width', '0');
                                setImportantStyle(el, 'max-width', '0');
                                setImportantStyle(el, 'margin', '0');
                                setImportantStyle(el, 'padding', '0');
                                setImportantStyle(el, 'border', '0');
                                setImportantStyle(el, 'overflow', 'hidden');
                                setImportantStyle(el, 'flex', '0 0 0');
                            } else {
                                removeInlineStyle(el, 'display');
                                removeInlineStyle(el, 'width');
                                removeInlineStyle(el, 'min-width');
                                removeInlineStyle(el, 'max-width');
                                removeInlineStyle(el, 'margin');
                                removeInlineStyle(el, 'padding');
                                removeInlineStyle(el, 'border');
                                removeInlineStyle(el, 'overflow');
                                removeInlineStyle(el, 'flex');
                            }
                        };

                        const fillTarget = (el, fill) => {
                            if (!el) return;
                            if (fill) {
                                setImportantStyle(el, 'width', '100%');
                                setImportantStyle(el, 'max-width', '100%');
                                setImportantStyle(el, 'min-width', '0');
                                setImportantStyle(el, 'flex', '1 1 auto');
                            } else {
                                removeInlineStyle(el, 'width');
                                removeInlineStyle(el, 'max-width');
                                removeInlineStyle(el, 'min-width');
                                removeInlineStyle(el, 'flex');
                            }
                        };

                        const kickChatTarget = kickChatFrame && kickChatFrame.parentElement ? kickChatFrame.parentElement : kickChatFrame;
                        const chatTarget = chatFrame && chatFrame.parentElement ? chatFrame.parentElement : chatFrame;
                        hideTarget(kickChatTarget, !streamChatEnabled);
                        fillTarget(chatTarget, !streamChatEnabled);
                    }

                    function ensureBigscreenChatClampStyle() {
                        if (document.getElementById('codex-bigscreen-chat-clamp-style') || !document.head) {
                            return;
                        }
                        const st = document.createElement('style');
                        st.id = 'codex-bigscreen-chat-clamp-style';
                        st.textContent = 'body.codex-bigscreen-chat-below-media #chat-wrap,' +
                            'body.codex-bigscreen-chat-below-media .chat-wrap{' +
                            'margin-top:var(--codex-chat-below-media-push,0px)!important;' +
                            'box-sizing:border-box!important;' +
                            '}';
                        document.head.appendChild(st);
                    }

                    function enforceBigscreenChatBelowMedia() {
                        const root = document.documentElement;
                        const mediaBounds = getMediaBounds();
                        if (!mediaBounds || mediaBounds.bottom <= 48) {
                            if (root.style.getPropertyValue('--codex-chat-below-media-push')) {
                                root.style.removeProperty('--codex-chat-below-media-push');
                            }
                            document.body.classList.remove('codex-bigscreen-chat-below-media');
                            return;
                        }
                        const wraps = [];
                        const w1 = document.querySelector('#chat-wrap');
                        const w2 = document.querySelector('.chat-wrap');
                        if (w1) wraps.push(w1);
                        if (w2 && w2 !== w1) wraps.push(w2);
                        for (let i = 0; i < wraps.length; i++) {
                            const rect = wraps[i].getBoundingClientRect();
                            if (rect.width > 40 && rect.height > 40 && rect.left >= mediaBounds.right - 16) {
                                if (root.style.getPropertyValue('--codex-chat-below-media-push')) {
                                    root.style.removeProperty('--codex-chat-below-media-push');
                                }
                                document.body.classList.remove('codex-bigscreen-chat-below-media');
                                return;
                            }
                        }

                        document.body.classList.add('codex-bigscreen-chat-below-media');
                        let maxPush = 0;
                        const currentPush = Math.max(
                            0,
                            parseFloat(root.style.getPropertyValue('--codex-chat-below-media-push')) || 0
                        );
                        const clearance = 12;
                        for (let i = 0; i < wraps.length; i++) {
                            const wrap = wraps[i];
                            const rect = wrap.getBoundingClientRect();
                            const naturalTop = Math.round(rect.top - currentPush);
                            const push = Math.max(0, Math.round(mediaBounds.bottom + clearance - naturalTop));
                            maxPush = Math.max(maxPush, push);
                        }
                        const nextPush = maxPush + 'px';
                        if (root.style.getPropertyValue('--codex-chat-below-media-push') !== nextPush) {
                            root.style.setProperty('--codex-chat-below-media-push', nextPush);
                        }
                    }

                    let scheduled = false;
                    function scheduleReport() {
                        if (scheduled) {
                            return;
                        }

                        scheduled = true;
                        window.requestAnimationFrame(() => {
                            scheduled = false;
                            hideBigscreenActionButtons();
                            normalizeKickPlayerFrames();
                            ensureBigscreenChatClampStyle();
                            enforceBigscreenChatBelowMedia();
                            syncBigscreenDualFromEmbeddedChat();
                            reportLayout();
                            reportDebugLayout();
                            reportMediaState();
                        });
                    }

                    window.addEventListener('resize', scheduleReport);
                    window.addEventListener('load', () => {
                        scheduleReport();
                        setTimeout(scheduleReport, 200);
                        setTimeout(scheduleReport, 800);
                    });
                    document.addEventListener('DOMContentLoaded', scheduleReport, { once: true });
                    window.setInterval(normalizeKickPlayerFrames, 2000);
                    window.setInterval(reportMediaState, 2000);
                    window.setInterval(hideBigscreenActionButtons, 1000);
                    window.setInterval(syncBigscreenDualFromEmbeddedChat, 500);
                    window.setInterval(() => {
                        ensureBigscreenChatClampStyle();
                        enforceBigscreenChatBelowMedia();
                    }, 400);

                    if (typeof MutationObserver === 'function') {
                        const observer = new MutationObserver(() => scheduleReport());
                        const startObserver = () => {
                            if (document.body) {
                                observer.observe(document.body, { childList: true, subtree: true });
                            }
                        };

                        startObserver();
                        document.addEventListener('DOMContentLoaded', startObserver);
                    }

                    scheduleReport();
                })();
            ");
        }
    }

    public sealed class MediaPopoutForm : Form
    {
        private const int WmNcHitTest = 0x0084;
        private const int WmSizing = 0x0214;
        private const int WmNclButtonDown = 0x00A1;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;
        private const int ResizeBorderThickness = 8;
        private const int DragBandHeight = 28;
        private const int WmszTopLeft = 4;
        private const int WmszTopRight = 5;
        private const int WmszBottomLeft = 7;
        private const int WmszBottomRight = 8;

        private readonly string _storageRoot;
        private readonly MediaPopoutTarget _target;
        private readonly WebView2 _webView;
        private readonly Button _closeButton;
        private readonly Button _pauseButton;
        private readonly Timer _overlayTimer;
        private readonly double _aspectRatio;
        private bool _isPlaybackPaused;
        private bool _pauseUsedMuteFallback;
        private bool _isClosing;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public MediaPopoutForm(string storageRoot, MediaPopoutTarget target)
        {
            _storageRoot = storageRoot;
            _target = target;

            Text = string.Empty;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 340);
            MinimumSize = new Size(360, 220);
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowIcon = false;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            _aspectRatio = (double)Size.Width / Math.Max(1, Size.Height);

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.BackColor = Color.Black;
            Controls.Add(_webView);

            _closeButton = CreateOverlayButton("X", new Size(34, 34), OnCloseButtonClicked);
            _pauseButton = CreateOverlayButton("||", new Size(64, 64), OnPauseButtonClicked);
            _pauseButton.Font = new Font("Segoe UI", 18.0f, FontStyle.Bold);
            Controls.Add(_pauseButton);
            Controls.Add(_closeButton);

            _overlayTimer = new Timer();
            _overlayTimer.Interval = 125;
            _overlayTimer.Tick += OnOverlayTimerTick;

            Resize += OnFormResize;
            Shown += OnShown;
            Activated += OnActivationChanged;
            Deactivate += OnActivationChanged;
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
            MouseDown += OnDragSurfaceMouseDown;
        }

        private async void OnShown(object sender, EventArgs e)
        {
            try
            {
                string userDataFolder = Path.Combine(_storageRoot, "UserData");
                Directory.CreateDirectory(userDataFolder);

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                await RegisterInteractionScriptAsync();
                _webView.MouseMove += OnInteractiveSurfaceMouseActivity;
                _webView.MouseEnter += OnInteractiveSurfaceMouseActivity;
                _webView.MouseLeave += OnInteractiveSurfaceMouseLeave;
                _webView.MouseDown += OnDragSurfaceMouseDown;
                _closeButton.MouseMove += OnInteractiveSurfaceMouseActivity;
                _pauseButton.MouseMove += OnInteractiveSurfaceMouseActivity;
                LayoutOverlayButtons();
                UpdateOverlayVisibility();
                _overlayTimer.Start();

                if (_target != null && _target.UseHostedVideoElement)
                {
                    _webView.CoreWebView2.NavigateToString(BuildHostedVideoHtml(_target.PlayerUrl));
                }
                else if (_target != null && !string.IsNullOrWhiteSpace(_target.PlayerUrl))
                {
                    _webView.CoreWebView2.Navigate(_target.PlayerUrl);
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                AgentDebugLog.Write(
                    "pre-fix",
                    "N4",
                    "DestinyChatDesktop.cs:7017",
                    "Media popout initialization failed",
                    new Dictionary<string, object>
                    {
                        { "playerUrl", _target != null ? (_target.PlayerUrl ?? string.Empty) : string.Empty },
                        { "useHostedVideoElement", _target != null && _target.UseHostedVideoElement },
                        { "error", ex.Message }
                    });
                // #endregion
                MessageBox.Show(
                    this,
                    "The media popout could not be opened.\r\n\r\n" + ex.Message,
                    "Media Popout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
            }
        }

        private Button CreateOverlayButton(string text, Size size, EventHandler clickHandler)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = size;
            button.Visible = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(92, 92, 92);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(116, 116, 116);
            button.BackColor = Color.FromArgb(72, 72, 72);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 11.0f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Click += clickHandler;
            return button;
        }

        private System.Threading.Tasks.Task RegisterInteractionScriptAsync()
        {
            return _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    const host = window.chrome && window.chrome.webview;
                    if (!host) {
                        return;
                    }

                    const corner = 16;
                    let lastZone = '';

                    function getZone(x, y) {
                        const width = window.innerWidth || document.documentElement.clientWidth || 0;
                        const height = window.innerHeight || document.documentElement.clientHeight || 0;
                        const left = x <= corner;
                        const right = x >= width - corner;
                        const top = y <= corner;
                        const bottom = y >= height - corner;

                        if (top && left) return 'top-left';
                        if (top && right) return 'top-right';
                        if (bottom && left) return 'bottom-left';
                        if (bottom && right) return 'bottom-right';
                        return '';
                    }

                    function sendHover(zone) {
                        if (zone === lastZone) {
                            return;
                        }

                        lastZone = zone;
                        host.postMessage({ type: 'pip-hover', zone: zone });
                    }

                    window.addEventListener('mousemove', (event) => {
                        sendHover(getZone(event.clientX, event.clientY));
                    }, true);

                    window.addEventListener('mouseleave', () => {
                        sendHover('');
                    }, true);

                    window.addEventListener('mousedown', (event) => {
                        if (event.button !== 0) {
                            return;
                        }

                        const zone = getZone(event.clientX, event.clientY);
                        host.postMessage({ type: 'pip-press', zone: zone || 'move' });
                        event.preventDefault();
                        event.stopPropagation();
                    }, true);

                    document.addEventListener('dragstart', (event) => {
                        event.preventDefault();
                        event.stopPropagation();
                    }, true);

                    document.addEventListener('drop', (event) => {
                        event.preventDefault();
                        event.stopPropagation();
                    }, true);
                })();
            ");
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object payload = serializer.DeserializeObject(e.WebMessageAsJson);
                Dictionary<string, object> message = payload as Dictionary<string, object>;
                if (message == null || !message.ContainsKey("type"))
                {
                    return;
                }

                string type = Convert.ToString(message["type"]);
                string zone = message.ContainsKey("zone") ? Convert.ToString(message["zone"]) : string.Empty;

                if (string.Equals(type, "pip-hover", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyInteractionCursor(zone);
                    return;
                }

                if (string.Equals(type, "pip-press", StringComparison.OrdinalIgnoreCase))
                {
                    BeginWindowInteraction(zone);
                }
            }
            catch
            {
                // Ignore malformed page messages.
            }
        }

        private void ApplyInteractionCursor(string zone)
        {
            Cursor cursor = Cursors.Default;
            switch ((zone ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "top-left":
                case "bottom-right":
                    cursor = Cursors.SizeNWSE;
                    break;
                case "top-right":
                case "bottom-left":
                    cursor = Cursors.SizeNESW;
                    break;
            }

            Cursor = cursor;
            _webView.Cursor = cursor;
        }

        private void BeginWindowInteraction(string zone)
        {
            if (_isClosing || !IsHandleCreated)
            {
                return;
            }

            int hit = HtCaption;
            switch ((zone ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "top-left":
                    hit = HtTopLeft;
                    break;
                case "top-right":
                    hit = HtTopRight;
                    break;
                case "bottom-left":
                    hit = HtBottomLeft;
                    break;
                case "bottom-right":
                    hit = HtBottomRight;
                    break;
                default:
                    hit = HtCaption;
                    break;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNclButtonDown, (IntPtr)hit, IntPtr.Zero);
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            LayoutOverlayButtons();
            UpdateOverlayVisibility();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;
            _overlayTimer.Stop();
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            _overlayTimer.Tick -= OnOverlayTimerTick;
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
            _webView.MouseMove -= OnInteractiveSurfaceMouseActivity;
            _webView.MouseEnter -= OnInteractiveSurfaceMouseActivity;
            _webView.MouseLeave -= OnInteractiveSurfaceMouseLeave;
            _webView.MouseDown -= OnDragSurfaceMouseDown;
            _closeButton.MouseMove -= OnInteractiveSurfaceMouseActivity;
            _pauseButton.MouseMove -= OnInteractiveSurfaceMouseActivity;
            MouseDown -= OnDragSurfaceMouseDown;
        }

        private void OnActivationChanged(object sender, EventArgs e)
        {
            UpdateOverlayVisibility();
        }

        private void OnInteractiveSurfaceMouseActivity(object sender, EventArgs e)
        {
            UpdateOverlayVisibility();
        }

        private void OnInteractiveSurfaceMouseLeave(object sender, EventArgs e)
        {
            UpdateOverlayVisibility();
        }

        private void OnDragSurfaceMouseDown(object sender, MouseEventArgs e)
        {
            if (_isClosing || e.Button != MouseButtons.Left || !IsHandleCreated)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNclButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmSizing)
            {
                NativeRect rect = (NativeRect)Marshal.PtrToStructure(m.LParam, typeof(NativeRect));
                ConstrainAspectRatio(ref rect, m.WParam.ToInt32());
                Marshal.StructureToPtr(rect, m.LParam, true);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmNcHitTest && FormBorderStyle == FormBorderStyle.None)
            {
                base.WndProc(ref m);

                if ((int)m.Result == HtClient)
                {
                    Point clientPoint = PointToClient(new Point(
                        (short)(m.LParam.ToInt32() & 0xFFFF),
                        (short)((m.LParam.ToInt32() >> 16) & 0xFFFF)));

                    bool left = clientPoint.X >= 0 && clientPoint.X <= ResizeBorderThickness;
                    bool right = clientPoint.X <= ClientSize.Width && clientPoint.X >= ClientSize.Width - ResizeBorderThickness;
                    bool top = clientPoint.Y >= 0 && clientPoint.Y <= ResizeBorderThickness;
                    bool bottom = clientPoint.Y <= ClientSize.Height && clientPoint.Y >= ClientSize.Height - ResizeBorderThickness;

                    if (left && top)
                    {
                        m.Result = (IntPtr)HtTopLeft;
                        return;
                    }

                    if (right && top)
                    {
                        m.Result = (IntPtr)HtTopRight;
                        return;
                    }

                    if (left && bottom)
                    {
                        m.Result = (IntPtr)HtBottomLeft;
                        return;
                    }

                    if (right && bottom)
                    {
                        m.Result = (IntPtr)HtBottomRight;
                        return;
                    }

                    if (left)
                    {
                        m.Result = (IntPtr)HtLeft;
                        return;
                    }

                    if (right)
                    {
                        m.Result = (IntPtr)HtRight;
                        return;
                    }

                    if (top)
                    {
                        m.Result = (IntPtr)HtTop;
                        return;
                    }

                    if (bottom)
                    {
                        m.Result = (IntPtr)HtBottom;
                        return;
                    }

                    if (clientPoint.Y <= DragBandHeight &&
                        !_closeButton.Bounds.Contains(clientPoint) &&
                        !_pauseButton.Bounds.Contains(clientPoint))
                    {
                        m.Result = (IntPtr)HtCaption;
                        return;
                    }
                }

                return;
            }

            base.WndProc(ref m);
        }

        private void ConstrainAspectRatio(ref NativeRect rect, int edge)
        {
            switch (edge)
            {
                case WmszTopLeft:
                case WmszTopRight:
                case WmszBottomLeft:
                case WmszBottomRight:
                    break;
                default:
                    return;
            }

            int minWidth = Math.Max(MinimumSize.Width, 160);
            int minHeight = Math.Max(MinimumSize.Height, 120);
            int width = Math.Max(minWidth, rect.Right - rect.Left);
            int height = Math.Max(minHeight, rect.Bottom - rect.Top);

            int aspectHeightFromWidth = Math.Max(minHeight, (int)Math.Round(width / _aspectRatio));
            int aspectWidthFromHeight = Math.Max(minWidth, (int)Math.Round(height * _aspectRatio));

            if (Math.Abs(aspectHeightFromWidth - height) <= Math.Abs(aspectWidthFromHeight - width))
            {
                height = aspectHeightFromWidth;
                width = Math.Max(minWidth, (int)Math.Round(height * _aspectRatio));
            }
            else
            {
                width = aspectWidthFromHeight;
                height = Math.Max(minHeight, (int)Math.Round(width / _aspectRatio));
            }

            switch (edge)
            {
                case WmszTopLeft:
                    rect.Left = rect.Right - width;
                    rect.Top = rect.Bottom - height;
                    break;
                case WmszTopRight:
                    rect.Right = rect.Left + width;
                    rect.Top = rect.Bottom - height;
                    break;
                case WmszBottomLeft:
                    rect.Left = rect.Right - width;
                    rect.Bottom = rect.Top + height;
                    break;
                case WmszBottomRight:
                    rect.Right = rect.Left + width;
                    rect.Bottom = rect.Top + height;
                    break;
            }
        }

        private void OnOverlayTimerTick(object sender, EventArgs e)
        {
            UpdateOverlayVisibility();
        }

        private void LayoutOverlayButtons()
        {
            if (_isClosing || IsDisposed || Disposing)
            {
                return;
            }

            _closeButton.Location = new Point(Math.Max(8, ClientSize.Width - _closeButton.Width - 10), 10);
            _pauseButton.Location = new Point(
                Math.Max(0, (ClientSize.Width - _pauseButton.Width) / 2),
                Math.Max(0, (ClientSize.Height - _pauseButton.Height) / 2));
            _closeButton.BringToFront();
            _pauseButton.BringToFront();
        }

        private void UpdateOverlayVisibility()
        {
            if (_isClosing || IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            Point clientPoint = PointToClient(Cursor.Position);
            bool cursorInside = ClientRectangle.Contains(clientPoint);
            bool showButtons = cursorInside;

            _closeButton.Visible = showButtons;
            _pauseButton.Visible = showButtons;
            _closeButton.BringToFront();
            _pauseButton.BringToFront();
        }

        private void OnCloseButtonClicked(object sender, EventArgs e)
        {
            Close();
        }

        private async void OnPauseButtonClicked(object sender, EventArgs e)
        {
            if (_isClosing || _webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                bool targetPause = !_isPlaybackPaused;
                // #region agent log
                AgentDebugLog.Write(
                    "post-fix",
                    "P2",
                    "DestinyChatDesktop.cs:7470",
                    "Pause button target state",
                    new Dictionary<string, object>
                    {
                        { "targetPause", targetPause },
                        { "wasPaused", _isPlaybackPaused },
                        { "usedMuteFallback", _pauseUsedMuteFallback }
                    });
                // #endregion

                if (await SetPlaybackWithScriptAsync(targetPause))
                {
                    return;
                }

                if (targetPause)
                {
                    if (await _webView.CoreWebView2.TrySuspendAsync())
                    {
                        _isPlaybackPaused = true;
                        _pauseUsedMuteFallback = false;
                        _pauseButton.Text = ">";
                        return;
                    }

                    _webView.CoreWebView2.IsMuted = true;
                    _isPlaybackPaused = true;
                    _pauseUsedMuteFallback = true;
                    _pauseButton.Text = ">";
                    return;
                }

                if (_pauseUsedMuteFallback)
                {
                    _webView.CoreWebView2.IsMuted = false;
                    _pauseUsedMuteFallback = false;
                }
                else
                {
                    _webView.CoreWebView2.Resume();
                }

                _isPlaybackPaused = false;
                _pauseButton.Text = "||";
            }
            catch
            {
                // Best-effort playback control.
            }
        }

        public async System.Threading.Tasks.Task<Dictionary<string, object>> RunPauseResumeButtonSelfTestAsync()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            try
            {
                int attempts = 0;
                while (_webView.CoreWebView2 == null && attempts < 60)
                {
                    attempts++;
                    await System.Threading.Tasks.Task.Delay(100);
                }

                if (_webView.CoreWebView2 == null)
                {
                    result["ok"] = false;
                    result["reason"] = "webview-not-ready";
                    return result;
                }

                await System.Threading.Tasks.Task.Delay(300);
                OnPauseButtonClicked(this, EventArgs.Empty);
                await System.Threading.Tasks.Task.Delay(350);
                bool pausedAfterFirstClick = _isPlaybackPaused;
                OnPauseButtonClicked(this, EventArgs.Empty);
                await System.Threading.Tasks.Task.Delay(350);
                bool pausedAfterSecondClick = _isPlaybackPaused;

                result["ok"] = pausedAfterFirstClick && !pausedAfterSecondClick;
                result["pausedAfterFirstClick"] = pausedAfterFirstClick;
                result["pausedAfterSecondClick"] = pausedAfterSecondClick;
                result["pauseButtonText"] = _pauseButton.Text ?? string.Empty;
                return result;
            }
            catch (Exception ex)
            {
                result["ok"] = false;
                result["reason"] = "exception";
                result["error"] = ex.Message;
                return result;
            }
        }

        private async System.Threading.Tasks.Task<bool> SetPlaybackWithScriptAsync(bool pause)
        {
            string resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                (() => {
                    function collectMedia() {
                        const out = [];
                        function walk(root) {
                            try {
                                for (const el of root.querySelectorAll('video, audio')) {
                                    out.push(el);
                                }
                                for (const frame of root.querySelectorAll('iframe')) {
                                    try {
                                        const d = frame.contentDocument;
                                        if (d) {
                                            walk(d);
                                        }
                                    } catch (error) {
                                    }
                                }
                            } catch (error) {
                            }
                        }
                        walk(document);
                        return out;
                    }

                    const media = collectMedia();
                    if (!media.length) {
                        return { handled: false, paused: false, mediaCount: 0 };
                    }

                    const shouldPause = " + (pause ? "true" : "false") + @";
                    if (shouldPause) {
                        media.forEach((el) => {
                            try {
                                el.pause();
                            } catch (error) {
                            }
                        });
                        return { handled: true, paused: true, mediaCount: media.length };
                    }

                    media.forEach((el) => {
                        try {
                            const playPromise = el.play();
                            if (playPromise && typeof playPromise.catch === 'function') {
                                playPromise.catch(() => {});
                            }
                        } catch (error) {
                        }
                    });

                    return { handled: true, paused: false, mediaCount: media.length };
                })();
            ");

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> payload = serializer.Deserialize<Dictionary<string, object>>(resultJson);
            // #region agent log
            AgentDebugLog.Write(
                "post-fix",
                "P2",
                "DestinyChatDesktop.cs:7578",
                "Set playback script result",
                new Dictionary<string, object>
                {
                    { "targetPause", pause },
                    { "resultJson", resultJson ?? string.Empty },
                    { "payloadParsed", payload != null }
                });
            // #endregion
            if (payload == null || !payload.ContainsKey("handled"))
            {
                return false;
            }

            bool handled = Convert.ToBoolean(payload["handled"]);
            if (!handled)
            {
                return false;
            }

            _isPlaybackPaused = payload.ContainsKey("paused") && Convert.ToBoolean(payload["paused"]);
            _pauseButton.Text = _isPlaybackPaused ? ">" : "||";
            return true;
        }

        private static string BuildHostedVideoHtml(string videoUrl)
        {
            string encodedUrl = WebUtility.HtmlEncode(videoUrl ?? string.Empty);
            StringBuilder html = new StringBuilder();
            html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.Append("<style>");
            html.Append("html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:#000;}");
            html.Append("video{width:100vw;height:100vh;display:block;background:#000;object-fit:contain;}");
            html.Append("</style></head><body>");
            html.Append("<video autoplay playsinline controls src=\"");
            html.Append(encodedUrl);
            html.Append("\"></video></body></html>");
            return html.ToString();
        }
    }

    internal sealed class BorderlessToolStripRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // Intentionally suppress default bottom border line.
        }
    }

    public sealed class SpringToolStripTextBox : ToolStripTextBox
    {
        public override Size GetPreferredSize(Size constrainingSize)
        {
            if (IsOnOverflow || Owner == null)
            {
                return DefaultSize;
            }

            int width = Owner.DisplayRectangle.Width;
            if (Owner.OverflowButton.Visible)
            {
                width -= Owner.OverflowButton.Width + Owner.OverflowButton.Margin.Horizontal;
            }

            int springBoxCount = 0;
            foreach (ToolStripItem item in Owner.Items)
            {
                if (item.IsOnOverflow)
                {
                    continue;
                }

                SpringToolStripTextBox springBox = item as SpringToolStripTextBox;
                if (springBox != null)
                {
                    springBoxCount++;
                }
                else
                {
                    width -= item.Width;
                    width -= item.Margin.Horizontal;
                }
            }

            if (springBoxCount > 1)
            {
                width /= springBoxCount;
            }

            if (width < DefaultSize.Width)
            {
                width = DefaultSize.Width;
            }

            Size size = base.GetPreferredSize(constrainingSize);
            size.Width = width;
            return size;
        }

        protected override Size DefaultSize
        {
            get
            {
                return new Size(200, 23);
            }
        }
    }

    public sealed class EmbedChip : Control
    {
        private bool _hovered;
        private bool _pressed;
        private string _badgeText;
        private bool _isSelected;
        private Color _accentColor;

        public EmbedChip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);

            BackColor = Color.FromArgb(28, 28, 32);
            ForeColor = Color.FromArgb(222, 222, 228);
            Font = new Font("Segoe UI", 9.75f, FontStyle.Bold);
            Size = new Size(200, 28);
            TabStop = true;
            _badgeText = string.Empty;
            _isSelected = false;
            _accentColor = Color.FromArgb(92, 96, 104);
        }

        public string BadgeText
        {
            get { return _badgeText; }
            set
            {
                _badgeText = value ?? string.Empty;
                Invalidate();
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                Invalidate();
            }
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set
            {
                _accentColor = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Focus();
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _isSelected
                ? Color.FromArgb(44, 48, 58)
                : _pressed
                    ? Color.FromArgb(42, 42, 48)
                    : _hovered
                        ? Color.FromArgb(34, 34, 40)
                        : BackColor;
            Color border = Focused
                ? Color.FromArgb(96, 132, 192)
                : _isSelected
                    ? Color.FromArgb(86, 96, 112)
                    : Color.FromArgb(48, 48, 54);
            Color textColor = _isSelected
                ? Color.FromArgb(243, 244, 246)
                : ForeColor;

            using (SolidBrush fillBrush = new SolidBrush(fill))
            using (Pen borderPen = new Pen(border))
            using (SolidBrush badgeBrush = new SolidBrush(_accentColor))
            using (Font badgeFont = new Font("Segoe UI", 8.0f, FontStyle.Bold))
            {
                e.Graphics.FillRectangle(fillBrush, rect);
                e.Graphics.DrawRectangle(borderPen, rect);

                int left = 8;
                if (!string.IsNullOrWhiteSpace(_badgeText))
                {
                    Rectangle badgeRect = new Rectangle(8, 5, Math.Min(34, Math.Max(24, 14 + (_badgeText.Length * 7))), Height - 10);
                    e.Graphics.FillRectangle(badgeBrush, badgeRect);

                    TextRenderer.DrawText(
                        e.Graphics,
                        _badgeText,
                        badgeFont,
                        badgeRect,
                        Color.FromArgb(248, 248, 248),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                    left = badgeRect.Right + 8;
                }

                Rectangle textRect = new Rectangle(left, 0, Math.Max(0, Width - left - 8), Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    textRect,
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine);
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == Keys.Space)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }

    public sealed class EmbedViewportPanel : Panel
    {
        public EmbedViewportPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = true;
        }
    }
}
