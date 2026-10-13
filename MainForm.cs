using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Text;

namespace PortScanner.WinForms;

public sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(23, 40, 65);
    private static readonly Color Accent = Color.FromArgb(39, 104, 203);
    private static readonly Color Muted = Color.FromArgb(87, 103, 123);
    private static readonly Color Canvas = Color.FromArgb(244, 247, 251);

    private const int MaxPortsPerScan = 4096;

    private readonly TextBox _hostInput = new();
    private readonly NumericUpDown _startPortInput = new();
    private readonly NumericUpDown _endPortInput = new();
    private readonly RadioButton _rangeModeButton = new();
    private readonly RadioButton _customModeButton = new();
    private readonly TextBox _customPortsInput = new();
    private readonly NumericUpDown _timeoutInput = new();
    private readonly CheckBox _openOnlyCheckbox = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _exportButton = new();
    private readonly DataGridView _resultGrid = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly List<PortScanResult> _results = new();

    private CancellationTokenSource? _scanCancellation;
    private int _scanGeneration;
    private int _completed;
    private int _open;
    private int _closed;
    private int _timedOut;
    private int _other;
    private int _total;
    private bool _scanning;

    public MainForm()
    {
        Text = "TCP Port Scanner | Windows Desktop";
        MinimumSize = new Size(890, 640);
        Size = new Size(1020, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Canvas;
        ForeColor = Navy;

        BuildInterface();

        _startButton.Click += async (_, _) => await StartScanAsync();
        _cancelButton.Click += (_, _) => CancelScan();
        _exportButton.Click += (_, _) => ExportResults();
        _openOnlyCheckbox.CheckedChanged += (_, _) => RefreshResultGrid();
        _rangeModeButton.CheckedChanged += (_, _) => UpdatePortModeControls();
        _customModeButton.CheckedChanged += (_, _) => UpdatePortModeControls();
        FormClosing += (_, _) => _scanCancellation?.Cancel();
        AcceptButton = _startButton;
    }

    private void BuildInterface()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(page);

        var header = new TableLayoutPanel
        {
            BackColor = Navy,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 15, 24, 13),
            Margin = Padding.Empty
        };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        header.Controls.Add(new Label
        {
            Text = "TCP PORT SCANNER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "Check TCP connectivity on devices you own or have permission to test.",
            ForeColor = Color.FromArgb(201, 216, 234),
            Font = new Font("Segoe UI", 9.5f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 1);
        page.Controls.Add(header, 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 18, 20, 20),
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(content, 0, 1);
        content.Controls.Add(BuildSettingsGroup(), 0, 0);
        content.Controls.Add(BuildProgressArea(), 0, 1);
        content.Controls.Add(BuildResultsGroup(), 0, 2);
    }

    private GroupBox BuildSettingsGroup()
    {
        var group = new GroupBox
        {
            Text = "  Scan settings  ",
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 9),
            BackColor = Color.White,
            ForeColor = Navy
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,
            Margin = Padding.Empty
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        group.Controls.Add(fields);

        fields.Controls.Add(MakeFieldLabel("Hostname / IP address"), 0, 0);
        fields.Controls.Add(MakeFieldLabel("Start port"), 1, 0);
        fields.Controls.Add(MakeFieldLabel("End port"), 2, 0);
        fields.Controls.Add(MakeFieldLabel("Timeout (ms)"), 3, 0);

        _hostInput.Text = "127.0.0.1";
        _hostInput.BorderStyle = BorderStyle.FixedSingle;
        _hostInput.Font = new Font("Segoe UI", 10);
        _hostInput.Dock = DockStyle.Fill;
        _hostInput.Margin = new Padding(3, 2, 15, 6);
        fields.Controls.Add(_hostInput, 0, 1);

        ConfigureNumber(_startPortInput, 1, 65535, 1);
        ConfigureNumber(_endPortInput, 1, 65535, 1024);
        ConfigureNumber(_timeoutInput, 100, 5000, 700);
        fields.Controls.Add(_startPortInput, 1, 1);
        fields.Controls.Add(_endPortInput, 2, 1);
        fields.Controls.Add(_timeoutInput, 3, 1);

        var modeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(2, 4, 0, 0),
            Margin = Padding.Empty
        };
        _rangeModeButton.Text = "Port range";
        _rangeModeButton.AutoSize = true;
        _rangeModeButton.Checked = true;
        _rangeModeButton.Margin = new Padding(0, 4, 18, 0);
        _customModeButton.Text = "Custom ports";
        _customModeButton.AutoSize = true;
        _customModeButton.Margin = new Padding(0, 4, 15, 0);
        modeRow.Controls.Add(_rangeModeButton);
        modeRow.Controls.Add(_customModeButton);
        modeRow.Controls.Add(new Label
        {
            Text = "Separate ports with commas; use hyphens for short ranges.",
            AutoSize = true,
            ForeColor = Muted,
            Margin = new Padding(0, 6, 0, 0)
        });
        fields.Controls.Add(modeRow, 0, 2);
        fields.SetColumnSpan(modeRow, 4);

        _customPortsInput.Dock = DockStyle.Fill;
        _customPortsInput.Font = new Font("Segoe UI", 10);
        _customPortsInput.BorderStyle = BorderStyle.FixedSingle;
        _customPortsInput.Margin = new Padding(3, 2, 12, 7);
        _customPortsInput.PlaceholderText = "Custom ports, e.g. 22, 80, 443, 8000-8005";
        _customPortsInput.AccessibleName = "Custom TCP ports";
        fields.Controls.Add(_customPortsInput, 0, 3);
        fields.SetColumnSpan(_customPortsInput, 4);
        UpdatePortModeControls();

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 7, 0, 0),
            Margin = Padding.Empty
        };
        _startButton.Text = "Start scan";
        StyleButton(_startButton, Accent, Color.White, 128);
        _cancelButton.Text = "Cancel";
        StyleButton(_cancelButton, Color.FromArgb(224, 229, 237), Navy, 100);
        _cancelButton.Enabled = false;
        _openOnlyCheckbox.Text = "Show open ports only";
        _openOnlyCheckbox.Checked = false;
        _openOnlyCheckbox.AutoSize = true;
        _openOnlyCheckbox.Margin = new Padding(20, 8, 8, 0);
        _openOnlyCheckbox.ForeColor = Navy;
        buttonRow.Controls.AddRange(new Control[] { _startButton, _cancelButton, _openOnlyCheckbox });
        var scopeNote = new Label
        {
            Text = $"Maximum {MaxPortsPerScan:N0} ports per scan",
            AutoSize = true,
            ForeColor = Muted,
            Margin = new Padding(18, 9, 0, 0)
        };
        buttonRow.Controls.Add(scopeNote);
        fields.Controls.Add(buttonRow, 0, 4);
        fields.SetColumnSpan(buttonRow, 4);
        return group;
    }

    private Control BuildProgressArea()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4, 7, 4, 11)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        _statusLabel.Text = "Ready. Enter a host and choose a TCP port range or custom list.";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Muted;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _progress.Dock = DockStyle.Fill;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Value = 0;
        panel.Controls.Add(_statusLabel, 0, 0);
        panel.Controls.Add(_progress, 0, 1);
        return panel;
    }

    private GroupBox BuildResultsGroup()
    {
        var group = new GroupBox
        {
            Text = "  Scan results  ",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ForeColor = Navy,
            Padding = new Padding(12, 12, 12, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        group.Controls.Add(layout);

        _resultGrid.Dock = DockStyle.Fill;
        _resultGrid.BackgroundColor = Color.White;
        _resultGrid.BorderStyle = BorderStyle.None;
        _resultGrid.AllowUserToAddRows = false;
        _resultGrid.AllowUserToDeleteRows = false;
        _resultGrid.AllowUserToResizeRows = false;
        _resultGrid.ReadOnly = true;
        _resultGrid.MultiSelect = false;
        _resultGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resultGrid.RowHeadersVisible = false;
        _resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _resultGrid.EnableHeadersVisualStyles = false;
        _resultGrid.ColumnHeadersHeight = 37;
        _resultGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _resultGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 239, 249);
        _resultGrid.ColumnHeadersDefaultCellStyle.ForeColor = Navy;
        _resultGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        _resultGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(208, 226, 255);
        _resultGrid.DefaultCellStyle.SelectionForeColor = Navy;
        _resultGrid.DefaultCellStyle.ForeColor = Navy;
        _resultGrid.GridColor = Color.FromArgb(231, 235, 241);
        _resultGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 254);
        _resultGrid.RowTemplate.Height = 29;
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Port", HeaderText = "TCP port", FillWeight = 18,
            ValueType = typeof(int)
        });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status", HeaderText = "Status", FillWeight = 24
        });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Elapsed", HeaderText = "Time (ms)", FillWeight = 18,
            ValueType = typeof(long)
        });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Details", HeaderText = "Details", FillWeight = 40
        });
        _resultGrid.CellFormatting += (_, args) =>
        {
            if (args.ColumnIndex != 1 || args.Value is not string status)
                return;
            args.CellStyle.ForeColor = status switch
            {
                "Open" => Color.FromArgb(11, 122, 81),
                "Closed" => Muted,
                "Timed out" => Color.FromArgb(181, 108, 13),
                _ => Color.FromArgb(165, 67, 59)
            };
        };
        layout.Controls.Add(_resultGrid, 0, 0);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(2, 7, 2, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        _summaryLabel.Text = "Open: 0   •   Closed: 0   •   Timed out: 0   •   Other: 0";
        _summaryLabel.ForeColor = Muted;
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _exportButton.Text = "Export CSV";
        StyleButton(_exportButton, Color.FromArgb(230, 236, 246), Navy, 121);
        _exportButton.Enabled = false;
        _exportButton.Dock = DockStyle.Fill;
        footer.Controls.Add(_summaryLabel, 0, 0);
        footer.Controls.Add(_exportButton, 1, 0);
        layout.Controls.Add(footer, 0, 1);
        return group;
    }

    private static Label MakeFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Muted,
        Margin = new Padding(3, 0, 2, 0)
    };

    private static void ConfigureNumber(NumericUpDown input, int min, int max, int value)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
        input.Dock = DockStyle.Fill;
        input.Font = new Font("Segoe UI", 10);
        input.Margin = new Padding(3, 2, 14, 6);
        input.TextAlign = HorizontalAlignment.Left;
    }

    private static void StyleButton(Button button, Color backColor, Color textColor, int width)
    {
        button.Width = width;
        button.Height = 31;
        button.Margin = new Padding(2, 0, 8, 0);
        button.BackColor = backColor;
        button.ForeColor = textColor;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        button.UseVisualStyleBackColor = false;
    }

    private void UpdatePortModeControls()
    {
        bool enabled = !_scanning;
        bool customMode = _customModeButton.Checked;
        _startPortInput.Enabled = enabled && !customMode;
        _endPortInput.Enabled = enabled && !customMode;
        _customPortsInput.Enabled = enabled && customMode;
    }

    private bool TryGetScanParameters(out string host, out int[] ports, out int timeoutMs)
    {
        host = _hostInput.Text.Trim();
        ports = Array.Empty<int>();
        timeoutMs = (int)_timeoutInput.Value;

        if (host.Length == 0 || host.Any(char.IsWhiteSpace)
            || (IPAddress.TryParse(host, out _) == false && Uri.CheckHostName(host) == UriHostNameType.Unknown))
        {
            MessageBox.Show(this, "Enter a valid hostname or IP address.",
                "Invalid target", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _hostInput.Focus();
            return false;
        }

        if (_customModeButton.Checked)
        {
            if (!PortListParser.TryParse(_customPortsInput.Text, MaxPortsPerScan, out ports, out var error))
            {
                MessageBox.Show(this, error, "Invalid custom ports",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _customPortsInput.Focus();
                return false;
            }
        }
        else
        {
            int startPort = (int)_startPortInput.Value;
            int endPort = (int)_endPortInput.Value;
            if (endPort < startPort)
            {
                MessageBox.Show(this, "The end port must be greater than or equal to the start port.",
                    "Invalid port range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _startPortInput.Focus();
                return false;
            }

            if (endPort - startPort + 1 > MaxPortsPerScan)
            {
                MessageBox.Show(this, $"Choose at most {MaxPortsPerScan:N0} ports per scan.",
                    "Port range too large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _startPortInput.Focus();
                return false;
            }
            ports = Enumerable.Range(startPort, endPort - startPort + 1).ToArray();
        }

        return true;
    }

    private async Task StartScanAsync()
    {
        if (_scanning || !TryGetScanParameters(out var host, out var ports, out var timeout))
            return;

        _scanning = true;
        var generation = ++_scanGeneration;
        _results.Clear();
        _resultGrid.Rows.Clear();
        _completed = _open = _closed = _timedOut = _other = 0;
        _total = ports.Length;
        _progress.Maximum = _total;
        _progress.Value = 0;
        UpdateSummary();
        SetScanningControls(true);

        using var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        try
        {
            _statusLabel.Text = $"Resolving {host}...";
            var address = await PortScanEngine.ResolveHostAsync(host, cancellation.Token);
            _statusLabel.Text = $"Checking TCP ports on {host} ({address})...";

            // Progress<T> sends callbacks to the Windows Forms UI synchronization context.
            var progress = new Progress<PortScanResult>(result =>
            {
                if (!IsDisposed && !Disposing && generation == _scanGeneration)
                    AddResult(result);
            });

            await PortScanEngine.ScanAsync(address, ports, timeout, progress, cancellation.Token);
            if (!IsDisposed && !Disposing)
                _statusLabel.Text = $"Finished scanning {host}: {_open} open port(s) detected.";
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
                _statusLabel.Text = $"Scan canceled: {_completed} of {_total} ports checked.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                _statusLabel.Text = "Unable to complete the scan.";
                MessageBox.Show(this, ex.Message, "Scan error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _scanCancellation = null;
            _scanning = false;
            if (!IsDisposed && !Disposing)
            {
                SetScanningControls(false);
                _exportButton.Enabled = _results.Count > 0;
                if (_resultGrid.Rows.Count > 1)
                    _resultGrid.Sort(_resultGrid.Columns["Port"]!, ListSortDirection.Ascending);
            }
        }
    }

    private void CancelScan()
    {
        if (!_scanning || _scanCancellation is null)
            return;
        _cancelButton.Enabled = false;
        _statusLabel.Text = "Canceling remaining checks...";
        _scanCancellation.Cancel();
    }

    private void SetScanningControls(bool scanning)
    {
        _startButton.Enabled = !scanning;
        _cancelButton.Enabled = scanning;
        _hostInput.Enabled = !scanning;
        _rangeModeButton.Enabled = !scanning;
        _customModeButton.Enabled = !scanning;
        UpdatePortModeControls();
        _timeoutInput.Enabled = !scanning;
        _exportButton.Enabled = !scanning && _results.Count > 0;
    }

    private void AddResult(PortScanResult result)
    {
        _results.Add(result);
        _completed++;
        switch (result.Status)
        {
            case "Open": _open++; break;
            case "Closed": _closed++; break;
            case "Timed out": _timedOut++; break;
            default: _other++; break;
        }
        _progress.Value = Math.Min(_completed, _total);
        if (!_openOnlyCheckbox.Checked || result.Status == "Open")
            AddGridRow(result);
        UpdateSummary();
        _statusLabel.Text = $"Scanning... {_completed:N0} of {_total:N0} TCP ports checked";
    }

    private void UpdateSummary()
    {
        _summaryLabel.Text = $"Open: {_open}   •   Closed: {_closed}   •   Timed out: {_timedOut}   •   Other: {_other}";
    }

    private void AddGridRow(PortScanResult result)
    {
        _resultGrid.Rows.Add(result.Port, result.Status, result.ElapsedMs, result.Details);
    }

    private void RefreshResultGrid()
    {
        _resultGrid.SuspendLayout();
        try
        {
            _resultGrid.Rows.Clear();
            foreach (var result in _results.OrderBy(result => result.Port))
            {
                if (!_openOnlyCheckbox.Checked || result.Status == "Open")
                    AddGridRow(result);
            }
        }
        finally
        {
            _resultGrid.ResumeLayout();
        }
    }

    private void ExportResults()
    {
        if (_results.Count == 0)
            return;
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"tcp-port-scan-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = "csv",
            AddExtension = true,
            Title = "Export TCP port scan"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var csv = new StringBuilder("Port,Status,ElapsedMs,Details\r\n");
            foreach (var result in _results.OrderBy(result => result.Port))
                csv.Append(result.Port).Append(',')
                    .Append(CsvValue(result.Status)).Append(',')
                    .Append(result.ElapsedMs).Append(',')
                    .Append(CsvValue(result.Details)).Append("\r\n");
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            _statusLabel.Text = $"Exported {_results.Count:N0} result(s) to CSV.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string CsvValue(string value) =>
        '"' + value.Replace("\"", "\"\"") + '"';
}
