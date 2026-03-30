using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FoulzExternal.features.games.universal.spotify
{
    public partial class SpotifyOverlay : Window
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const uint WM_APPCOMMAND = 0x0319;
        private static readonly IntPtr APPCOMMAND_MEDIA_PLAY_PAUSE = (IntPtr)(14 << 16);
        private static readonly IntPtr APPCOMMAND_MEDIA_NEXTTRACK = (IntPtr)(11 << 16);
        private static readonly IntPtr APPCOMMAND_MEDIA_PREVIOUSTRACK = (IntPtr)(12 << 16);

        private readonly DispatcherTimer _timer;
        private IntPtr _spotifyHwnd;

        private static SpotifyOverlay? _instance;

        public static void Launch()
        {
            if (_instance != null && _instance.IsLoaded)
            {
                _instance.Activate();
                return;
            }
            _instance = new SpotifyOverlay();
            _instance.Show();
        }

        public SpotifyOverlay()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Update();
            _timer.Start();

            // position at top-left of screen initially
            Left = 20;
            Top = 20;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Update()
        {
            _spotifyHwnd = IntPtr.Zero;
            uint spotifyPid = 0;

            foreach (var p in Process.GetProcessesByName("Spotify"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    spotifyPid = (uint)p.Id;
                    break;
                }
            }

            if (spotifyPid == 0)
            {
                SetDisconnected("Not found");
                return;
            }

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var cls = new StringBuilder(256);
                GetClassName(hWnd, cls, 256);
                if (!cls.ToString().Contains("Chrome_WidgetWin_1")) return true;
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == spotifyPid) { _spotifyHwnd = hWnd; return false; }
                return true;
            }, IntPtr.Zero);

            if (_spotifyHwnd == IntPtr.Zero)
            {
                SetDisconnected("Not found");
                return;
            }

            var title = new StringBuilder(512);
            GetWindowText(_spotifyHwnd, title, 512);
            string windowTitle = title.ToString();

            statusDot.Fill = new SolidColorBrush(Color.FromRgb(0x1D, 0xB9, 0x54));

            if (string.IsNullOrEmpty(windowTitle) || windowTitle == "Spotify")
            {
                txtStatus.Text = "Paused";
                txtSong.Text = "Nothing Playing";
                txtArtist.Text = "";
            }
            else
            {
                int dash = windowTitle.IndexOf(" - ", StringComparison.Ordinal);
                if (dash >= 0)
                {
                    txtStatus.Text = "Playing";
                    txtArtist.Text = windowTitle.Substring(0, dash);
                    txtSong.Text = windowTitle.Substring(dash + 3);
                }
                else
                {
                    txtStatus.Text = "Playing";
                    txtSong.Text = windowTitle;
                    txtArtist.Text = "";
                }
            }
        }

        private void SetDisconnected(string reason)
        {
            statusDot.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            txtStatus.Text = reason;
            txtSong.Text = "-";
            txtArtist.Text = "-";
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_spotifyHwnd != IntPtr.Zero)
                PostMessage(_spotifyHwnd, WM_APPCOMMAND, IntPtr.Zero, APPCOMMAND_MEDIA_PREVIOUSTRACK);
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_spotifyHwnd != IntPtr.Zero)
                PostMessage(_spotifyHwnd, WM_APPCOMMAND, IntPtr.Zero, APPCOMMAND_MEDIA_PLAY_PAUSE);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_spotifyHwnd != IntPtr.Zero)
                PostMessage(_spotifyHwnd, WM_APPCOMMAND, IntPtr.Zero, APPCOMMAND_MEDIA_NEXTTRACK);
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _instance = null;
            base.OnClosed(e);
        }
    }
}
