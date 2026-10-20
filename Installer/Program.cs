using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace NetAdapterManagerSetup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}

internal sealed class InstallerForm : Form
{
    private const string ProductDisplayName = "网络适配器管理器";
    private const string Version = "1.4.11";
    private const string InstallFolderName = "NetAdapterManager";
    private readonly string _installRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", InstallFolderName);
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _installButton;

    public InstallerForm()
    {
        Text = $"{ProductDisplayName} 安装程序";
        Width = 560;
        Height = 350;
        MinimumSize = new Size(560, 350);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var title = new Label
        {
            Text = $"{ProductDisplayName} {Version}",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 18, System.Drawing.FontStyle.Bold),
            AutoSize = true,
            Location = new Point(34, 28)
        };
        var subtitle = new Label
        {
            Text = "Windows IPv4 配置工具",
            AutoSize = true,
            ForeColor = System.Drawing.Color.DimGray,
            Location = new Point(37, 68)
        };
        var installPathTitle = new Label { Text = "安装位置", AutoSize = true, Location = new Point(36, 112) };
        var installPath = new TextBox
        {
            Text = _installRoot,
            ReadOnly = true,
            Width = 470,
            Location = new Point(36, 136)
        };
        var dataPathTitle = new Label { Text = "数据位置（与安装目录绑定）", AutoSize = true, Location = new Point(36, 176) };
        var dataPath = new TextBox
        {
            Text = _installRoot,
            ReadOnly = true,
            Width = 470,
            Location = new Point(36, 200)
        };

        _statusLabel = new Label { Text = "准备安装。", AutoSize = true, Location = new Point(36, 245) };
        _progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Width = 270, Location = new Point(36, 270) };
        _installButton = new Button { Text = "安装", Width = 90, Height = 32, Location = new Point(315, 265) };
        _installButton.Click += InstallButton_Click;
        var cancelButton = new Button { Text = "取消", Width = 90, Height = 32, Location = new Point(415, 265), DialogResult = DialogResult.Cancel };

        Controls.AddRange([title, subtitle, installPathTitle, installPath, dataPathTitle, dataPath,
            _statusLabel, _progressBar, _installButton, cancelButton]);
        AcceptButton = _installButton;
        CancelButton = cancelButton;
    }

    private void InstallButton_Click(object? sender, EventArgs e)
    {
        _installButton.Enabled = false;
        _statusLabel.Text = "正在安装，请稍候…";
        _progressBar.Value = 10;
        try
        {
            Directory.CreateDirectory(_installRoot);
            ExtractPayload(_installRoot);
            _progressBar.Value = 75;
            CreateShortcuts(_installRoot);
            _progressBar.Value = 100;
            _statusLabel.Text = "安装完成，数据将保存在安装目录。";
            MessageBox.Show(this,
                $"{ProductDisplayName} {Version} 已安装完成。\n\n安装位置：{_installRoot}\n数据位置：{_installRoot}",
                "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _progressBar.Value = 0;
            _statusLabel.Text = "安装失败，请检查权限或磁盘空间。";
            _installButton.Enabled = true;
            MessageBox.Show(this, $"安装失败：{ex.Message}", "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractPayload(string installRoot)
    {
        var resourceName = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) throw new InvalidOperationException("安装包中没有找到程序文件。");

        var staging = Path.Combine(Path.GetTempPath(), $"NetAdapterManager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("无法读取安装包内容。");
            var archivePath = Path.Combine(staging, "Payload.zip");
            using (var archiveFile = File.Create(archivePath)) resource.CopyTo(archiveFile);
            ZipFile.ExtractToDirectory(archivePath, installRoot, overwriteFiles: true);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static void CreateShortcuts(string installRoot)
    {
        var executable = Path.Combine(installRoot, "NetAdapterManager.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("安装后没有找到主程序。", executable);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ProductDisplayName);
        Directory.CreateDirectory(startMenu);
        CreateShortcut(Path.Combine(desktop, $"{ProductDisplayName}.lnk"), executable, installRoot);
        CreateShortcut(Path.Combine(startMenu, $"{ProductDisplayName}.lnk"), executable, installRoot);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("系统不支持创建桌面快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = $"{targetPath},0";
        shortcut.Description = ProductDisplayName;
        shortcut.Save();
    }
}
