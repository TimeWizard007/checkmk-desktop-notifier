using System.Text.RegularExpressions;
using System.Xml.Linq;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class PackagingTests
{
    [Fact]
    public void Central_version_is_1_3_0()
    {
        var version = ReadCentralVersion();
        Assert.Equal("1.3.0", version);
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
        Assert.Contains("(docs/images/problem-list-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/problem-list-v1.2.png)", readmePl, StringComparison.Ordinal);
        Assert.Contains("(docs/images/taken-filter-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/take-dialog-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/release-dialog-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/settings-team-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/settings-connection.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/settings-notifications-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/images/tray-menu-v1.2.png)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/compact-bar.png", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/about.png", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/problem-list.png)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/settings-notifications.png)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/settings-general.png", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/tray-menu.png)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/compact-bar.png", readmePl, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/about.png", readmePl, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/problem-list.png)", readmePl, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/settings-notifications.png)", readmePl, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/settings-general.png", readmePl, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/images/tray-menu.png)", readmePl, StringComparison.Ordinal);
    }

    [Fact]
    public void V1_release_assets_exist()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     "SHA256SUMS.txt",
                     "docs/RELEASE_NOTES_1.2.0.md",
                     "docs/images/problem-list-v1.2.png",
                     "docs/images/taken-filter-v1.2.png",
                     "docs/images/take-dialog-v1.2.png",
                     "docs/images/release-dialog-v1.2.png",
                     "docs/images/settings-team-v1.2.png",
                     "docs/images/settings-connection.png",
                     "docs/images/settings-notifications-v1.2.png",
                     "docs/images/tray-menu-v1.2.png"
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, relative)), relative);
        }

        var sums = File.ReadAllText(Path.Combine(root, "SHA256SUMS.txt"));
        Assert.Contains("CheckmkDesktopNotifier-Setup-x64.exe", sums, StringComparison.Ordinal);
        Assert.Contains(
            "8B880CB7EE363A135DACECDEF8A90FF6AA806315EA33D5028D327F0D3B8362BB",
            sums,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CheckmkDesktopNotifier-macOS", sums, StringComparison.Ordinal);
        Assert.DoesNotContain("1.3.0-beta.1", sums, StringComparison.Ordinal);
    }

    [Fact]
    public void Macos_beta_release_docs_exist()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     "docs/RELEASE_NOTES_1.3.0-beta.1.md",
                     "docs/MACOS_BETA_TESTERS.md",
                     "scripts/package-macos-app.sh",
                     "scripts/build-macos-beta.sh"
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, relative)), relative);
        }

        var notes = File.ReadAllText(Path.Combine(root, "docs/RELEASE_NOTES_1.3.0-beta.1.md"));
        Assert.Contains("macOS beta", notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.3.0-beta.1", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("10.10.20.", notes, StringComparison.Ordinal);
        var testers = File.ReadAllText(Path.Combine(root, "docs/MACOS_BETA_TESTERS.md"));
        Assert.Contains("Gatekeeper", testers, StringComparison.Ordinal);
        Assert.DoesNotContain("10.10.20.", testers, StringComparison.Ordinal);
    }

    [Fact]
    public void V1_3_0_release_docs_and_versioned_installer_name_exist()
    {
        var root = FindRepoRoot();
        foreach (var relative in new[]
                 {
                     "docs/RELEASE_NOTES_1.3.0.md",
                     "scripts/build-macos-release.sh",
                     "scripts/create-macos-dmg.sh",
                     "scripts/generate-macos-icon.sh",
                     "scripts/generate-macos-icon.py"
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, relative)), relative);
        }

        var notes = File.ReadAllText(Path.Combine(root, "docs/RELEASE_NOTES_1.3.0.md"));
        Assert.Contains("unified Windows + macOS", notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.3.0", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("10.10.20.", notes, StringComparison.Ordinal);
        var iss = File.ReadAllText(Path.Combine(root, "installer/CheckmkDesktopNotifier.iss"));
        Assert.Contains("OutputBaseFilename=CheckmkDesktopNotifier-Setup-x64-v{#MyAppVersion}", iss, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputBaseFilename=CheckmkDesktopNotifier-Setup-x64\n", iss + "\n", StringComparison.Ordinal);
        var macosHost = File.ReadAllText(Path.Combine(
            root,
            "src/CheckmkDesktopNotifier.App.MacOS/CheckmkDesktopNotifier.App.MacOS.csproj"));
        Assert.DoesNotContain("<Version>", macosHost, StringComparison.Ordinal);
        Assert.DoesNotContain("1.3.0-beta.1", macosHost, StringComparison.Ordinal);
        var plist = File.ReadAllText(Path.Combine(root, "src/CheckmkDesktopNotifier.App.MacOS/Bundle/Info.plist"));
        Assert.Contains("<string>1.3.0</string>", plist, StringComparison.Ordinal);
        Assert.Contains("CFBundleIconFile", plist, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier.icns", plist, StringComparison.Ordinal);
        Assert.DoesNotContain("1.3.0-beta.1", plist, StringComparison.Ordinal);
        var packager = File.ReadAllText(Path.Combine(root, "scripts/package-macos-app.sh"));
        Assert.Contains("generate-macos-icon.py", packager, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier.icns", packager, StringComparison.Ordinal);
        var historical = File.ReadAllText(Path.Combine(root, "SHA256SUMS.txt"));
        Assert.Contains(
            "8B880CB7EE363A135DACECDEF8A90FF6AA806315EA33D5028D327F0D3B8362BB",
            historical,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Macos_beta_checksums_do_not_overwrite_windows_sums()
    {
        var root = FindRepoRoot();
        var windows = File.ReadAllText(Path.Combine(root, "SHA256SUMS.txt"));
        Assert.Contains(
            "8B880CB7EE363A135DACECDEF8A90FF6AA806315EA33D5028D327F0D3B8362BB",
            windows,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CheckmkDesktopNotifier-macOS", windows, StringComparison.Ordinal);

        var macosPath = Path.Combine(root, "SHA256SUMS-macOS-v1.3.0-beta.1.txt");
        Assert.True(File.Exists(macosPath), "SHA256SUMS-macOS-v1.3.0-beta.1.txt");
        var macos = File.ReadAllText(macosPath);
        Assert.Contains("CheckmkDesktopNotifier-macOS-x64-v1.3.0-beta.1.zip", macos, StringComparison.Ordinal);
        Assert.Contains("CheckmkDesktopNotifier-macOS-arm64-v1.3.0-beta.1.zip", macos, StringComparison.Ordinal);
        Assert.Contains(
            "6F019D58BAA33D2561691742C1F645E37F28FAE226FE651339CA25AB7678FD86",
            macos,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "6F225EF0E78BECDCEE0CAA2D6BD7FB8813AE475579A4D22ED4660766D57F51BF",
            macos,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CheckmkDesktopNotifier-Setup-x64.exe", macos, StringComparison.Ordinal);
        Assert.DoesNotContain("10.10.20.", macos, StringComparison.Ordinal);
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
        var root = Path.Combine(Path.GetTempPath(), "cdn-layout", Guid.NewGuid().ToString("N"));
        Assert.True(InstallLayout.BinariesAreSeparateFromUserData(root));
        Assert.EndsWith(
            Path.Combine("Programs", "CheckmkDesktopNotifier"),
            InstallLayout.GetPerUserInstallDirectory(root),
            StringComparison.Ordinal);
        Assert.False(InstallLayout.GetPerUserDataDirectory(root).Contains(
            Path.Combine("Programs", "CheckmkDesktopNotifier"),
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal("CheckmkDesktopNotifier.exe", InstallLayout.ExecutableFileName);
    }

    [Fact]
    public void Installed_autostart_command_is_quoted_path_without_secrets()
    {
        var root = Path.Combine(Path.GetTempPath(), "cdn-layout", Guid.NewGuid().ToString("N"));
        var command = AutostartCommand.Format(InstallLayout.GetPerUserInstallExecutablePath(root));
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
                     "scripts/hash-windows-installer.sh",
                     "scripts/package-macos-app.sh",
                     "scripts/build-macos-beta.sh",
                     "scripts/build-macos-release.sh",
                     "scripts/create-macos-dmg.sh",
                     "scripts/generate-macos-icon.sh",
                     "scripts/generate-macos-icon.py"
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
