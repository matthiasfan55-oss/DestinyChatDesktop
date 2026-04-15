using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DestinyChatDesktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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

            if (runSplitSelfTest && File.Exists(selfTestResultPath))
            {
                File.Delete(selfTestResultPath);
            }

            Application.Run(new MainForm(storageRoot, statePath, config, state, runSplitSelfTest, selfTestResultPath));
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
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            File.WriteAllText(path, serializer.Serialize(this));
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
        private readonly string _selfTestResultPath;
        private bool _splitSelfTestStarted;
        private System.Threading.Tasks.Task _pendingSplitSelfTestTask;
        private bool _dualChatHostEnabled;
        private bool _dualChatHostAvailable;
        private string _dualChatRequestedUrl;
        private string _dualChatEmptyMessage;
        private int _dualChatInputTop;
        private int _dualChatPaneLeft;
        private int _dualChatPaneTop;
        private int _dualChatPaneWidth;
        private int _dualChatPaneHeight;
        private bool _dualChatLayoutActive;
        private int _bigscreenChatTopOffset;
        private bool _isCheckingForUpdates;
        private bool _startupUpdateCheckQueued;

        public MainForm(string storageRoot, string statePath, AppConfig config, AppState state, bool runSplitSelfTest, string selfTestResultPath)
        {
            _storageRoot = storageRoot;
            _statePath = statePath;
            _cookiePath = Path.Combine(_storageRoot, "cookies.json");
            _config = config;
            _state = state;
            _runSplitSelfTest = runSplitSelfTest;
            _selfTestResultPath = selfTestResultPath ?? string.Empty;
            _pendingNavigation = runSplitSelfTest ? _config.HomeUrl : GetStartupUrl();
            _latestBigscreenEmbeds = new List<BigscreenEmbedState>();
            _dualChatInputTop = 0;
            _dualChatPaneLeft = 0;
            _dualChatPaneTop = 0;
            _dualChatPaneWidth = 0;
            _dualChatPaneHeight = 0;
            _dualChatLayoutActive = false;

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
            _toolStrip.RenderMode = ToolStripRenderMode.System;
            _toolStrip.BackColor = Color.FromArgb(30, 30, 30);
            _toolStrip.ForeColor = Color.White;
            _toolStrip.Padding = new Padding(6, 4, 6, 4);

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
            _toolStrip.Items.Add(_loginButton);
            _toolStrip.Items.Add(_reloadButton);
            _toolStrip.Items.Add(_updateButton);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_muteButton);
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
            _bigscreenBarPanel.Height = 84;
            _bigscreenBarPanel.BackColor = Color.FromArgb(17, 17, 19);
            _bigscreenBarPanel.Visible = false;
            _bigscreenBarPanel.Resize += OnBigscreenBarPanelResize;

            _bigscreenControlsPanel = new Panel();
            _bigscreenControlsPanel.Dock = DockStyle.None;
            _bigscreenControlsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _bigscreenControlsPanel.Height = 43;
            _bigscreenControlsPanel.Margin = new Padding(0);
            _bigscreenControlsPanel.Location = new Point(0, 0);
            _bigscreenControlsPanel.Width = _bigscreenBarPanel.Width;
            _bigscreenControlsPanel.Padding = new Padding(8, 2, 8, 3);
            _bigscreenControlsPanel.BackColor = Color.FromArgb(17, 17, 19);

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

            _bigscreenControlsPanel.Controls.Add(_bigscreenCinemaButton);
            _bigscreenControlsPanel.Controls.Add(_bigscreenRefreshButton);
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
            button.Height = 22;
            button.Margin = new Padding(2, -4, 2, 2);
            button.ToolTipText = toolTipText;
            button.Font = new Font("Segoe UI Symbol", 8.75f, FontStyle.Regular);
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
            _dualChatView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _dualChatView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            _dualChatView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _dualChatView.CoreWebView2.IsMuted = true;
            _dualChatView.CoreWebView2.NewWindowRequested += OnDualChatNewWindowRequested;

            // Inject CSS to hide the YouTube live chat header (shown in popout mode)
            // so only the message list and input are visible.
            _dualChatView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    const style = document.createElement('style');
                    style.textContent =
                        'yt-live-chat-header-renderer,' +
                        '#chat-messages > yt-live-chat-header-renderer,' +
                        'yt-live-chat-ticker-renderer { display: none !important; }';
                    document.head
                        ? document.head.appendChild(style)
                        : document.addEventListener('DOMContentLoaded', () => document.head.appendChild(style));
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
                    const dualButtonId = 'codex-dual-stream-chat-btn';
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
                                background-image: url(""data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><rect x='3.25' y='4.25' width='17.5' height='15.5' rx='1.5' fill='none' stroke='white' stroke-width='1.75'/><path d='M12 4.5v15' stroke='white' stroke-width='1.75' stroke-linecap='round'/><rect x='4.5' y='5.5' width='7' height='13' rx='0.75' fill='rgba(255,255,255,0.35)' stroke='none'/></svg>"");
                            }

                            #${dualButtonId}.codex-split-chat-active .btn-icon {
                                opacity: 1;
                            }

                            #chat-output-frame {
                                position: relative;
                            }

                            #${dualPaneId} {
                                position: absolute;
                                top: 0;
                                right: 0;
                                bottom: var(--codex-dual-chat-bottom-offset, 0px);
                                width: 0;
                                display: none !important;
                                overflow: hidden;
                                background: transparent;
                                border-left: none;
                                box-sizing: border-box;
                                z-index: -1;
                                opacity: 0 !important;
                                visibility: hidden !important;
                                pointer-events: none !important;
                            }

                            #${dualPaneId}.enabled {
                                display: none !important;
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
                            eyeButton.parentElement.insertBefore(button, eyeButton);
                        }

                        return button;
                    }

                    function ensureDualButton() {
                        const externalButton = document.getElementById('codex-ext-panel-btn');
                        const snipButton = document.getElementById('codex-snip-btn');
                        const splitButton = document.getElementById(buttonId);
                        const fallbackRef = document.getElementById('chat-watching-focus-btn')
                            || document.getElementById('chat-settings-btn');
                        const ref = externalButton || fallbackRef || splitButton;
                        if (!ref || !ref.parentElement) {
                            return null;
                        }

                        let btn = document.getElementById(dualButtonId);
                        if (!btn) {
                            btn = document.createElement('a');
                            btn.id = dualButtonId;
                            btn.className = 'chat-tool-btn';
                            btn.setAttribute('role', 'button');
                            btn.title = 'Dual Chat';
                            btn.setAttribute('data-tippy-content', 'Dual Chat');
                            btn.innerHTML = '<i class=""btn-icon""></i>';
                            btn.addEventListener('click', (event) => {
                                event.preventDefault();
                                event.stopPropagation();
                                setDualChatEnabled(!dualChatEnabled, true);
                            });
                        }

                        if (externalButton && externalButton.parentElement === ref.parentElement) {
                            if (btn.parentElement !== ref.parentElement || btn.nextElementSibling !== externalButton) {
                                ref.parentElement.insertBefore(btn, externalButton);
                            }
                        } else if (snipButton && snipButton.parentElement === ref.parentElement) {
                            const insertBeforeNode = snipButton.nextSibling === btn ? btn.nextSibling : snipButton.nextSibling;
                            if (btn.parentElement !== ref.parentElement || btn.previousElementSibling !== snipButton) {
                                if (insertBeforeNode) {
                                    ref.parentElement.insertBefore(btn, insertBeforeNode);
                                } else {
                                    ref.parentElement.appendChild(btn);
                                }
                            }
                        } else if (splitButton && splitButton.parentElement === ref.parentElement) {
                            const insertBeforeNode = splitButton.nextSibling === btn ? btn.nextSibling : splitButton.nextSibling;
                            if (btn.parentElement !== ref.parentElement || btn.previousElementSibling !== splitButton) {
                                if (insertBeforeNode) {
                                    ref.parentElement.insertBefore(btn, insertBeforeNode);
                                } else {
                                    ref.parentElement.appendChild(btn);
                                }
                            }
                        } else if (btn.parentElement !== ref.parentElement || btn.nextElementSibling !== ref) {
                            ref.parentElement.insertBefore(btn, ref);
                        }

                        syncDualChatButton();
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

                        if (!payload || payload.type !== 'codex-dual-chat-source') {
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

                    function requestDualChatSource() {
                        const host = window.chrome && window.chrome.webview;
                        if (host && typeof host.postMessage === 'function') {
                            host.postMessage({ type: 'codex-dual-chat-request' });
                        }
                    }

                    function ensureDualChatPane() {
                        const frame = document.getElementById('chat-output-frame');
                        if (!frame) {
                            return null;
                        }

                        let pane = document.getElementById(dualPaneId);
                        if (!pane) {
                            pane = document.createElement('div');
                            pane.id = dualPaneId;
                            pane.innerHTML =
                                '<iframe class=""codex-dual-chat-frame"" allow=""autoplay; clipboard-write"" sandbox=""allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox""></iframe>' +
                                '<div class=""codex-dual-chat-empty""></div>';
                            frame.appendChild(pane);
                        } else if (pane.parentElement !== frame) {
                            frame.appendChild(pane);
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
                        btn.style.opacity = dualChatSource && dualChatSource.available ? '' : '0.78';
                    }

                    function bindDualChatInputResizeObserver() {
                        const inputAnchor = document.querySelector('#chat-input-control')
                            || document.querySelector('#chat-input-wrap')
                            || document.querySelector('#chat-input-frame');
                        if (dualChatInputObserved === inputAnchor) {
                            return;
                        }

                        dualChatInputObserved = inputAnchor;
                        if (dualChatInputResizeObserver) {
                            dualChatInputResizeObserver.disconnect();
                        }

                        if (inputAnchor instanceof Element && typeof ResizeObserver === 'function') {
                            dualChatInputResizeObserver = new ResizeObserver(() => {
                                scheduleDualChatLayoutReport();
                            });
                            dualChatInputResizeObserver.observe(inputAnchor);
                            if (inputAnchor.parentElement) {
                                dualChatInputResizeObserver.observe(inputAnchor.parentElement);
                            }
                        }
                    }

                    function reportDualChatLayout() {
                        bindDualChatInputResizeObserver();

                        const root = document.documentElement;
                        const frame = document.getElementById('chat-output-frame');
                        const inputAnchor = document.querySelector('#chat-input-control')
                            || document.querySelector('#chat-input-wrap')
                            || document.querySelector('#chat-input-frame');
                        let inputTop = 0;
                        let bottomOffset = 0;
                        let paneLeft = 0;
                        let paneTop = 0;
                        let paneWidth = 0;
                        let paneHeight = 0;
                        const layoutActive = !!dualChatEnabled && document.body.classList.contains('codex-dual-chat-enabled');

                        if (inputAnchor instanceof Element) {
                            const rect = inputAnchor.getBoundingClientRect();
                            if (rect.height > 0 && rect.top < window.innerHeight) {
                                inputTop = Math.max(0, Math.round(rect.top));
                                bottomOffset = Math.max(0, Math.round(window.innerHeight - rect.top));
                            }
                        }

                        if (layoutActive && frame instanceof Element) {
                            const frameRect = frame.getBoundingClientRect();
                            if (frameRect.width > 0 && inputTop > frameRect.top) {
                                paneWidth = Math.max(0, Math.round((frameRect.width - 14) / 2));
                                paneTop = Math.max(0, Math.round(frameRect.top));
                                paneLeft = Math.max(0, Math.round(frameRect.right - paneWidth));
                                paneHeight = Math.max(0, Math.round(inputTop - frameRect.top));
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
                                paneHeight: paneHeight
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
                        const host = window.chrome && window.chrome.webview;
                        if (!host || typeof host.postMessage !== 'function') {
                            return;
                        }

                        const available = !!(dualChatSource && dualChatSource.available && dualChatSource.chatUrl);
                        host.postMessage({
                            type: 'codex-dual-chat-state',
                            enabled: !!dualChatEnabled,
                            available: available,
                            chatUrl: available ? dualChatSource.chatUrl : '',
                            message: available ? '' : getDualChatUnavailableText()
                        });
                    }

                    function updateDualChatPane() {
                        const pane = ensureDualChatPane();
                        if (!pane) {
                            return;
                        }

                        scheduleDualChatLayoutReport();

                        const iframe = pane.querySelector('.codex-dual-chat-frame');
                        const empty = pane.querySelector('.codex-dual-chat-empty');
                        const desiredChatUrl = dualChatSource && dualChatSource.available && dualChatSource.chatUrl
                            ? dualChatSource.chatUrl
                            : '';
                        pane.setAttribute('data-codex-chat-url', desiredChatUrl);
                        pane.classList.toggle('enabled', !!dualChatEnabled);
                        document.body.classList.toggle('codex-dual-chat-enabled', !!dualChatEnabled);

                        if (!dualChatEnabled) {
                            pane.classList.remove('empty');
                            if (iframe && iframe.getAttribute('src') !== 'about:blank') {
                                iframe.setAttribute('src', 'about:blank');
                            }
                            syncDualChatButton();
                            notifyDualChatHostState();
                            return;
                        }

                        if (dualChatSource && dualChatSource.available && dualChatSource.chatUrl) {
                            pane.classList.remove('empty');
                            if (empty) {
                                empty.textContent = '';
                            }
                            if (iframe && iframe.getAttribute('src') !== 'about:blank') {
                                iframe.setAttribute('src', 'about:blank');
                            }
                        } else {
                            pane.classList.add('empty');
                            if (iframe && iframe.getAttribute('src') !== 'about:blank') {
                                iframe.setAttribute('src', 'about:blank');
                            }
                            if (empty) {
                                empty.textContent = getDualChatUnavailableText();
                            }
                        }

                        syncDualChatButton();
                        notifyDualChatHostState();
                    }

                    function setDualChatEnabled(enabled, refreshSource) {
                        dualChatEnabled = !!enabled;
                        if (dualChatEnabled) {
                            if (splitEnabled) {
                                setSplitEnabled(false, false);
                            }

                            const closeExternalPanel = window.__codexCloseExtPanel;
                            if (typeof closeExternalPanel === 'function') {
                                try {
                                    closeExternalPanel();
                                } catch (error) {
                                }
                            }
                        }

                        if (refreshSource || dualChatEnabled) {
                            requestDualChatSource();
                        }

                        updateDualChatPane();
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
                    function watchMsgNotify() {
                        const lines = document.querySelector('.chat-lines');
                        if (!lines) return;
                        new MutationObserver(muts => {
                            for (const m of muts) {
                                for (const n of m.addedNodes) {
                                    if (!(n instanceof Element) || !n.classList.contains('msg-user')) continue;
                                    const nick = n.getAttribute('data-username') || '';
                                    if (!isWatched(nick)) continue;
                                    maybeNotify(nick, n.querySelector('.text')?.textContent?.trim() || '');
                                }
                            }
                        }).observe(lines, { childList: true });
                    }

                    // ── Feature 3: DinkDonk button ───────────────────────────────
                    let ddTimer = null;
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
                        if (!data.startsWith('BROADCAST ')) return;
                        try {
                            const p = JSON.parse(data.slice(10));
                            if (p.nick === 'DinkDonkBot' && typeof p.data === 'string' && p.data.includes('dinkdonk.mov')) {
                                const btn = document.getElementById('codex-dinkdonk-btn');
                                if (!btn) return;
                                btn.href = p.data;
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
                        } catch {}
                    });

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

                        return document.getElementById('codex-dual-stream-chat-btn');
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
                        if (!src) return;
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
                    function buildExtPanelBtn() {
                        if (document.getElementById('codex-ext-panel-btn')) return;
                        const ref = document.getElementById('chat-watching-focus-btn')
                            || document.getElementById('chat-settings-btn');
                        if (!ref || !ref.parentElement) return;
                        const btn = document.createElement('a');
                        btn.id = 'codex-ext-panel-btn';
                        btn.className = 'chat-tool-btn';
                        btn.setAttribute('role', 'button');
                        btn.title = 'External Chat Panel';
                        btn.setAttribute('data-tippy-content', 'External Chat Panel');
                        btn.innerHTML = `<i class=""btn-icon"" style=""background-repeat:no-repeat;background-position:center;background-size:90% 90%;background-image:url(&quot;data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><rect x='2' y='3' width='9' height='18' rx='1.5' fill='none' stroke='white' stroke-width='1.75'/><rect x='13' y='3' width='9' height='18' rx='1.5' fill='none' stroke='white' stroke-width='1.75'/><line x1='7.5' y1='7' x2='7.5' y2='17' stroke='white' stroke-width='1.25' stroke-linecap='round'/><line x1='17.5' y1='7' x2='17.5' y2='17' stroke='white' stroke-width='1.25' stroke-linecap='round'/></svg>&quot;)""></i>`;
                        btn.addEventListener('click', e => {
                            e.preventDefault(); e.stopPropagation();
                            const p = document.getElementById(EXT_PANEL);
                            if (p && p.style.display !== 'none') closeExtPanel();
                            else openExtPanel();
                        });
                        ref.parentElement.insertBefore(btn, ref);
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
                            #codex-ext-settings-btn .btn-icon { opacity:.5; transition:opacity .15s; }
                            #codex-ext-settings-btn:hover .btn-icon,
                            #codex-ext-settings-btn.codex-split-chat-active .btn-icon { opacity:1; }
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

                        // ── Activity Alerts ──
                        body.appendChild(mkSection('🔔 Activity Alerts'));
                        body.appendChild(mkCheckRow('Show inline join/quit/embed alerts', 'activity.enabled', true));
                        body.appendChild(mkCheckRow('Alert on JOIN', 'activity.join', true));
                        body.appendChild(mkCheckRow('Alert on QUIT', 'activity.quit', true));
                        body.appendChild(mkCheckRow('Alert on embed update', 'activity.embed', true));
                        body.appendChild(mkCheckRow('Desktop notification when watched user messages', 'notifications.enabled', false, on => {
                            if (on && typeof Notification !== 'undefined' && Notification.permission === 'default') Notification.requestPermission();
                        }));

                        // ── DinkDonk ──
                        body.appendChild(mkSection('🎬 DinkDonk'));
                        body.appendChild(mkCheckRow('Show DinkDonk poll button', 'dinkdonk.enabled', true, on => {
                            const b = document.getElementById('codex-dinkdonk-btn');
                            if (b) b.style.display = on ? '' : 'none';
                        }));

                        // ── External Chat Panel ──
                        body.appendChild(mkSection('💬 External Chat Panel'));
                        const urlRow = document.createElement('div'); urlRow.className = 'xs-row';
                        const urlLabel = document.createElement('label');
                        urlLabel.textContent = 'URL:'; urlLabel.style.flex = '0 0 auto'; urlLabel.style.color = '#888';
                        const urlIn = document.createElement('input');
                        urlIn.type = 'text'; urlIn.placeholder = '#kick/username  or  #youtube/VIDEO_ID';
                        urlIn.value = xGet('externalChat.url', '');
                        urlIn.addEventListener('input', () => {
                            xSet('externalChat.url', urlIn.value.trim());
                            const iframe = document.querySelector('#' + EXT_PANEL + ' iframe');
                            if (iframe) { const src = extSrc(urlIn.value); if (src) iframe.src = src; }
                        });
                        urlRow.appendChild(urlLabel); urlRow.appendChild(urlIn);
                        body.appendChild(urlRow);

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
                    function buildSnipBtn() {
                        if (document.getElementById('codex-snip-btn')) return;
                        const ref = document.getElementById('codex-ext-panel-btn')
                            || document.getElementById('chat-settings-btn');
                        if (!ref || !ref.parentElement) return;
                        const btn = document.createElement('a');
                        btn.id = 'codex-snip-btn';
                        btn.className = 'chat-tool-btn';
                        btn.setAttribute('role', 'button');
                        btn.title = 'Snip & upload';
                        btn.setAttribute('data-tippy-content', 'Snip & upload to femboy.beauty');
                        btn.innerHTML = `<i class=""btn-icon"" style=""font-style:normal;font-size:16px;line-height:1;display:flex;align-items:center;justify-content:center;"">✂</i>`;
                        btn.addEventListener('click', e => {
                            e.preventDefault(); e.stopPropagation();
                            // Debug: immediately turn orange so we know click fired
                            btn.style.outline = '2px solid orange';
                            const host = window.chrome && window.chrome.webview;
                            if (!host) {
                                btn.style.outline = '2px solid red';
                                btn.title = 'ERR: no webview host';
                                return;
                            }
                            btn.title = 'Snipping…';
                            btn.classList.add('codex-split-chat-active');
                            host.postMessage({ type: 'codex-snip-start' });
                        });
                        ref.parentElement.insertBefore(btn, ref);
                    }

                    // ── Receive snip URL back from C# ────────────────────────────
                    if (!window.__codexSnipListenerAdded) {
                        window.__codexSnipListenerAdded = true;
                        const host = window.chrome && window.chrome.webview;
                        if (host) {
                            host.addEventListener('message', e => {
                                let msg;
                                try { msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data; } catch { return; }
                                if (!msg || msg.type !== 'codex-snip-done') return;
                                const btn = document.getElementById('codex-snip-btn');
                                if (btn) {
                                    btn.style.outline = '2px solid cyan';
                                    btn.classList.remove('codex-split-chat-active');
                                    btn.title = msg.url ? 'Snip & upload' : ('ERR: ' + (msg.error || 'unknown'));
                                }
                                if (!msg.url) {
                                    // Show error in chat input for debugging
                                    const ta = document.querySelector('#chat-input-control');
                                    if (ta) ta.value = '[snip error: ' + (msg.error || 'unknown') + ']';
                                    return;
                                }
                                const ta = document.querySelector('#chat-input-control');
                                if (!ta) return;
                                const s = ta.selectionStart != null ? ta.selectionStart : ta.value.length;
                                const en = ta.selectionEnd != null ? ta.selectionEnd : ta.value.length;
                                const prefix = (s > 0 && ta.value[s - 1] !== ' ') ? ' ' : '';
                                ta.value = ta.value.slice(0, s) + prefix + msg.url + ' ' + ta.value.slice(en);
                                ta.selectionStart = ta.selectionEnd = s + prefix.length + msg.url.length + 1;
                                ta.dispatchEvent(new Event('input', { bubbles: true }));
                                ta.focus();
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
                        btn.innerHTML = `<i class=""btn-icon"" style=""font-style:normal;font-size:15px;display:flex;align-items:center;justify-content:center;"">⚙</i>`;
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
                        buildDinkDonkBtn();
                        buildExtPanelBtn();
                        buildSnipBtn();
                        buildDualChatBtn();
                        requestDualChatSource();
                        setupPhraseInput();
                        setupHiddenPhrases();
                        setupImageUpload();
                        watchMsgNotify();
                        if (xGet('externalChat.open', false) && xGet('externalChat.url', '')) {
                            window.setTimeout(openExtPanel, 500);
                        }
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

            if (string.IsNullOrWhiteSpace(e.Uri) || e.Uri.IndexOf("/embed/chat", StringComparison.OrdinalIgnoreCase) < 0)
            {
                _dualChatInputTop = 0;
                LayoutDualChatPanel();
            }

            if (string.IsNullOrWhiteSpace(e.Uri) || e.Uri.IndexOf("/bigscreen", StringComparison.OrdinalIgnoreCase) < 0)
            {
                _bigscreenChatTopOffset = 0;
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                SetStatus("Ready.");
                PersistBrowserSession();
                NotifyDualChatSourceChanged();

                if (_runSplitSelfTest && !_splitSelfTestStarted)
                {
                    _splitSelfTestStarted = true;
                    _pendingSplitSelfTestTask = RunSplitSelfTestAsync();
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

            return currentUri.AbsoluteUri.IndexOf("/embed/chat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsBigscreenPage()
        {
            Uri currentUri = _webView.Source;
            if (currentUri == null)
            {
                return false;
            }

            return currentUri.AbsoluteUri.IndexOf("/bigscreen", StringComparison.OrdinalIgnoreCase) >= 0;
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
                                hasDualButton: !!document.getElementById('codex-dual-stream-chat-btn')
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

                        const dualButton = await waitFor(() => document.getElementById('codex-dual-stream-chat-btn'), 5000);
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
                            if (notify) {
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
                            }
                        }

                        return finish({
                            ok: true,
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
                            scrollAnchorBefore: scrollAnchorBefore,
                            scrollAnchorAfter: scrollAnchorAfter,
                            clickedUser: overlayUser.textContent ? overlayUser.textContent.trim() : ''
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
                if (_runSplitSelfTest)
                {
                    BeginInvoke(new Action(Close));
                }
            }
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

                if (string.Equals(type, "codex-snip-start", StringComparison.OrdinalIgnoreCase))
                {
                    // Debug: confirm C# received the message
                    _webView.CoreWebView2.ExecuteScriptAsync(
                        "document.getElementById('codex-snip-btn').style.outline='2px solid lime';");
                    HandleSnipAndUploadAsync();
                    return;
                }

                if (string.Equals(type, "codex-dual-chat-request", StringComparison.OrdinalIgnoreCase))
                {
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

                    ApplyDualChatHostState(enabled, available, chatUrl, emptyMessage);
                    return;
                }

                if (string.Equals(type, "codex-bigscreen-layout", StringComparison.OrdinalIgnoreCase))
                {
                    int chatTop = 0;
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

                    _bigscreenChatTopOffset = Math.Max(0, chatTop);
                    LayoutDualChatPanel();
                    return;
                }

                if (string.Equals(type, "codex-dual-chat-layout", StringComparison.OrdinalIgnoreCase))
                {
                    int inputTop = 0;
                    int paneLeft = 0;
                    int paneTop = 0;
                    int paneWidth = 0;
                    int paneHeight = 0;
                    bool layoutActive = false;
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

                    if (message.ContainsKey("paneLeft"))
                    {
                        object paneLeftValue = message["paneLeft"];
                        if (paneLeftValue is int)
                        {
                            paneLeft = (int)paneLeftValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(paneLeftValue), out paneLeft);
                        }
                    }

                    if (message.ContainsKey("paneTop"))
                    {
                        object paneTopValue = message["paneTop"];
                        if (paneTopValue is int)
                        {
                            paneTop = (int)paneTopValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(paneTopValue), out paneTop);
                        }
                    }

                    if (message.ContainsKey("paneWidth"))
                    {
                        object paneWidthValue = message["paneWidth"];
                        if (paneWidthValue is int)
                        {
                            paneWidth = (int)paneWidthValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(paneWidthValue), out paneWidth);
                        }
                    }

                    if (message.ContainsKey("paneHeight"))
                    {
                        object paneHeightValue = message["paneHeight"];
                        if (paneHeightValue is int)
                        {
                            paneHeight = (int)paneHeightValue;
                        }
                        else
                        {
                            int.TryParse(Convert.ToString(paneHeightValue), out paneHeight);
                        }
                    }

                    if (message.ContainsKey("layoutActive"))
                    {
                        object layoutActiveValue = message["layoutActive"];
                        if (layoutActiveValue is bool)
                        {
                            layoutActive = (bool)layoutActiveValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(layoutActiveValue), out layoutActive);
                        }
                    }

                    _dualChatInputTop = Math.Max(0, inputTop);
                    _dualChatPaneLeft = Math.Max(0, paneLeft);
                    _dualChatPaneTop = Math.Max(0, paneTop);
                    _dualChatPaneWidth = Math.Max(0, paneWidth);
                    _dualChatPaneHeight = Math.Max(0, paneHeight);
                    _dualChatLayoutActive = layoutActive;
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
            // Minimize the window so the full screen is visible for snipping
            this.Invoke((Action)(() => { this.WindowState = FormWindowState.Minimized; }));

            // Wait for minimize animation, then send the hotkey
            await System.Threading.Tasks.Task.Delay(400);

            // Clear clipboard so we can detect when a new snip lands
            this.Invoke((Action)(() => { try { Clipboard.Clear(); } catch { } }));

            // Send Win+Shift+S — system-level hotkey that triggers the snip overlay
            // on all Windows 10/11 machines regardless of which snip app is installed.
            // The snip overlay auto-copies the selection to clipboard on completion.
            SendSnipHotkey();

            // Poll clipboard for up to 45 seconds for a new image
            System.Drawing.Image snip = null;
            for (int i = 0; i < 225; i++)
            {
                await System.Threading.Tasks.Task.Delay(200);
                this.Invoke((Action)(() =>
                {
                    try { if (Clipboard.ContainsImage()) snip = Clipboard.GetImage(); } catch { }
                }));
                if (snip != null) break;
            }

            // Restore window regardless of outcome
            this.Invoke((Action)(() => { this.WindowState = FormWindowState.Normal; }));

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
            catch { }

            if (string.IsNullOrEmpty(uploadedUrl))
            {
                PostSnipResult(null, "upload");
                return;
            }

            PostSnipResult(uploadedUrl, null);
        }

        private void PostSnipResult(string url, string error)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Snip] PostSnipResult url=" + url + " error=" + error);
                string urlJson = url == null ? "null" : "\"" + url.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                string errJson = error == null ? "null" : "\"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                _webView.CoreWebView2.PostWebMessageAsString(
                    "{\"type\":\"codex-snip-done\",\"url\":" + urlJson + ",\"error\":" + errJson + "}");
            }
            catch { }
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

        private void OnMainFormResize(object sender, EventArgs e)
        {
            LayoutDualChatPanel();
        }

        private void LayoutDualChatPanel()
        {
            bool shouldShow = _dualChatHostEnabled && (IsChatPage() || IsBigscreenPage());
            if (!shouldShow)
            {
                _dualChatPanel.Visible = false;
                return;
            }

            Rectangle webViewBounds = _webView.Bounds;
            int top = webViewBounds.Top;
            int bottom = webViewBounds.Bottom;
            int width = Math.Max(320, (webViewBounds.Width - 14) / 2);
            int height = Math.Max(0, bottom - top);
            int left = webViewBounds.Right - width;

            if (IsChatPage())
            {
                if (!_dualChatLayoutActive || _dualChatPaneWidth <= 0 || _dualChatPaneHeight <= 0)
                {
                    _dualChatPanel.Visible = false;
                    return;
                }

                double zoomFactor = _browserReady ? ClampZoom(_webView.ZoomFactor) : ClampZoom(_state.ZoomFactor);
                left = webViewBounds.Left + (int)Math.Round(_dualChatPaneLeft * zoomFactor);
                top = webViewBounds.Top + (int)Math.Round(_dualChatPaneTop * zoomFactor);
                width = Math.Max(0, (int)Math.Round(_dualChatPaneWidth * zoomFactor));
                height = Math.Max(0, (int)Math.Round(_dualChatPaneHeight * zoomFactor));
            }
            else if (IsBigscreenPage())
            {
                int fullHeight = bottom - top;
                int cappedHeight = (int)Math.Round(fullHeight * 0.60);
                top = bottom - cappedHeight;
                height = Math.Max(0, bottom - top);
                left = webViewBounds.Right - width;
            }

            _dualChatPanel.Bounds = new Rectangle(left, top, width, height);
            _dualChatPanel.Visible = height > 0;
            _dualChatPanel.BringToFront();

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

            if (!_browserReady || _dualChatView.CoreWebView2 == null)
            {
                LayoutDualChatPanel();
                return;
            }

            if (!_dualChatHostEnabled || (!IsChatPage() && !IsBigscreenPage()))
            {
                if (!IsChatPage())
                {
                    _dualChatInputTop = 0;
                    _dualChatPaneLeft = 0;
                    _dualChatPaneTop = 0;
                    _dualChatPaneWidth = 0;
                    _dualChatPaneHeight = 0;
                    _dualChatLayoutActive = false;
                }
                _dualChatPanel.Visible = false;
                _dualChatView.Visible = false;
                _dualChatEmptyLabel.Visible = false;
                _dualChatRequestedUrl = null;

                if (_dualChatView.Source == null || !_dualChatView.Source.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    _dualChatView.CoreWebView2.Navigate("about:blank");
                }

                return;
            }

            LayoutDualChatPanel();
            _dualChatPanel.Visible = true;

            if (_dualChatHostAvailable && !string.IsNullOrWhiteSpace(chatUrl))
            {
                bool requestedUrlChanged = !string.Equals(_dualChatRequestedUrl, chatUrl, StringComparison.OrdinalIgnoreCase);
                bool needsInitialNavigation = _dualChatView.Source == null ||
                    string.IsNullOrWhiteSpace(_dualChatView.Source.AbsoluteUri) ||
                    _dualChatView.Source.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase);

                _dualChatRequestedUrl = chatUrl;
                _dualChatEmptyLabel.Visible = false;
                _dualChatView.Visible = true;

                if (requestedUrlChanged || needsInitialNavigation)
                {
                    _dualChatView.CoreWebView2.Navigate(chatUrl);
                }
            }
            else
            {
                _dualChatRequestedUrl = null;
                _dualChatView.Visible = false;
                _dualChatEmptyLabel.Text = _dualChatEmptyMessage;
                _dualChatEmptyLabel.Visible = true;

                if (_dualChatView.Source == null || !_dualChatView.Source.AbsoluteUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    _dualChatView.CoreWebView2.Navigate("about:blank");
                }
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
                if (string.IsNullOrWhiteSpace(url))
                {
                    return;
                }

                NavigateToUrl(url);
                SetStatus("Opened bigscreen selection. Click Chat to return to chat-only view.");
            }
            catch
            {
                // Ignore malformed page messages.
            }
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
                if (!string.Equals(messageType, "embedsState", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                List<BigscreenEmbedState> embeds = new List<BigscreenEmbedState>();
                object[] items = message.ContainsKey("items") ? message["items"] as object[] : null;
                if (items != null)
                {
                    foreach (object item in items)
                    {
                        Dictionary<string, object> values = item as Dictionary<string, object>;
                        if (values == null)
                        {
                            continue;
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

                _latestBigscreenEmbeds = embeds;
                UpdateBigscreenEmbeds(embeds);
                NotifyDualChatSourceChanged();
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
            if (!_browserReady || _webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                Dictionary<string, object> payload = BuildDualChatSourcePayload();

                // On bigscreen, auto-enable dual chat host since there is no JS UI to toggle it.
                // On chat page, only apply host state when the user has explicitly enabled it.
                if (_dualChatHostEnabled || IsBigscreenPage())
                {
                    bool available = false;
                    if (payload.ContainsKey("available"))
                    {
                        object availableValue = payload["available"];
                        if (availableValue is bool)
                        {
                            available = (bool)availableValue;
                        }
                        else
                        {
                            bool.TryParse(Convert.ToString(availableValue), out available);
                        }
                    }

                    string chatUrl = payload.ContainsKey("chatUrl") ? Convert.ToString(payload["chatUrl"]) : string.Empty;
                    string emptyMessage = payload.ContainsKey("reason") && !available
                        ? "No live stream chat is available for the current stream selection."
                        : string.Empty;
                    ApplyDualChatHostState(true, available, chatUrl, emptyMessage);
                }

                if (!IsChatPage())
                {
                    return;
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                _webView.CoreWebView2.PostWebMessageAsJson(serializer.Serialize(payload));
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
                { "available", false }
            };

            BigscreenEmbedState selectedEmbed = NormalizeBigscreenEmbedState(GetSelectedBigscreenEmbed());
            if (selectedEmbed == null)
            {
                payload["reason"] = "no-selection";
                return payload;
            }

            string chatUrl = BuildStreamChatUrl(selectedEmbed);
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
                return payload;
            }

            payload["available"] = true;
            payload["chatUrl"] = chatUrl;
            return payload;
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

            NavigateToUrl(url);
            SetStatus("Opened bigscreen selection. Click Chat to return to chat-only view.");
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
            NavigateToUrl(BigscreenBarUrl);
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

        private BigscreenEmbedState GetSelectedBigscreenEmbed()
        {
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

        private static string BuildStreamChatUrl(BigscreenEmbedState embed)
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
                    return BuildYouTubeChatUrl(normalizedEmbed.MediaId);
                case "kick":
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

        private static string BuildYouTubeChatUrl(string mediaId)
        {
            string videoId = GetPrimaryMediaPathSegment(mediaId);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            return "https://www.youtube.com/live_chat?v="
                + Uri.EscapeDataString(videoId)
                + "&is_popout=1";
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

            return "https://player.kick.com/" + Uri.EscapeDataString(channel) + "?autoplay=true";
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

            _pendingNavigation = uri.AbsoluteUri;
            UpdateAddressBox(_pendingNavigation);
            UpdateBigscreenBarVisibility();

            if (_browserReady && _webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Navigate(_pendingNavigation);
            }
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
            html.Append("document.addEventListener('click',function(e){const link=e.target.closest('a.embed-link');if(!link){return;}e.preventDefault();if(host){host.postMessage({type:'openEmbed',url:link.dataset.url||''});}});");
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
            catch
            {
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
            catch
            {
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

                    // Selectors for the right-side chat container on destiny.gg bigscreen.
                    // We want the first element that represents where the chat region starts.
                    const CHAT_SELECTORS = [
                        '#chat-wrap',
                        '#chat',
                        '.chat-wrap',
                        '.chat',
                        '#sidebar',
                        '.sidebar'
                    ];

                    let reported = false;

                    function reportChatTop() {
                        let chatTop = 0;
                        for (let i = 0; i < CHAT_SELECTORS.length; i++) {
                            const el = document.querySelector(CHAT_SELECTORS[i]);
                            if (el) {
                                const rect = el.getBoundingClientRect();
                                if (rect.height > 10) {
                                    chatTop = Math.max(0, Math.round(rect.top));
                                    break;
                                }
                            }
                        }

                        host.postMessage({ type: 'codex-bigscreen-layout', chatTop: chatTop });
                    }

                    function tryReport() {
                        for (let i = 0; i < CHAT_SELECTORS.length; i++) {
                            const el = document.querySelector(CHAT_SELECTORS[i]);
                            if (el && el.getBoundingClientRect().height > 10) {
                                reportChatTop();
                                reported = true;
                                return true;
                            }
                        }
                        return false;
                    }

                    // Report on resize too (window resize changes layout).
                    window.addEventListener('resize', reportChatTop);

                    if (document.readyState === 'loading') {
                        document.addEventListener('DOMContentLoaded', () => {
                            if (!tryReport()) {
                                // Wait for the layout to settle after initial render.
                                window.requestAnimationFrame(() => {
                                    window.requestAnimationFrame(tryReport);
                                });
                            }
                        }, { once: true });
                    } else {
                        if (!tryReport()) {
                            window.requestAnimationFrame(() => {
                                window.requestAnimationFrame(tryReport);
                            });
                        }
                    }

                    window.addEventListener('load', () => {
                        if (!reported) {
                            tryReport();
                        }
                        // Report again after load in case lazy-rendered elements shifted layout.
                        setTimeout(reportChatTop, 500);
                    });
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
                if (await TogglePlaybackWithScriptAsync())
                {
                    return;
                }

                if (_isPlaybackPaused)
                {
                    _webView.CoreWebView2.Resume();
                    _isPlaybackPaused = false;
                    _pauseButton.Text = "||";
                    return;
                }

                bool suspended = await _webView.CoreWebView2.TrySuspendAsync();
                if (suspended)
                {
                    _isPlaybackPaused = true;
                    _pauseButton.Text = ">";
                }
            }
            catch
            {
                // Best-effort playback control.
            }
        }

        private async System.Threading.Tasks.Task<bool> TogglePlaybackWithScriptAsync()
        {
            string resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                (() => {
                    const videos = Array.from(document.querySelectorAll('video'));
                    if (!videos.length) {
                        return { handled: false, paused: false };
                    }

                    const shouldPause = videos.some((video) => !video.paused);
                    if (shouldPause) {
                        videos.forEach((video) => {
                            try {
                                video.pause();
                            } catch (error) {
                            }
                        });
                        return { handled: true, paused: true };
                    }

                    videos.forEach((video) => {
                        try {
                            const playPromise = video.play();
                            if (playPromise && typeof playPromise.catch === 'function') {
                                playPromise.catch(() => {});
                            }
                        } catch (error) {
                        }
                    });

                    return { handled: true, paused: false };
                })();
            ");

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> payload = serializer.Deserialize<Dictionary<string, object>>(resultJson);
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
