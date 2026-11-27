using System.Text.Json;
using System.Text.RegularExpressions;

namespace VibepolloUi;

sealed class MainPage : Page
{
    static readonly Regex ProgressLine = new(@"^\[(\d+)%\]\s*(.*)$", RegexOptions.Compiled);

    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Cyan() };
    readonly TextBlock _plan = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
    readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Value = 0 };
    readonly ComboBox _versions = new() { MinWidth = 360, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox _cache = new() { MinWidth = 280 };
    readonly CheckBox _silent = new() { Content = "Silent Windows install (/qn, no restart)", IsChecked = false };
    readonly CheckBox _stableOnly = new() { Content = "Stable only", IsChecked = false };
    readonly Button _check = new() { Content = "Check releases" };
    readonly Button _install = new() { Content = "Install selected" };
    readonly Button _rollback = new() { Content = "Roll back cached" };
    readonly Button _vigem = new() { Content = "Install ViGEmBus 1.22.0" };
    JsonDocument? _doc;
    bool _busy;
    bool _filling;
    string? _preferTag;

    public MainPage()
    {
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 16, 24));
        var root = new ScrollViewer
        {
            Content = Build()
        };
        Content = root;
        _check.Click += async (_, _) => await RefreshAsync();
        _stableOnly.Checked += (_, _) => Fill();
        _stableOnly.Unchecked += (_, _) => Fill();
        _versions.SelectionChanged += (_, _) =>
        {
            if (_versions.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                _preferTag = tag;
            ShowSelection();
        };
        _install.Click += async (_, _) => await InstallAsync();
        _rollback.Click += async (_, _) => await RollbackAsync();
        _vigem.Click += async (_, _) => await VigemAsync();
        Loaded += async (_, _) => await RefreshAsync();
    }

    UIElement Build()
    {
        var stack = new StackPanel { Padding = new Thickness(22), Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = "VIBEPOLLO UPDATER",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Cyan(),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Unofficial. Official Nonary packages only. SHA-256 is checked before anything is installed.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 160, 184)),
        });
        stack.Children.Add(_status);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(_check);
        row.Children.Add(_stableOnly);
        row.Children.Add(_silent);
        stack.Children.Add(row);
        stack.Children.Add(_plan);
        stack.Children.Add(_versions);
        var notesBox = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 24, 34)),
            Padding = new Thickness(12),
            Child = _notes,
            MinHeight = 220,
        };
        stack.Children.Add(notesBox);
        stack.Children.Add(_bar);
        var act = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        act.Children.Add(_install);
        act.Children.Add(_cache);
        act.Children.Add(_rollback);
        act.Children.Add(_vigem);
        stack.Children.Add(act);
        _install.IsEnabled = false;
        _rollback.IsEnabled = false;
        return stack;
    }

    async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        _check.IsEnabled = false;
        _install.IsEnabled = false;
        SetStatus("Checking GitHub releases. Nothing is installed.");
        try
        {
            var run = await Task.Run(() => Engine.Run(new[] { "--json" }, 25000));
            if (run.code != 0)
            {
                SetStatus("Check failed. " + Brief(run.stderr, run.stdout));
                return;
            }
            _doc?.Dispose();
            _doc = JsonDocument.Parse(run.stdout);
            Fill();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            _busy = false;
            _check.IsEnabled = true;
            ShowSelection();
        }
    }

    void Fill()
    {
        if (_doc is null) return;
        _filling = true;
        try
        {
            FillCore();
        }
        finally
        {
            _filling = false;
        }
        ShowSelection();
    }

    void FillCore()
    {
        var root = _doc!.RootElement;
        var installed = Str(root, "installed");
        var service = Str(root, "service");
        var stacked = false;
        if (root.TryGetProperty("windows_installs", out var wins) && wins.ValueKind == JsonValueKind.Object)
            stacked = wins.TryGetProperty("stacked", out var s) && s.ValueKind == JsonValueKind.True;
        var backups = root.TryGetProperty("config_backups", out var b) && b.ValueKind == JsonValueKind.Array ? b.GetArrayLength() : 0;
        var newest = backups > 0 ? Str(b[0], "name") : "none";
        SetStatus("Installed: " + (string.IsNullOrEmpty(installed) ? "not installed" : installed)
            + "    " + ChannelLine("stable")
            + "    " + ChannelLine("latest")
            + "    Service: " + service
            + (stacked ? "    TWO INSTALLS REGISTERED" : "")
            + "    Config backups: " + backups + " (newest " + newest + ")");

        var stableTag = ChannelTag("stable");
        var latestTag = ChannelTag("latest");
        var keep = _preferTag;
        _versions.Items.Clear();
        var select = -1;
        var stableIndex = -1;
        var updateIndex = -1;
        var reinstallIndex = -1;
        if (root.TryGetProperty("releases", out var rels))
        {
            foreach (var rel in rels.EnumerateArray())
            {
                var pre = rel.TryGetProperty("prerelease", out var preEl) && preEl.ValueKind == JsonValueKind.True;
                if (_stableOnly.IsChecked == true && pre)
                    continue;
                var tag = Str(rel, "tag");
                var action = Str(rel, "action");
                var idx = _versions.Items.Count;
                _versions.Items.Add(new ComboBoxItem
                {
                    Content = tag + "   " + Role(tag, pre, stableTag, latestTag) + "   " + action,
                    Tag = tag,
                });
                if (tag == keep)
                    select = idx;
                if (tag == stableTag && stableIndex < 0)
                    stableIndex = idx;
                if (action == "update" && updateIndex < 0)
                    updateIndex = idx;
                if (action == "reinstall" && reinstallIndex < 0)
                    reinstallIndex = idx;
            }
        }
        if (_versions.Items.Count > 0)
            _versions.SelectedIndex = select >= 0 ? select
                : updateIndex >= 0 ? updateIndex
                : reinstallIndex >= 0 ? reinstallIndex
                : stableIndex >= 0 ? stableIndex
                : 0;

        _cache.Items.Clear();
        if (root.TryGetProperty("cached_rollbacks", out var cache))
        {
            foreach (var item in cache.EnumerateArray())
                _cache.Items.Add(item.GetString() ?? "");
            if (_cache.Items.Count > 0)
                _cache.SelectedIndex = 0;
        }
        _rollback.IsEnabled = _cache.Items.Count > 0;

        if (root.TryGetProperty("vigembus", out var vg))
        {
            var summary = Str(vg, "summary");
            _vigem.Content = "ViGEmBus: " + summary;
            _vigem.IsEnabled = true;
        }
        else
        {
            _vigem.Content = "ViGEmBus is Windows-only";
            _vigem.IsEnabled = false;
        }
    }

    void ShowSelection()
    {
        if (_filling || _doc is null) return;
        var tag = (_versions.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrEmpty(tag)) return;
        foreach (var rel in _doc.RootElement.GetProperty("releases").EnumerateArray())
        {
            if (Str(rel, "tag") != tag) continue;
            var installed = Str(_doc.RootElement, "installed");
            var action = Str(rel, "action");
            var sha = rel.TryGetProperty("sha256_present", out var present) && present.ValueKind == JsonValueKind.True;
            var from = string.IsNullOrEmpty(installed) ? "not installed" : installed;
            _plan.Text = "Plan: " + from + "  →  " + tag + "    " + action.ToUpperInvariant()
                + (sha ? "    SHA-256 present" : "    SHA-256 MISSING, install stays off");
            _plan.Foreground = action == "downgrade"
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 184, 75))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 214, 228, 240));
            _notes.Text = Str(rel, "name") + "\n"
                + Str(rel, "published_at") + "    " + Str(rel, "asset") + "    " + Str(rel, "size_mb") + " MiB\n"
                + "SHA-256 " + Str(rel, "sha256") + "\n"
                + Str(rel, "html_url") + "\n\n"
                + Str(rel, "notes");
            var supported = _doc.RootElement.TryGetProperty("platform_supported", out var sup) && sup.ValueKind == JsonValueKind.True;
            _install.IsEnabled = supported && sha && action is "install" or "update" or "reinstall" or "downgrade";
            _install.Content = action switch
            {
                "downgrade" => "Install older version",
                "reinstall" => "Reinstall this version",
                "install" => "Install",
                _ => "Update",
            };
            return;
        }
    }

    async Task InstallAsync()
    {
        var tagPick = (_versions.SelectedItem as ComboBoxItem)?.Tag as string;
        if (_busy || _doc is null || string.IsNullOrEmpty(tagPick)) return;
        JsonElement? chosen = null;
        foreach (var rel in _doc.RootElement.GetProperty("releases").EnumerateArray())
        {
            if (Str(rel, "tag") == tagPick) { chosen = rel; break; }
        }
        if (chosen is null) return;
        var relv = chosen.Value;
        var tag = Str(relv, "tag");
        var action = Str(relv, "action");
        var dialog = new ContentDialog
        {
            Title = action == "downgrade" ? "Install an older Vibepollo?" : "Install Vibepollo?",
            Content = "Download, verify SHA-256, then run the official installer.\n\n"
                + Str(_doc.RootElement, "installed") + "  →  " + tag + "  (" + action + ")\n"
                + Str(relv, "asset")
                + (_silent.IsChecked == true ? "\n\nSilent: /qn /norestart" : ""),
            PrimaryButtonText = "Install",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var args = new List<string> { "--install-version", tag };
        if (action == "downgrade") args.Add("--allow-downgrade");
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, "Installing " + tag);
    }

    async Task RollbackAsync()
    {
        var name = _cache.SelectedItem as string;
        if (_busy || string.IsNullOrWhiteSpace(name)) return;
        var dialog = new ContentDialog
        {
            Title = "Roll back from cache?",
            Content = "Reinstall the cached package already on this machine:\n\n" + name,
            PrimaryButtonText = "Roll back",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var args = new List<string> { "--rollback-cached", name };
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, "Rolling back " + name);
    }

    async Task VigemAsync()
    {
        if (_busy) return;
        var dialog = new ContentDialog
        {
            Title = "Install ViGEmBus 1.22.0?",
            Content = "Download the pinned official installer, check its SHA-256, then run it. An in-place upgrade may need the setup twice.",
            PrimaryButtonText = "Install",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var args = new List<string> { "--install-vigembus" };
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, "Installing ViGEmBus");
    }

    async Task RunJobAsync(IReadOnlyList<string> args, string start)
    {
        _busy = true;
        _install.IsEnabled = false;
        _rollback.IsEnabled = false;
        _check.IsEnabled = false;
        _vigem.IsEnabled = false;
        _bar.Value = 0;
        SetStatus(start);
        try
        {
            var run = await Task.Run(() => Engine.Run(args, 20 * 60 * 1000, line =>
            {
                var m = ProgressLine.Match(line);
                if (!m.Success) return;
                var pct = int.Parse(m.Groups[1].Value);
                var msg = m.Groups[2].Value;
                DispatcherQueue.TryEnqueue(() =>
                {
                    _bar.Value = pct;
                    SetStatus(msg);
                });
            }));
            _notes.Text = (run.stdout + "\n" + run.stderr).Trim();
            SetStatus(run.code is 0 or 3010 ? "Finished (exit " + run.code + ")." : "Installer exit " + run.code + ".");
            if (run.code is 0 or 3010) _bar.Value = 100;
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            _busy = false;
            _check.IsEnabled = true;
        }
        await RefreshAsync();
    }

    void SetStatus(string text) => _status.Text = text;

    static string Str(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }

    string ChannelTag(string key)
    {
        if (_doc is null || !_doc.RootElement.TryGetProperty("channels", out var ch)) return "";
        if (!ch.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return "";
        return Str(v, "tag");
    }

    string ChannelLine(string key)
    {
        if (_doc is null || !_doc.RootElement.TryGetProperty("channels", out var ch)) return key + " —";
        if (!ch.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return key + " —";
        var tag = Str(v, "tag");
        var action = Str(v, "action");
        return string.IsNullOrEmpty(tag) ? key + " —" : key + " " + tag + " (" + action + ")";
    }

    static string Role(string tag, bool pre, string stableTag, string latestTag)
    {
        var marks = new List<string>();
        if (tag == stableTag) marks.Add("stable");
        if (tag == latestTag) marks.Add("latest");
        if (marks.Count == 0) marks.Add(pre ? "pre" : "older");
        return string.Join("+", marks);
    }

    static string Brief(string stderr, string stdout)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 240 ? text[..240] : text;
    }

    static SolidColorBrush Cyan() => new(Windows.UI.Color.FromArgb(255, 88, 225, 232));
}
