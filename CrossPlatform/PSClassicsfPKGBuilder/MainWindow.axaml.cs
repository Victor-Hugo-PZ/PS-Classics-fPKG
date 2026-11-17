using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Material.Icons;
using PS_Classics_fPKG_Builder;
using PSClassicsfPKGBuilder.Controls;
using PSClassicsfPKGBuilder.Dialogs;

namespace PSClassicsfPKGBuilder;

/// <summary>
/// Main window for Linux and macOS. Mirrors the Windows app (Windows/PS Classics fPKG Builder/PSClassicsfPKGBuilder.xaml.vb):
/// the handlers only read the controls, build a *BuildRequest and show messages. All the logic is in PSClassics.Core.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly FilePickerFileType BinFiles = new("BIN") { Patterns = new[] { "*.bin", "*.BIN" } };
    private static readonly FilePickerFileType IsoFiles = new("ISO") { Patterns = new[] { "*.iso", "*.ISO" } };
    private static readonly FilePickerFileType TxtFiles = new("TXT") { Patterns = new[] { "*.txt", "*.TXT" } };
    private static readonly FilePickerFileType LuaFiles = new("LUA") { Patterns = new[] { "*.lua", "*.LUA" } };
    private static readonly FilePickerFileType PkgFiles = new("PS4 package") { Patterns = new[] { "*.pkg", "*.PKG" } };
    private static readonly FilePickerFileType MemoryCardFiles = new("PS2 memory card") { Patterns = new[] { "*.vm2", "*.ps2", "*.bin", "*.VM2", "*.PS2", "*.BIN" } };
    private static readonly FilePickerFileType ImageFiles = new("Images (PNG, JPG, BMP, GIF, WebP)")
    {
        Patterns = ImageFormats.Patterns().SelectMany(p => new[] { p, p.ToUpperInvariant() }).ToArray(),
    };

    private string _currentPS1GameID = "";
    private string _currentPS2GameID = "";
    private string _currentPS2GameCRC = "";
    private bool _building;

    public MainWindow()
    {
        InitializeComponent();
        WireUpNavigation();
        WireUpPS1();
        WireUpPS2();
        WireUpPSP();
        UpdateAllPreviews();
        Opened += async (_, _) => await CheckEnvironment();
    }

    #region Shared helpers

    private static string EmulatorsDirectory => Path.Combine(AppPaths.ToolsDirectory, "PS4", "emus");

    private void WireUpNavigation()
    {
        PlatformListBox.SelectionChanged += (_, _) =>
        {
            PS1Page.IsVisible = PlatformListBox.SelectedIndex == 0;
            PS2Page.IsVisible = PlatformListBox.SelectedIndex == 1;
            PSPPage.IsVisible = PlatformListBox.SelectedIndex == 2;
        };
        VerifyPkgButton.Click += VerifyPkgButton_Click;
        OpenGuideButton.Click += async (_, _) => await new GuideWindow().ShowDialog(this);
        WineSetupButton.Click += async (_, _) =>
        {
            await new WineSetupWindow().ShowDialog(this);
            UpdateWineStatus();
        };
        WineSetupButton.IsVisible = !Platform.IsWindows;
        WineStatusPanel.IsVisible = !Platform.IsWindows;
    }

    /// <summary>Shows the state of the Tools folder (and of Wine on Linux/macOS) and warns once when something is missing.</summary>
    private async Task CheckEnvironment()
    {
        if (AppPaths.ToolsAvailable())
        {
            ToolsStatusText.Text = "Tools folder found";
            ToolsStatusIcon.Kind = MaterialIconKind.CheckCircleOutline;
        }
        else
        {
            ToolsStatusText.Text = "Tools folder missing";
            ToolsStatusIcon.Kind = MaterialIconKind.AlertOutline;
            ToolsStatusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFC857"));
            await MessageDialog.ShowError(this, "Tools folder missing",
                "The 'Tools' folder was not found at:\n" + AppPaths.ToolsDirectory + "\n\n" +
                "Building fPKGs will fail until it is copied next to the application (see README.md, 'Building from source').");
        }

        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            if (await MessageDialog.Confirm(this, "Wine is not ready",
                    "The PS4 publishing tools are Windows programs and run through Wine on this system. Wine is not ready yet.\n\nOpen the Wine setup now?",
                    "Open Wine setup", "Later"))
            {
                await new WineSetupWindow().ShowDialog(this);
                UpdateWineStatus();
            }
        }
    }

    /// <summary>Updates the Wine line at the bottom left. Returns true when Wine is ready (always true on Windows).</summary>
    private bool UpdateWineStatus()
    {
        if (Platform.IsWindows) return true;
        var status = Wine.GetStatus();
        if (status.IsReady)
        {
            WineStatusText.Text = "Wine ready";
            WineStatusIcon.Kind = MaterialIconKind.CheckCircleOutline;
            WineStatusIcon.Foreground = Brushes.White;
        }
        else
        {
            WineStatusText.Text = status.WineFound ? "Wine needs setup" : "Wine not found";
            WineStatusIcon.Kind = MaterialIconKind.AlertOutline;
            WineStatusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFC857"));
        }
        return status.IsReady;
    }

    private async void VerifyPkgButton_Click(object sender, RoutedEventArgs e)
    {
        var pkgFile = await PickFile("Select the .pkg to check.", PkgFiles);
        if (pkgFile == null) return;
        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            await MessageDialog.ShowError(this, "Check a .pkg", "Wine is not ready. Press 'Wine setup' first.");
            return;
        }

        VerifyPkgButton.IsEnabled = false;
        VerifyPkgButtonText.Text = "Checking…";
        try
        {
            var report = await Task.Run(() => PackageVerifier.Verify(pkgFile));
            await new PackageReportWindow(report).ShowDialog(this);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowError(this, "Check a .pkg", "The package could not be checked:\n" + ex.Message);
        }
        finally
        {
            VerifyPkgButtonText.Text = "Check a .pkg";
            VerifyPkgButton.IsEnabled = true;
        }
    }

    private async Task<string> PickFile(string title, params FilePickerFileType[] types)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = types,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string> PickOutputFolder()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Please select an output folder",
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    /// <summary>Fills a text box with a file chosen by the user.</summary>
    private void Browse(Button button, TextBox target, string title, params FilePickerFileType[] types)
    {
        button.Click += async (_, _) =>
        {
            var file = await PickFile(title, types);
            if (file != null) target.Text = file;
        };
    }

    private void BrowseImage(Button button, TextBox target, bool isIcon) =>
        Browse(button, target, isIcon
            ? "Select an image for the icon (PNG, JPG, BMP, GIF or WebP)."
            : "Select an image for the background (PNG, JPG, BMP, GIF or WebP).", ImageFiles);

    /// <summary>NP titles are upper case only (the Windows app uses CharacterCasing="Upper").</summary>
    private static void ForceUpperCase(TextBox box)
    {
        box.TextChanged += (_, _) =>
        {
            var text = box.Text ?? "";
            var upper = text.ToUpperInvariant();
            if (text == upper) return;
            var caret = box.CaretIndex;
            box.Text = upper;
            box.CaretIndex = caret;
        };
    }

    private static string SelectedText(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content as string ?? "";

    /// <summary>Common checks before a build, same messages as the Windows app. Returns false when the build must not start.</summary>
    private async Task<bool> CheckRequiredFields(string disc, string title, string npTitle)
    {
        string error = null;
        if (string.IsNullOrEmpty(disc)) error = "No disc 1 specified, fPKG creation will be aborted.";
        else if (string.IsNullOrEmpty(title)) error = "No game title specified, fPKG creation will be aborted.";
        else if (string.IsNullOrEmpty(npTitle)) error = "No NP title specified, fPKG creation will be aborted.";
        else if (npTitle.Length != 9) error = "'NP Title' length mismatching, only 9 characters are allowed, fPKG creation will be aborted.";

        if (error != null)
        {
            await MessageDialog.ShowError(this, "Cannot create fPKG", error);
            return false;
        }

        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            await MessageDialog.ShowError(this, "Cannot create fPKG",
                "Wine is not ready, so the PS4 tools cannot run. Press 'Wine setup' at the bottom left and follow the steps.");
            return false;
        }
        return true;
    }

    /// <summary>Asks for the output folder and runs the build in the background, keeping the window responsive.</summary>
    private async Task RunBuild(Button buildButton, TextBlock buildButtonText, ProgressBar progress, Func<string, PackageBuildResult> build)
    {
        if (_building) return;
        var outputFolder = await PickOutputFolder();
        if (outputFolder == null) return;

        _building = true;
        buildButton.IsEnabled = false;
        buildButtonText.Text = "Building… (this can take a few minutes)";
        progress.IsVisible = true;
        try
        {
            var result = await Task.Run(() => build(outputFolder));
            await ReportResult(result, outputFolder);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowError(this, "Cannot create fPKG", "The fPKG could not be created:\n" + ex.Message);
        }
        finally
        {
            _building = false;
            buildButton.IsEnabled = true;
            buildButtonText.Text = "Build fPKG";
            progress.IsVisible = false;
        }
    }

    private async Task ReportResult(PackageBuildResult result, string outputFolder)
    {
        if (result.Success)
        {
            if (await MessageDialog.Confirm(this, "PKG created!", "PKG created!\nDo you want to open the output folder ?"))
                OpenFolder(outputFolder);
        }
        else
        {
            await MessageDialog.ShowError(this, "Cannot create fPKG", "PKG creation failed!\n" + result.Output);
        }
    }

    private static void OpenFolder(string folder)
    {
        var opener = Platform.IsWindows ? "explorer" : Platform.IsMacOS ? "open" : "xdg-open";
        try
        {
            var info = new ProcessStartInfo(opener) { UseShellExecute = false };
            info.ArgumentList.Add(folder);
            Process.Start(info);
        }
        catch (Exception)
        {
            //Nothing to do: the folder is shown in the message anyway
        }
    }

    /// <summary>Asks a yes/no question from the build thread (callbacks of the build requests) and waits for the answer.</summary>
    private bool AskFromBuildThread(string title, string message) =>
        Dispatcher.UIThread.InvokeAsync(() => MessageDialog.Confirm(this, title, message)).GetAwaiter().GetResult();

    private void WarnFromBuildThread(string message) =>
        Dispatcher.UIThread.InvokeAsync(() => MessageDialog.ShowInfo(this, "Warning", message)).GetAwaiter().GetResult();

    private static void UpdatePreview(DashPreview preview, string icon, string background, string fallbackIcon, string fallbackBackground,
        string title, bool isPS5, Image iconThumbnail, Image backgroundThumbnail, TextBlock hints)
    {
        var messages = preview.Update(icon, background, fallbackIcon, fallbackBackground, title, isPS5);
        iconThumbnail.Source = preview.CurrentIcon;
        backgroundThumbnail.Source = preview.CurrentBackground;
        hints.Text = string.Join(Environment.NewLine, messages);
        hints.IsVisible = messages.Count > 0;
    }

    private void UpdateAllPreviews()
    {
        UpdatePS1Preview();
        UpdatePS2Preview();
        UpdatePSPPreview();
    }

    #endregion

    #region PS1

    private void WireUpPS1()
    {
        ForceUpperCase(PS1NPTitleTextBox);
        BrowsePS1Disc1Button.Click += BrowsePS1Disc1Button_Click;
        BrowseImage(BrowsePS1IconButton, SelectedPS1IconTextBox, true);
        BrowseImage(BrowsePS1StartupImageButton, SelectedPS1BGTextBox, false);
        Browse(BrowsePS1Disc2Button, PS1SelectedDisc2TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1Disc3Button, PS1SelectedDisc3TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1Disc4Button, PS1SelectedDisc4TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1TXTConfigButton, SelectedPS1TXTConfigTextBox, "Select a TXT config file.", TxtFiles);
        Browse(BrowsePS1LUAConfigButton, SelectedPS1LUAConfigTextBox, "Select a LUA config file.", LuaFiles);

        SelectedPS1IconTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        SelectedPS1BGTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        PS1TitleTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        PS1PS5Radio.IsCheckedChanged += (_, _) => UpdatePS1Preview();

        BuildPS1fPKGButton.Click += BuildPS1fPKGButton_Click;
    }

    private void UpdatePS1Preview()
    {
        var sceSys = Path.Combine(EmulatorsDirectory, "ps1hd", "sce_sys");
        UpdatePreview(PS1Preview, SelectedPS1IconTextBox.Text, SelectedPS1BGTextBox.Text,
            Path.Combine(sceSys, "icon0.png"), Path.Combine(sceSys, "pic1.png"),
            PS1TitleTextBox.Text, PS1PS5Radio.IsChecked == true, PS1IconThumbnail, PS1BackgroundThumbnail, PS1HintsText);
    }

    private async void BrowsePS1Disc1Button_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFile("Select a PS1 BIN file.", BinFiles);
        if (file == null) return;

        //Get the game ID from the BIN file and a title from the master list
        var gameID = await Task.Run(() => PS1Game.GetPS1GameID(file)) ?? "";
        var gameTitle = gameID.Length == 10 ? PS1Game.GetPS1GameTitleFromDatabaseList(gameID) : "";

        //Forget the ID of a previously selected game (it must not leak into the libcrypt check of this one)
        _currentPS1GameID = gameID.Trim();

        if (!string.IsNullOrEmpty(gameID))
        {
            PS1NPTitleTextBox.Text = gameID.Replace("-", "").Trim();
            PS1SelectedDisc1TextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, "Game ID not found",
                     "Cannot find the game ID within the PS1 BIN file, do you want to use this file anyway ?\nDo not forget to specify a NP title and title if you do so."))
        {
            PS1SelectedDisc1TextBox.Text = file;
        }

        if (!string.IsNullOrEmpty(gameTitle)) PS1TitleTextBox.Text = gameTitle;
    }

    private async void BuildPS1fPKGButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRequiredFields(PS1SelectedDisc1TextBox.Text, PS1TitleTextBox.Text, PS1NPTitleTextBox.Text)) return;

        var request = new PS1BuildRequest
        {
            Title = PS1TitleTextBox.Text,
            NPTitle = PS1NPTitleTextBox.Text,
            DetectedGameID = _currentPS1GameID,
            Disc1 = PS1SelectedDisc1TextBox.Text,
            Disc2 = PS1SelectedDisc2TextBox.Text ?? "",
            Disc3 = PS1SelectedDisc3TextBox.Text ?? "",
            Disc4 = PS1SelectedDisc4TextBox.Text ?? "",
            IconFile = SelectedPS1IconTextBox.Text ?? "",
            BackgroundFile = SelectedPS1BGTextBox.Text ?? "",
            TxtConfigFile = SelectedPS1TXTConfigTextBox.Text ?? "",
            LuaConfigFile = SelectedPS1LUAConfigTextBox.Text ?? "",
            Upscaling = SelectedText(PS1UpscalingComboBox),
            SkipBootlogo = PS1SkipBootlogoCheckBox.IsChecked == true,
            Guncon = PS1GunconCheckBox.IsChecked == true,
            Force60Hz = PS1Force60HzCheckBox.IsChecked == true,
            EmulateAnalogSticks = PS1EmulateAnalogSticksCheckBox.IsChecked == true,
            ConfirmLibcrypt = () => AskFromBuildThread("Libcrypt",
                "This game is Libcrypt protected. Do you want to add --libcrypt to the configuration file ?"),
        };

        await RunBuild(BuildPS1fPKGButton, BuildPS1fPKGButtonText, PS1BuildProgress, folder => PS1PackageBuilder.Build(request, folder));
    }

    #endregion

    #region PS2

    private void WireUpPS2()
    {
        ForceUpperCase(PS2NPTitleTextBox);
        BrowsePS2Disc1Button.Click += BrowsePS2Disc1Button_Click;
        BrowseImage(BrowsePS2IconButton, SelectedIconTextBox, true);
        BrowseImage(BrowsePS2StartupImageButton, SelectedStartupImageTextBox, false);
        Browse(BrowsePS2MCButton, SelectedPS2MemoryCardTextBox, "Select a PS2 Memory Card file.", MemoryCardFiles);
        Browse(BrowsePS2TXTConfigButton, SelectedPS2TXTConfigTextBox, "Select a TXT config file.", TxtFiles);
        Browse(BrowsePS2LUAConfigButton, SelectedPS2LUAConfigTextBox, "Select a LUA config file.", LuaFiles);
        Browse(BrowsePS2Disc2Button, SelectedDisc2TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc3Button, SelectedDisc3TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc4Button, SelectedDisc4TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc5Button, SelectedDisc5TextBox, "Select a PS2 ISO file.", IsoFiles);

        PS2EmulatorComboBox.SelectionChanged += (_, _) =>
        {
            UpdatePS2Preview();
            //Hint under the emulator field: describes the choice that is selected now
            PS2EmulatorHint.Text = SelectedText(PS2EmulatorComboBox) == "Rogue v1"
                ? "Rogue v1 selected. If the game does not start or crashes, switch back to Jak v2 (the default)."
                : "Default: Jak v2. If the game does not start or crashes, try Rogue v1.";
        };
        SelectedIconTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        SelectedStartupImageTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        PS2TitleTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        PS2PS5Radio.IsCheckedChanged += (_, _) => UpdatePS2Preview();

        BuildPS2fPKGButton.Click += BuildPS2fPKGButton_Click;
    }

    /// <summary>The default icon and background of a PS2 package come from the selected emulator.</summary>
    private void UpdatePS2Preview()
    {
        var emulator = SelectedText(PS2EmulatorComboBox);
        if (string.IsNullOrEmpty(emulator)) emulator = "Jak v2";
        var sceSys = Path.Combine(EmulatorsDirectory, emulator, "sce_sys");
        UpdatePreview(PS2Preview, SelectedIconTextBox.Text, SelectedStartupImageTextBox.Text,
            Path.Combine(sceSys, "icon0.png"), Path.Combine(sceSys, "pic1.png"),
            PS2TitleTextBox.Text, PS2PS5Radio.IsChecked == true, PS2IconThumbnail, PS2BackgroundThumbnail, PS2HintsText);
    }

    private async void BrowsePS2Disc1Button_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFile("Select a PS2 ISO file.", IsoFiles);
        if (file == null) return;

        BrowsePS2Disc1Button.IsEnabled = false;
        string gameID, gameCRC;
        try
        {
            (gameID, gameCRC) = await Task.Run(() =>
            {
                var id = PS2Game.GetPS2GameID(file) ?? "";
                var elf = IsoTools.GetELFfromISO(file, id);
                return (id, IsoTools.GetGameCRC(elf) ?? "");
            });
        }
        catch (Exception)
        {
            (gameID, gameCRC) = ("", "");
        }
        finally
        {
            BrowsePS2Disc1Button.IsEnabled = true;
        }

        _currentPS2GameID = gameID;
        _currentPS2GameCRC = gameCRC;

        if (!string.IsNullOrEmpty(gameID) && !string.IsNullOrEmpty(gameCRC))
        {
            var configs = Path.Combine(AppPaths.ToolsDirectory, "PS4", "ps2-configs");
            ShowDatabaseOption(PS2AddTXTConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(gameID, Path.Combine(configs, "configs_txt.dat")));
            ShowDatabaseOption(PS2AddLUAConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(gameID, Path.Combine(configs, "configs_lua.dat")));
            ShowDatabaseOption(PS2AddPS3ConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(gameID + ".CONFIG", Path.Combine(configs, "configs_ps3.dat")));

            if (IsoTools.IsConfigAvailable(gameCRC + ".lua", Path.Combine(configs, "widescreen.dat")))
            {
                PS2UseWidescreenPatchCheckBox.IsChecked = true;
                PS2UseWidescreenPatchCheckBox.IsEnabled = true;
            }
            else
            {
                PS2UseWidescreenPatchCheckBox.IsEnabled = false;
            }

            PS2TitleTextBox.Text = PS2Game.GetPS2GameTitleFromDatabaseList(gameID.Replace(".", "").Replace("_", "-").Trim());
            PS2NPTitleTextBox.Text = gameID.Replace(".", "").Replace("_", "").Trim();
            SelectedDisc1TextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, "PS2 Game ID not found",
                     "Could not find the PS2 game ID within the ISO file.\nDo you want to use this file anyway ?"))
        {
            SelectedDisc1TextBox.Text = file;
        }
    }

    private static void ShowDatabaseOption(CheckBox box, bool available)
    {
        box.IsChecked = available;
        box.IsVisible = available;
    }

    private async void BuildPS2fPKGButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRequiredFields(SelectedDisc1TextBox.Text, PS2TitleTextBox.Text, PS2NPTitleTextBox.Text)) return;

        var request = new PS2BuildRequest
        {
            Title = PS2TitleTextBox.Text,
            NPTitle = PS2NPTitleTextBox.Text,
            DetectedGameID = _currentPS2GameID,
            GameCRC = _currentPS2GameCRC,
            Disc1 = SelectedDisc1TextBox.Text,
            Disc2 = SelectedDisc2TextBox.Text ?? "",
            Disc3 = SelectedDisc3TextBox.Text ?? "",
            Disc4 = SelectedDisc4TextBox.Text ?? "",
            Disc5 = SelectedDisc5TextBox.Text ?? "",
            Emulator = SelectedText(PS2EmulatorComboBox),
            IconFile = SelectedIconTextBox.Text ?? "",
            BackgroundFile = SelectedStartupImageTextBox.Text ?? "",
            MemoryCardFile = SelectedPS2MemoryCardTextBox.Text ?? "",
            TxtConfigFile = SelectedPS2TXTConfigTextBox.Text ?? "",
            LuaConfigFile = SelectedPS2LUAConfigTextBox.Text ?? "",
            AddTxtConfigFromDatabase = PS2AddTXTConfigFromDatabaseCheckBox.IsChecked == true,
            AddLuaConfigFromDatabase = PS2AddLUAConfigFromDatabaseCheckBox.IsChecked == true,
            AddPS3ConfigFromDatabase = PS2AddPS3ConfigFromDatabaseCheckBox.IsChecked == true,
            UseWidescreenPatch = PS2UseWidescreenPatchCheckBox.IsChecked == true,
            Uprender = SelectedText(PS2UprenderComboBox),
            Upscaling = SelectedText(PS2UpscalingComboBox),
            DisplayMode = SelectedText(PS2DisplayModeComboBox),
            RestartEmulatorOnDiscChange = PS2RestartEmulatorOnDiscChangeCheckBox.IsChecked == true,
            MultitapIndex = PS2MultitapComboBox.SelectedIndex,
            ImproveSpeed = PS2ImproveSpeedCheckBox.IsChecked == true,
            FixGraphics = PS2FixGraphicsCheckBox.IsChecked == true,
            DisableMTVU = PS2DisableMTVUCheckBox.IsChecked == true,
            DisableInstantVIF1Transfer = PS2DisableInstantVIF1TransferCheckBox.IsChecked == true,
            UseCompression = PS2UseCompressionCheckBox.IsChecked == true,
        };

        await RunBuild(BuildPS2fPKGButton, BuildPS2fPKGButtonText, PS2BuildProgress, folder => PS2PackageBuilder.Build(request, folder));
    }

    #endregion

    #region PSP

    private void WireUpPSP()
    {
        ForceUpperCase(PSPNPTitleTextBox);
        BrowsePSPDiscButton.Click += BrowsePSPDiscButton_Click;
        BrowseImage(BrowsePSPIconButton, SelectedPSPIconTextBox, true);
        BrowseImage(BrowsePSPBGButton, SelectedPSPBGImageTextBox, false);
        Browse(BrowsePSPConfigButton, SelectedPSPConfigTextBox, "Select a config-title.txt file.", TxtFiles);

        SelectedPSPIconTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        SelectedPSPBGImageTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        PSPTitleTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        PSPPS5Radio.IsCheckedChanged += (_, _) => UpdatePSPPreview();

        BuildPSPfPKGButton.Click += BuildPSPfPKGButton_Click;
    }

    private void UpdatePSPPreview() =>
        UpdatePreview(PSPPreview, SelectedPSPIconTextBox.Text, SelectedPSPBGImageTextBox.Text, null, null,
            PSPTitleTextBox.Text, PSPPS5Radio.IsChecked == true, PSPIconThumbnail, PSPBackgroundThumbnail, PSPHintsText);

    private async void BrowsePSPDiscButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFile("Select a PSP ISO file.", IsoFiles);
        if (file == null) return;

        if (IsoTools.FileExistInISO(file, "\\PSP_GAME\\PARAM.SFO"))
        {
            Directory.CreateDirectory(AppPaths.CacheDirectory);
            var tempUMDDataFile = Path.Combine(AppPaths.CacheDirectory, "temp_umd_data.bin");
            var extracted = IsoTools.ExtractFileFromPSPISO(file, "UMD_DATA.BIN", tempUMDDataFile);

            if (!string.IsNullOrEmpty(extracted))
            {
                PSPNPTitleTextBox.Text = Encoding.ASCII.GetString(PspTools.ReadBytes(tempUMDDataFile, 0, 10)).Replace("-", "");
                var title = Regex.Replace(Path.GetFileNameWithoutExtension(file), "\\((.*?)\\)", "");
                PSPTitleTextBox.Text = Regex.Replace(title, " {2,}", "");
                File.Delete(tempUMDDataFile);
            }

            SelectedPSPDiscTextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, "Unknown PSP ISO file",
                     "Could not find any PSP game information within the ISO file.\nDo you want to use this file anyway ?"))
        {
            SelectedPSPDiscTextBox.Text = file;
        }
    }

    private async void BuildPSPfPKGButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRequiredFields(SelectedPSPDiscTextBox.Text, PSPTitleTextBox.Text, PSPNPTitleTextBox.Text)) return;

        var request = new PSPBuildRequest
        {
            Title = PSPTitleTextBox.Text,
            NPTitle = PSPNPTitleTextBox.Text,
            IsoFile = SelectedPSPDiscTextBox.Text,
            IconFile = SelectedPSPIconTextBox.Text ?? "",
            BackgroundFile = SelectedPSPBGImageTextBox.Text ?? "",
            ConfigFile = SelectedPSPConfigTextBox.Text ?? "",
            Warn = WarnFromBuildThread,
        };

        await RunBuild(BuildPSPfPKGButton, BuildPSPfPKGButtonText, PSPBuildProgress, folder => PSPPackageBuilder.Build(request, folder));
    }

    #endregion
}
