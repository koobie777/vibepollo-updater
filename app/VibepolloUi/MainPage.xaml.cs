using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;

namespace VibepolloUi;

sealed class MainPage : Page
{
    public const string AppVersion = "1.5.0";
    static readonly Regex ProgressLine = new(@"^\[\s*(\d+)%\]\s*(.*)$", RegexOptions.Compiled);

    readonly TextBlock _statusInstalled = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = TextPrimary() };
    readonly TextBlock _statusStable = new() { Foreground = TextMuted() };
    readonly TextBlock _statusLatest = new() { Foreground = TextMuted() };
    readonly TextBlock _statusService = new() { Foreground = TextMuted() };
    readonly TextBlock _statusBackups = new() { Foreground = TextMuted() };
    readonly TextBlock _statusBanner = new() { TextWrapping = TextWrapping.Wrap, Foreground = Cyan(), FontSize = 13 };

    readonly TextBlock _plan = new() { TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    readonly TextBlock _notes = new()
    {
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
        FontFamily = new FontFamily("Cascadia Mono, Consolas, JetBrains Mono, monospace"),
        FontSize = 12,
        Foreground = TextSoft(),
    };
    readonly TextBlock _terminal = new()
    {
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
        FontFamily = new FontFamily("Cascadia Mono, Consolas, JetBrains Mono, monospace"),
        FontSize = 12,
        Foreground = TerminalFg(),
        Text = "Ready. No changes are made until you click Update / Install.",
    };
    readonly ScrollViewer _terminalScroll = new() { MaxHeight = 190, MinHeight = 110 };
    readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Value = 0 };
    readonly ComboBox _versions = new() { MinWidth = 360, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox _cache = new() { MinWidth = 280, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly CheckBox _silent = new() { Content = "Silent Windows install (/qn, no restart)", IsChecked = false };
    readonly CheckBox _stableOnly = new() { Content = "Stable releases only", IsChecked = false };
    readonly Button _check = new() { Content = "Check releases" };
    readonly Button _install = new() { Content = "Install selected", MinWidth = 190 };
    readonly Button _rollback = new() { Content = "Roll back selected build", MinWidth = 190 };
    readonly Button _vigem = new() { Content = "Install ViGEmBus 1.22.0" };
    readonly Button _copyLog = new() { Content = "Copy log" };
    readonly Button _clearLog = new() { Content = "Clear" };
    readonly TextBlock _rollbackHint = new() { TextWrapping = TextWrapping.Wrap, Foreground = TextMuted(), FontSize = 12 };
    readonly StringBuilder _logBuf = new();

    JsonDocument? _doc;
    bool _busy;
    bool _filling;
    string? _preferTag;

    public MainPage()
    {
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 8, 13, 23));
        Content = new ScrollViewer { Content = Build() };

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
        _copyLog.Click += (_, _) =>
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(_terminal.Text);
                Clipboard.SetContent(pkg);
                SetBanner("Terminal log copied to clipboard.");
            }
            catch { }
        };
        _clearLog.Click += (_, _) =>
        {
            _logBuf.Clear();
            _terminal.Text = "Log cleared.";
        };
        Loaded += async (_, _) => await RefreshAsync();
    }

    UIElement Build()
    {
        var page = new StackPanel { Padding = new Thickness(24, 20, 24, 24), Spacing = 14, MaxWidth = 980 };

        // Header
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 4 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        titleRow.Children.Add(new TextBlock
        {
            Text = "VIBEPOLLO UPDATER",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Cyan(),
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleRow.Children.Add(Pill($"v{AppVersion} · PORTABLE", CyanBg(), Cyan()));
        titleStack.Children.Add(titleRow);
        titleStack.Children.Add(new TextBlock
        {
            Text = "Unofficial portable updater for Nonary/Vibepollo. Official packages only — SHA-256 verified before install.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = TextMuted(),
            FontSize = 12.5,
        });
        Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);

        _check.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_check, 1);
        header.Children.Add(_check);
        page.Children.Add(header);

        // Status Overview Card
        var statusBody = new StackPanel { Spacing = 8 };
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        chips.Children.Add(_statusInstalled);
        chips.Children.Add(_statusStable);
        chips.Children.Add(_statusLatest);
        statusBody.Children.Add(chips);
        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        metaRow.Children.Add(_statusService);
        metaRow.Children.Add(_statusBackups);
        statusBody.Children.Add(metaRow);
        statusBody.Children.Add(_statusBanner);
        page.Children.Add(Card("SYSTEM STATUS", statusBody, CardBorder()));

        // Card 1: Update / Install (Primary Action)
        var updateBody = new StackPanel { Spacing = 10 };
        var optRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        optRow.Children.Add(_stableOnly);
        if (OperatingSystem.IsWindows())
            optRow.Children.Add(_silent);
        updateBody.Children.Add(optRow);
        updateBody.Children.Add(_versions);
        updateBody.Children.Add(_plan);

        var notesScroll = new ScrollViewer
        {
            MinHeight = 140,
            MaxHeight = 220,
            Content = _notes,
        };
        updateBody.Children.Add(new Border
        {
            Background = PanelDark(),
            BorderBrush = SubBorder(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = notesScroll,
        });

        updateBody.Children.Add(_bar);

        _install.Background = AccentBtnBg();
        _install.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        _install.BorderBrush = Cyan();
        _install.Padding = new Thickness(18, 9, 18, 9);
        _install.IsEnabled = false;
        var primaryRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        primaryRow.Children.Add(_install);
        primaryRow.Children.Add(new TextBlock
        {
            Text = "Backs up existing config, verifies SHA-256, and runs the official installer.",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TextMuted(),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });
        updateBody.Children.Add(primaryRow);
        page.Children.Add(Card("INSTALL OR UPDATE RELEASE", updateBody, CyanBorder()));

        // Dedicated Terminal Output Card
        var termHeader = new Grid();
        termHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        termHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var termLabel = new TextBlock
        {
            Text = "TERMINAL OUTPUT",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Cyan(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(termLabel, 0);
        termHeader.Children.Add(termLabel);
        var termBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        termBtns.Children.Add(_copyLog);
        termBtns.Children.Add(_clearLog);
        Grid.SetColumn(termBtns, 1);
        termHeader.Children.Add(termBtns);

        _terminalScroll.Content = _terminal;
        var termCard = new StackPanel { Spacing = 8 };
        termCard.Children.Add(termHeader);
        termCard.Children.Add(new Border
        {
            Background = TerminalBg(),
            BorderBrush = SubBorder(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = _terminalScroll,
        });
        page.Children.Add(new Border
        {
            Background = CardBg(),
            BorderBrush = CardBorder(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Child = termCard,
        });

        // Clear Separation Divider before Recovery & Optional Tools
        page.Children.Add(new Border
        {
            Height = 1,
            Background = SubBorder(),
            Margin = new Thickness(0, 6, 0, 2),
        });

        // Card 2: Rollback & Recovery (Clearly separated from Update!)
        var rollbackBody = new StackPanel { Spacing = 10 };
        _rollbackHint.Text = "Reinstall a previously verified build stored in your local rollback cache (last 3 builds kept). Kept separate from Update to prevent accidental downgrades.";
        rollbackBody.Children.Add(_rollbackHint);
        var rbGrid = new Grid { ColumnSpacing = 12 };
        rbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_cache, 0);
        rbGrid.Children.Add(_cache);
        _rollback.BorderBrush = Amber();
        _rollback.Padding = new Thickness(16, 8, 16, 8);
        _rollback.IsEnabled = false;
        Grid.SetColumn(_rollback, 1);
        rbGrid.Children.Add(_rollback);
        rollbackBody.Children.Add(rbGrid);
        page.Children.Add(Card("ROLLBACK & RECOVERY (CACHED BUILDS)", rollbackBody, AmberBorder(), Amber()));

        // Card 3: Optional Driver (Windows only)
        if (OperatingSystem.IsWindows())
        {
            var drvRow = new Grid { ColumnSpacing = 12 };
            drvRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            drvRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var drvText = new TextBlock
            {
                Text = "ViGEmBus v1.22.0 (pinned SHA-256) — optional virtual gamepad bus driver for Xbox 360 / DS4 emulation.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = TextMuted(),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
            };
            Grid.SetColumn(drvText, 0);
            drvRow.Children.Add(drvText);
            Grid.SetColumn(_vigem, 1);
            drvRow.Children.Add(_vigem);
            page.Children.Add(Card("OPTIONAL WINDOWS DRIVER", drvRow, CardBorder()));
        }

        return page;
    }

    static Border Card(string title, UIElement content, Brush border, Brush? titleBrush = null)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = titleBrush ?? TextMuted(),
        });
        stack.Children.Add(content);
        return new Border
        {
            Background = CardBg(),
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Child = stack,
        };
    }

    static Border Pill(string text, Brush bg, Brush fg) => new()
    {
        Background = bg,
        BorderBrush = fg,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(999),
        Padding = new Thickness(10, 3, 10, 3),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = fg,
        },
    };

    void LogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        _logBuf.Append('[').Append(stamp).Append("] ").AppendLine(line.TrimEnd());
        _terminal.Text = _logBuf.ToString().TrimEnd();
        _terminalScroll.ChangeView(null, _terminalScroll.ScrollableHeight + 200, null);
    }

    void LogBlock(string text)
    {
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(line))
                LogLine(line);
        }
    }

    async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        _check.IsEnabled = false;
        _install.IsEnabled = false;
        SetBanner("Checking GitHub releases… nothing is installed yet.");
        try
        {
            var run = await Task.Run(() => Engine.Run(new[] { "--json" }, 25000));
            if (run.code != 0)
            {
                var err = Brief(run.stderr, run.stdout);
                SetBanner("Release check failed: " + err);
                LogLine("ERROR: release check failed: " + err);
                return;
            }
            _doc?.Dispose();
            _doc = JsonDocument.Parse(run.stdout);
            Fill();
            SetBanner("Up to date with GitHub release list. Pick a release and click Update / Install when ready.");
        }
        catch (Exception ex)
        {
            SetBanner(ex.Message);
            LogLine("ERROR: " + ex.Message);
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
        try { FillCore(); }
        finally { _filling = false; }
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

        _statusInstalled.Text = "Installed: " + (string.IsNullOrEmpty(installed) ? "not installed" : installed);
        _statusStable.Text = "Stable: " + ChannelSummary("stable");
        _statusLatest.Text = "Latest: " + ChannelSummary("latest");
        _statusService.Text = "Service: " + service + (stacked ? "  ⚠ TWO INSTALLS REGISTERED" : "");
        _statusBackups.Text = $"Config backups: {backups} (newest: {newest})";

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
                    Content = $"{tag}   ·   {Role(tag, pre, stableTag, latestTag)}   ·   {action}",
                    Tag = tag,
                });
                if (tag == keep) select = idx;
                if (tag == stableTag && stableIndex < 0) stableIndex = idx;
                if (action == "update" && updateIndex < 0) updateIndex = idx;
                if (action == "reinstall" && reinstallIndex < 0) reinstallIndex = idx;
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
        _rollbackHint.Text = _cache.Items.Count > 0
            ? $"Cached builds ready for offline rollback ({_cache.Items.Count} available). Kept separate from Update to prevent accidental downgrades."
            : "No cached installers yet. After your first download, up to 3 verified builds stay cached here for 1-click rollback.";

        if (root.TryGetProperty("vigembus", out var vg))
        {
            var summary = Str(vg, "summary");
            _vigem.Content = "ViGEmBus: " + summary;
            _vigem.IsEnabled = true;
        }
        else
        {
            _vigem.Content = "ViGEmBus (Windows only)";
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
            _plan.Text = $"Plan: {from}  →  {tag}   [{action.ToUpperInvariant()}]"
                + (sha ? "   ·   SHA-256 verified" : "   ·   SHA-256 MISSING (install disabled)");
            _plan.Foreground = action == "downgrade" ? Amber() : TextPrimary();
            _notes.Text = $"{Str(rel, "name")}\n"
                + $"Published : {Str(rel, "published_at")}\n"
                + $"Package   : {Str(rel, "asset")} ({Str(rel, "size_mb")} MiB)\n"
                + $"SHA-256   : {Str(rel, "sha256")}\n"
                + $"URL       : {Str(rel, "html_url")}\n\n"
                + Str(rel, "notes");
            var supported = _doc.RootElement.TryGetProperty("platform_supported", out var sup) && sup.ValueKind == JsonValueKind.True;
            _install.IsEnabled = supported && sha && action is "install" or "update" or "reinstall" or "downgrade";
            _install.Content = action switch
            {
                "downgrade" => $"Install older ({tag})",
                "reinstall" => $"Reinstall {tag}",
                "install" => $"Install {tag}",
                _ => $"Update to {tag}",
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
            Title = action == "downgrade" ? "Install an older Vibepollo?" : $"Install Vibepollo {tag}?",
            Content = "Download, verify SHA-256, back up config, and run the official installer.\n\n"
                + $"{(string.IsNullOrEmpty(Str(_doc.RootElement, "installed")) ? "not installed" : Str(_doc.RootElement, "installed"))}  →  {tag}  ({action})\n"
                + Str(relv, "asset")
                + (_silent.IsChecked == true ? "\n\nSilent mode: /qn /norestart" : ""),
            PrimaryButtonText = action == "update" ? "Update now" : "Install",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var args = new List<string> { "--install-version", tag };
        if (action == "downgrade") args.Add("--allow-downgrade");
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, $"Updating Vibepollo to {tag}…");
    }

    async Task RollbackAsync()
    {
        var name = _cache.SelectedItem as string;
        if (_busy || string.IsNullOrWhiteSpace(name)) return;
        var dialog = new ContentDialog
        {
            Title = "Roll back from local cache?",
            Content = $"Reinstall the cached package already on this machine:\n\n{name}\n\nYour current config will be backed up first.",
            PrimaryButtonText = "Roll back",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var args = new List<string> { "--rollback-cached", name };
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, $"Rolling back using {name}…");
    }

    async Task VigemAsync()
    {
        if (_busy) return;
        var dialog = new ContentDialog
        {
            Title = "Install ViGEmBus 1.22.0?",
            Content = "Download the pinned official installer, verify SHA-256, then run it elevated. An in-place upgrade may need running setup twice.",
            PrimaryButtonText = "Install",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var args = new List<string> { "--install-vigembus" };
        if (_silent.IsChecked == true) args.Add("--silent");
        await RunJobAsync(args, "Installing ViGEmBus 1.22.0…");
    }

    async Task RunJobAsync(IReadOnlyList<string> args, string start)
    {
        _busy = true;
        _install.IsEnabled = false;
        _rollback.IsEnabled = false;
        _check.IsEnabled = false;
        _vigem.IsEnabled = false;
        _bar.Value = 0;
        SetBanner(start);
        LogLine("── " + start + " ──");
        try
        {
            var run = await Task.Run(() => Engine.Run(args, 20 * 60 * 1000, line =>
            {
                var m = ProgressLine.Match(line);
                if (m.Success)
                {
                    var pct = int.Parse(m.Groups[1].Value);
                    var msg = m.Groups[2].Value;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _bar.Value = pct;
                        SetBanner(msg);
                    });
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    DispatcherQueue.TryEnqueue(() => LogLine(line));
                }
            }));
            if (!string.IsNullOrWhiteSpace(run.stderr))
                LogBlock(run.stderr);
            if (run.code is 0 or 3010)
            {
                _bar.Value = 100;
                var msg = run.code == 3010 ? "Finished (restart Windows to finish driver setup)." : "Update completed successfully.";
                SetBanner(msg);
                LogLine("OK: " + msg);
            }
            else
            {
                var msg = $"Installer exited with code {run.code}. See Terminal Output below.";
                SetBanner(msg);
                LogLine("FAILED: " + msg);
            }
        }
        catch (Exception ex)
        {
            SetBanner(ex.Message);
            LogLine("ERROR: " + ex.Message);
        }
        finally
        {
            _busy = false;
            _check.IsEnabled = true;
        }
        // Refresh version/channel state WITHOUT clearing Terminal Output.
        var lastBanner = _statusBanner.Text;
        await RefreshAsync();
        SetBanner(lastBanner);
    }

    void SetBanner(string text) => _statusBanner.Text = text;

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

    string ChannelSummary(string key)
    {
        if (_doc is null || !_doc.RootElement.TryGetProperty("channels", out var ch)) return "—";
        if (!ch.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return "—";
        var tag = Str(v, "tag");
        var action = Str(v, "action");
        return string.IsNullOrEmpty(tag) ? "—" : $"{tag} ({action})";
    }

    static string Role(string tag, bool pre, string stableTag, string latestTag)
    {
        var marks = new List<string>();
        if (tag == stableTag) marks.Add("stable");
        if (tag == latestTag) marks.Add("latest");
        if (marks.Count == 0) marks.Add(pre ? "pre-release" : "older");
        return string.Join(" + ", marks);
    }

    static string Brief(string stderr, string stdout)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 240 ? text[..240] : text;
    }

    static SolidColorBrush Cyan() => new(Windows.UI.Color.FromArgb(255, 88, 225, 232));
    static SolidColorBrush CyanBg() => new(Windows.UI.Color.FromArgb(45, 88, 225, 232));
    static SolidColorBrush CyanBorder() => new(Windows.UI.Color.FromArgb(140, 88, 225, 232));
    static SolidColorBrush AccentBtnBg() => new(Windows.UI.Color.FromArgb(255, 18, 92, 118));
    static SolidColorBrush Amber() => new(Windows.UI.Color.FromArgb(255, 236, 186, 86));
    static SolidColorBrush AmberBorder() => new(Windows.UI.Color.FromArgb(110, 236, 186, 86));
    static SolidColorBrush CardBg() => new(Windows.UI.Color.FromArgb(255, 14, 22, 36));
    static SolidColorBrush CardBorder() => new(Windows.UI.Color.FromArgb(255, 34, 52, 78));
    static SolidColorBrush SubBorder() => new(Windows.UI.Color.FromArgb(255, 30, 46, 68));
    static SolidColorBrush PanelDark() => new(Windows.UI.Color.FromArgb(255, 10, 16, 28));
    static SolidColorBrush TerminalBg() => new(Windows.UI.Color.FromArgb(255, 7, 11, 20));
    static SolidColorBrush TerminalFg() => new(Windows.UI.Color.FromArgb(255, 210, 232, 245));
    static SolidColorBrush TextPrimary() => new(Windows.UI.Color.FromArgb(255, 232, 241, 251));
    static SolidColorBrush TextSoft() => new(Windows.UI.Color.FromArgb(255, 206, 222, 238));
    static SolidColorBrush TextMuted() => new(Windows.UI.Color.FromArgb(255, 142, 164, 190));
}
