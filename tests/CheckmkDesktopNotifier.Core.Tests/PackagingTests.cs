using System.Text.RegularExpressions;
using System.Xml.Linq;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class PackagingTests
{
    [Fact]
    public void Central_version_is_1_0_0()
    {
        var version = ReadCentralVersion();
        Assert.Equal("1.0.0", version);
        Assert.Equal(version, ApplicationVersion.FromAssembly(typeof(ProductInfo).Assembly));
    }

    [Fact]
    public void V1_readme_and_license_files_exist()
    {
        var root = FindRepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var readmePl = File.ReadAllText(Path.Combine(root, "README.pl.md"));
        var license = File.ReadAllText(Path.Combine(root, "LICENSE"));
        Assert.Contains("[Polski](README.pl.md)", readme, StringComparison.Ordinal);
        Assert.Contains("[English](README.md)", readmePl, StringComparison.Ordinal);
        Assert.Contains("not affiliated with Checkmk GmbH", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT License", license, StringComparison.Ordinal);
        Assert.Contains("TimeWizard007", license, StringComparison.Ordinal);
        Assert.DoesNotContain("vscode-file://", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("vscode-file://", readmePl, StringComparison.Ordinal);
        Assert.Contains("![Compact Always-on-Top bar](docs/images/compact-bar.png)", readme, StringComparison.Ordinal);
        Assert.Contains("![Kompaktowy pasek Always-on-Top](docs/images/compact-bar.png)", readmePl, StringComparison.Ordinal);
    }

    [Fact]
    public void V1_release_assets_exist()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     "SHA256SUMS.txt",
                     "docs/images/compact-bar.png",
                     "docs/images/problem-list.png",
                     "docs/images/settings-connection.png",
                     "docs/images/settings-notifications.png",
                     "docs/images/settings-general.png",
                     "docs/images/tray-menu.png",
                     "docs/images/about.png"
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, relative)), relative);
        }

        var sums = File.ReadAllText(Path.Combine(root, "SHA256SUMS.txt"));
        Assert.Contains("CheckmkDesktopNotifier-Setup-x64.exe", sums, StringComparison.Ordinal);
        Assert.Contains(
            "71C5A97C461B513DF2B977F4FEC39C2E739E5817779EF9BA205C44EDEF847B2E",
            sums,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_fallback_version_matches_directory_build_props()
    {
        var iss = File.ReadAllText(Path.Combine(FindRepoRoot(), "installer/CheckmkDesktopNotifier.iss"));
        var match = Regex.Match(iss, @"#define MyAppVersion ""([^""]+)""");
        Assert.True(match.Success);
        Assert.Equal(ReadCentralVersion(), match.Groups[1].Value);
    }

    [Fact]
    public void Executable_product_metadata_is_independent()
    {
        Assert.Equal("Checkmk Desktop Notifier", ProductInfo.ProductName);
        Assert.Equal("Desktop monitor and notifier for Checkmk", ProductInfo.Description);
        Assert.Equal("TimeWizard007", ProductInfo.Author);
        Assert.Contains("2026", ProductInfo.Copyright, StringComparison.Ordinal);
        Assert.Contains("TimeWizard007", ProductInfo.Copyright, StringComparison.Ordinal);
        Assert.Contains("not affiliated with Checkmk GmbH", ProductInfo.Disclaimer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Checkmk GmbH employee", ProductInfo.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Install_binaries_and_user_data_are_separate()
    {
        Assert.True(InstallLayout.BinariesAreSeparateFromUserData());
        Assert.EndsWith(
            Path.Combine("Programs", "CheckmkDesktopNotifier"),
            InstallLayout.GetPerUserInstallDirectory(),
            StringComparison.Ordinal);
        Assert.False(InstallLayout.GetPerUserDataDirectory().Contains(
            Path.Combine("Programs", "CheckmkDesktopNotifier"),
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal("CheckmkDesktopNotifier.exe", InstallLayout.ExecutableFileName);
    }

    [Fact]
    public void Installed_autostart_command_is_quoted_path_without_secrets()
    {
        var command = AutostartCommand.Format(InstallLayout.GetPerUserInstallExecutablePath());
        Assert.StartsWith("\"", command, StringComparison.Ordinal);
        Assert.EndsWith("\"", command, StringComparison.Ordinal);
        Assert.Contains(InstallLayout.ExecutableFileName, command, StringComparison.Ordinal);
        Assert.False(AutostartCommand.ContainsDisallowedPayload(command));
        Assert.DoesNotContain("Authorization", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", command, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CheckmkDesktopNotifier", AutostartCommand.ValueName);
    }

    [Fact]
    public void Installer_source_is_per_user_hkcu_run_only()
    {
        var iss = File.ReadAllText(Path.Combine(FindRepoRoot(), "installer/CheckmkDesktopNotifier.iss"));
        Assert.Contains("PrivilegesRequired=lowest", iss, StringComparison.Ordinal);
        Assert.Contains(@"{localappdata}\Programs\CheckmkDesktopNotifier", iss, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\Run", iss, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier", iss, StringComparison.Ordinal);
        Assert.Contains("Local\\TimeWizard007.CheckmkDesktopNotifier", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivilegesRequired=admin", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HKEY_LOCAL_MACHINE", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("HKLM", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("{commonstartup}", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{userstartup}", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schtasks", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"Program Files", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("checkmk.local.json", iss, StringComparison.Ordinal);
        Assert.Contains("*.json.example", iss, StringComparison.Ordinal);
        Assert.Contains("cmdkey.exe", iss, StringComparison.Ordinal);
        Assert.Contains("/delete:CheckmkDesktopNotifier", iss, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_instance_names_are_per_user_local_not_global()
    {
        Assert.StartsWith(@"Local\", SingleInstanceIdentity.MutexName, StringComparison.Ordinal);
        Assert.StartsWith(@"Local\", SingleInstanceIdentity.ActivateEventName, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Global\", SingleInstanceIdentity.MutexName, StringComparison.Ordinal);
        Assert.Equal(
            @"Local\TimeWizard007.CheckmkDesktopNotifier",
            SingleInstanceIdentity.MutexName);
    }

    [Fact]
    public void Packaging_scripts_do_not_reference_secrets()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     "scripts/build-windows-package.ps1",
                     "scripts/build-windows-package.sh",
                     "scripts/publish-win-x64.sh",
                     "scripts/hash-windows-installer.ps1",
                     "scripts/hash-windows-installer.sh"
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            Assert.DoesNotContain("CHECKMK_SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("checkmk.local.json", text, StringComparison.Ordinal);
        }
    }

    private static string ReadCentralVersion()
    {
        var xml = XDocument.Load(Path.Combine(FindRepoRoot(), "Directory.Build.props"));
        var version = xml.Root?
            .Elements("PropertyGroup")
            .Elements("Version")
            .Select(e => e.Value.Trim())
            .FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(version));
        return version!;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CheckmkDesktopNotifier.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
