using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FoulzExternal.features.games.universal.scriptrunner
{
    public partial class ScriptRunnerControl : UserControl
    {
        private readonly DispatcherTimer _outputTimer;
        private bool _suppressDropdownChange;

        public ScriptRunnerControl()
        {
            InitializeComponent();

            _outputTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _outputTimer.Tick += FlushOutput;
            _outputTimer.Start();

            Loaded += (_, _) => RefreshScriptList();
        }

        // ── Output flushing ───────────────────────────────────────────────────

        private void FlushOutput(object? sender, EventArgs e)
        {
            while (ScriptEngine.Output.TryDequeue(out var item))
            {
                var (text, level) = item;
                ConsoleOutput.AppendText(text + "\n");
                ConsoleOutput.ScrollToEnd();
            }

            // Sync run-button color with IsRunning
            RunBtn.Foreground = ScriptEngine.IsRunning
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x44))
                : new SolidColorBrush(Color.FromRgb(0x88, 0xFF, 0x88));
        }

        // ── Toolbar buttons ───────────────────────────────────────────────────

        private void RunBtn_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOutput.Clear();
            ScriptEngine.Run(CodeEditor.Text);
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            ScriptEngine.Stop();
        }

        private void NewBtn_Click(object sender, RoutedEventArgs e)
        {
            _suppressDropdownChange = true;
            ScriptDropdown.SelectedIndex = -1;
            _suppressDropdownChange = false;
            CodeEditor.Clear();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string dir = ScriptEngine.ScriptsDir;
            Directory.CreateDirectory(dir);

            string name = ScriptDropdown.SelectedItem as string ?? "untitled";
            if (string.IsNullOrWhiteSpace(name) || name == "(no scripts)")
                name = "untitled";
            if (!name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
                name += ".lua";

            string path = Path.Combine(dir, name);
            File.WriteAllText(path, CodeEditor.Text, System.Text.Encoding.UTF8);
            RefreshScriptList();
            SelectScript(name);
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            string dir = ScriptEngine.ScriptsDir;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }

        private void ClearConsoleBtn_Click(object sender, RoutedEventArgs e)
        {
            ConsoleOutput.Clear();
        }

        // ── Script list dropdown ──────────────────────────────────────────────

        private void ScriptDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDropdownChange) return;
            if (ScriptDropdown.SelectedItem is string name && name != "(no scripts)")
            {
                string path = Path.Combine(ScriptEngine.ScriptsDir, name);
                if (File.Exists(path))
                    CodeEditor.Text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
        }

        private void RefreshScriptList()
        {
            string dir = ScriptEngine.ScriptsDir;
            Directory.CreateDirectory(dir);

            var files = Directory.GetFiles(dir, "*.lua")
                .Concat(Directory.GetFiles(dir, "*.luau"))
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(x => x)
                .ToList();

            _suppressDropdownChange = true;
            string? prev = ScriptDropdown.SelectedItem as string;
            ScriptDropdown.Items.Clear();
            if (files.Count == 0)
                ScriptDropdown.Items.Add("(no scripts)");
            else
                foreach (var f in files)
                    ScriptDropdown.Items.Add(f);

            if (prev != null && ScriptDropdown.Items.Contains(prev))
                ScriptDropdown.SelectedItem = prev;
            else
                ScriptDropdown.SelectedIndex = files.Count > 0 ? 0 : -1;
            _suppressDropdownChange = false;
        }

        private void SelectScript(string name)
        {
            _suppressDropdownChange = true;
            if (ScriptDropdown.Items.Contains(name))
                ScriptDropdown.SelectedItem = name;
            _suppressDropdownChange = false;
        }
    }
}
