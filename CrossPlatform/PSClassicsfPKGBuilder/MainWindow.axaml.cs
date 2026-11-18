using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
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
    private static readonly FilePickerFileType PspImageFiles = new("PSP image (ISO, CSO)") { Patterns = new[] { "*.iso", "*.cso", "*.ISO", "*.CSO" } };
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
    private bool _fillingPresets;

    public MainWindow()
    {
        InitializeComponent();
        WireUpNavigation();
        WireUpPS1();
        WireUpPS2();
        WireUpPSP();
        SetUpStepBar(PS1StepBar, 0);
        SetUpStepBar(PS2StepBar, 1);
        SetUpStepBar(PSPStepBar, 2);
        FillPresets();
        UpdateAllPreviews();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        UiTranslator.Attach(this);
        Localizer.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(OnLanguageChanged);
        ApplyGuidedMode();
        Opened += async (_, _) =>
        {
            ShowKnownUpdate();
            await CheckEnvironment();
            await CheckForUpdates();
        };
    }

    #region Startup, language and settings

    private static string EmulatorsDirectory => Path.Combine(AppPaths.ToolsDirectory, "PS4", "emus");

    private void WireUpNavigation()
    {
        PlatformListBox.SelectionChanged += (_, _) =>
        {
            PS1Page.IsVisible = PlatformListBox.SelectedIndex == 0;
            PS2Page.IsVisible = PlatformListBox.SelectedIndex == 1;
            PSPPage.IsVisible = PlatformListBox.SelectedIndex == 2;
        };
        VerifyPkgButton.Click += async (_, _) =>
        {
            var pkgFile = await PickFile("Select the .pkg to check.", PkgFiles);
            if (pkgFile != null) await CheckPackage(pkgFile);
        };
        HistoryButton.Click += async (_, _) => await new HistoryWindow().ShowDialog(this);
        OpenGuideButton.Click += async (_, _) => await new GuideWindow().ShowDialog(this);
        SettingsButton.Click += async (_, _) =>
        {
            await new SettingsWindow().ShowDialog(this);
            ApplyGuidedMode();
            ShowKnownUpdate();
        };
        WineSetupButton.Click += async (_, _) =>
        {
            await new WineSetupWindow().ShowDialog(this);
            UpdateWineStatus();
        };
        DownloadUpdateButton.Click += (_, _) =>
        {
            var url = string.IsNullOrEmpty(AppSettings.Current.LatestVersionUrl) ? UpdateChecker.ReleasesPage : AppSettings.Current.LatestVersionUrl;
            OpenUrl(url);
        };
        SkipUpdateButton.Click += (_, _) =>
        {
            AppSettings.Current.SkippedVersion = AppSettings.Current.LatestVersionTag;
            AppSettings.Current.Save();
            UpdatePanel.IsVisible = false;
        };
        WineSetupButton.IsVisible = !Platform.IsWindows;
        WineStatusPanel.IsVisible = !Platform.IsWindows;
    }

    /// <summary>Texts set by code follow the language too (the XAML texts are handled by <see cref="UiTranslator"/>).</summary>
    private void OnLanguageChanged()
    {
        FillPresets();
        UpdateToolsStatus();
        UpdateWineStatus();
        UpdateEmulatorHint();
        ShowKnownUpdate();
        UpdateAllPreviews();
        for (var page = 0; page < 3; page++) ShowStep(page);
    }

    private void UpdateToolsStatus()
    {
        var found = AppPaths.ToolsAvailable();
        ToolsStatusText.Text = found ? Localizer.T("Tools folder found") : Localizer.T("Tools folder missing");
        ToolsStatusIcon.Kind = found ? MaterialIconKind.CheckCircleOutline : MaterialIconKind.AlertOutline;
        ToolsStatusIcon.Foreground = found ? Brushes.White : new SolidColorBrush(Color.Parse("#FFC857"));
    }

    /// <summary>Shows the state of the Tools folder (and of Wine on Linux/macOS) and warns once when something is missing.</summary>
    private async Task CheckEnvironment()
    {
        UpdateToolsStatus();
        if (!AppPaths.ToolsAvailable())
        {
            await MessageDialog.ShowError(this, Localizer.T("Tools folder missing"),
                Localizer.F("The 'Tools' folder was not found at:\n{0}\n\nBuilding fPKGs will fail until it is copied next to the application (see README.md, 'Building from source').", AppPaths.ToolsDirectory));
        }

        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            if (await MessageDialog.Confirm(this, Localizer.T("Wine is not ready"),
                    Localizer.T("The PS4 publishing tools are Windows programs and run through Wine on this system. Wine is not ready yet.\n\nOpen the Wine setup now?"),
                    Localizer.T("Open Wine setup"), Localizer.T("Later")))
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
            WineStatusText.Text = Localizer.T("Wine ready");
            WineStatusIcon.Kind = MaterialIconKind.CheckCircleOutline;
            WineStatusIcon.Foreground = Brushes.White;
        }
        else
        {
            WineStatusText.Text = status.WineFound ? Localizer.T("Wine needs setup") : Localizer.T("Wine not found");
            WineStatusIcon.Kind = MaterialIconKind.AlertOutline;
            WineStatusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFC857"));
        }
        return status.IsReady;
    }

    #endregion

    #region Updates

    /// <summary>Shows the banner when a newer version is already known (from an earlier check).</summary>
    private void ShowKnownUpdate()
    {
        var settings = AppSettings.Current;
        var known = UpdateChecker.ParseVersion(settings.LatestVersionTag);
        var isNewer = known != null && UpdateChecker.IsNewer(known, UpdateChecker.CurrentVersion());
        UpdatePanel.IsVisible = settings.CheckForUpdates && isNewer && settings.LatestVersionTag != settings.SkippedVersion;
        if (UpdatePanel.IsVisible) UpdateText.Text = Localizer.F("Version {0} is available.", settings.LatestVersionTag);
    }

    private async Task CheckForUpdates()
    {
        var settings = AppSettings.Current;
        if (!UpdateChecker.IsCheckDue(settings, DateTime.Now)) return;
        var info = await UpdateChecker.CheckAsync();
        if (info == null) return;
        settings.LastUpdateCheck = DateTime.Now;
        settings.LatestVersionTag = info.Tag;
        settings.LatestVersionUrl = info.Url;
        settings.Save();
        ShowKnownUpdate();
    }

    #endregion

    #region Step-by-step mode

    private StepBar StepBarOf(int page) => new[] { PS1StepBar, PS2StepBar, PSPStepBar }[page];

    /// <summary>Sections shown in each step of a page (steps 0 to 2; step 3 is the review card).</summary>
    private Control[][] StepSections(int page) => page switch
    {
        0 => new[] { new Control[] { PS1GameSection }, new Control[] { PS1ImagesSection }, new Control[] { PS1DiscsSection, PS1EmulatorSection, PS1AdvancedSection } },
        1 => new[] { new Control[] { PS2GameSection }, new Control[] { PS2ImagesSection }, new Control[] { PS2DiscsSection, PS2EmulatorSection, PS2ConfigsSection } },
        _ => new[] { new Control[] { PSPGameSection }, new Control[] { PSPImagesSection }, new Control[] { PSPEmulatorSection } },
    };

    private void SetUpStepBar(StepBar bar, int page)
    {
        bar.CanLeaveStep = step => CanLeaveStep(page, step);
        bar.StepChanged += (_, _) => ShowStep(page);
    }

    private void ApplyGuidedMode()
    {
        var guided = AppSettings.Current.GuidedMode;
        for (var page = 0; page < 3; page++)
        {
            StepBarOf(page).IsVisible = guided;
            ShowStep(page);
        }
    }

    private void ShowStep(int page)
    {
        var guided = AppSettings.Current.GuidedMode;
        var currentStep = StepBarOf(page).CurrentStep;
        var steps = StepSections(page);
        for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
        {
            foreach (var section in steps[stepIndex])
            {
                section.IsVisible = !guided || stepIndex == currentStep;
                if (guided && stepIndex == currentStep && section is Expander expander) expander.IsExpanded = true;
            }
        }
        var reviewCard = new Control[] { PS1ReviewCard, PS2ReviewCard, PSPReviewCard }[page];
        reviewCard.IsVisible = guided && currentStep == StepBar.StepCount - 1;
        if (reviewCard.IsVisible) UpdateReview(page);
    }

    /// <summary>Step 1 needs the game, a title and a valid NP title before going on.</summary>
    private async Task<bool> CanLeaveStep(int page, int currentStep)
    {
        if (currentStep != 0) return true;
        var disc = new[] { PS1SelectedDisc1TextBox.Text, SelectedDisc1TextBox.Text, SelectedPSPDiscTextBox.Text }[page];
        var title = new[] { PS1TitleTextBox.Text, PS2TitleTextBox.Text, PSPTitleTextBox.Text }[page];
        var npTitle = new[] { PS1NPTitleTextBox.Text, PS2NPTitleTextBox.Text, PSPNPTitleTextBox.Text }[page];
        var problems = new List<PreflightIssue>();
        if (string.IsNullOrWhiteSpace(disc))
            problems.Add(new PreflightIssue { IsError = true, Message = Localizer.T("Select the game file first."), Suggestion = Localizer.T("Press Browse… next to Disc 1, or drag the game onto the window.") });
        if (string.IsNullOrWhiteSpace(title))
            problems.Add(new PreflightIssue { IsError = true, Message = Localizer.T("No game title specified."), Suggestion = Localizer.T("Type the name that the home screen will show.") });
        problems.AddRange(BuildPreflight.CheckNPTitle(npTitle ?? "").Where(issue => issue.IsError));
        if (problems.Count == 0) return true;
        await MessageDialog.ShowError(this, Localizer.T("Before the next step"), BuildPreflight.Describe(problems));
        return false;
    }

    private void UpdateReview(int page)
    {
        static string Describe(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : Path.GetFileName(value);
        var lines = new List<string>();
        switch (page)
        {
            case 0:
            {
                var request = CreatePS1Request();
                lines.AddRange(ReviewHeader(request.Title, request.NPTitle, request.Disc1, request.Disc2, request.Disc3, request.Disc4));
                lines.Add(Localizer.T("Icon") + ": " + Describe(request.IconFile, Localizer.T("default of the emulator")));
                lines.Add(Localizer.T("Background") + ": " + Describe(request.BackgroundFile, Localizer.T("default of the emulator")));
                lines.Add("");
                lines.Add(BuildHistory.Summarize(request));
                PS1ReviewText.Text = string.Join("\n", lines);
                break;
            }
            case 1:
            {
                var request = CreatePS2Request();
                lines.AddRange(ReviewHeader(request.Title, request.NPTitle, request.Disc1, request.Disc2, request.Disc3, request.Disc4, request.Disc5));
                lines.Add(Localizer.T("Icon") + ": " + Describe(request.IconFile, Localizer.T("default of the emulator")));
                lines.Add(Localizer.T("Background") + ": " + Describe(request.BackgroundFile, Localizer.T("default of the emulator")));
                lines.Add("");
                lines.Add(BuildHistory.Summarize(request));
                PS2ReviewText.Text = string.Join("\n", lines);
                break;
            }
            default:
            {
                var request = CreatePSPRequest();
                lines.AddRange(ReviewHeader(request.Title, request.NPTitle, request.IsoFile));
                lines.Add(Localizer.T("Icon") + ": " + Describe(request.IconFile, Localizer.T("none (empty tile)")));
                lines.Add(Localizer.T("Background") + ": " + Describe(request.BackgroundFile, Localizer.T("none")));
                lines.Add("");
                lines.Add(BuildHistory.Summarize(request));
                PSPReviewText.Text = string.Join("\n", lines);
                break;
            }
        }
    }

    private static List<string> ReviewHeader(string title, string npTitle, params string[] discs)
    {
        var selected = discs.Where(d => !string.IsNullOrWhiteSpace(d)).Select(Path.GetFileName).ToArray();
        return new List<string>
        {
            Localizer.T("Title") + ": " + title,
            "NP Title: " + npTitle,
            Localizer.T("Discs") + ": " + (selected.Length == 0 ? "-" : string.Join(", ", selected)),
        };
    }

    #endregion

    #region Presets

    private void FillPresets()
    {
        _fillingPresets = true;
        var ps1Selected = PS1PresetComboBox.SelectedIndex;
        var ps2Selected = PS2PresetComboBox.SelectedIndex;
        PS1PresetComboBox.Items.Clear();
        foreach (var preset in EmulatorPresets.PS1) PS1PresetComboBox.Items.Add(Localizer.T(preset.Name));
        PS2PresetComboBox.Items.Clear();
        foreach (var preset in EmulatorPresets.PS2) PS2PresetComboBox.Items.Add(Localizer.T(preset.Name));
        PS1PresetComboBox.SelectedIndex = ps1Selected;
        PS2PresetComboBox.SelectedIndex = ps2Selected;
        if (ps1Selected >= 0) PS1PresetHint.Text = PresetHint(EmulatorPresets.PS1[ps1Selected].Description);
        if (ps2Selected >= 0) PS2PresetHint.Text = PresetHint(EmulatorPresets.PS2[ps2Selected].Description);
        _fillingPresets = false;
    }

    private static string PresetHint(string description) =>
        Localizer.T(description) + " " + Localizer.T("You can still change any option below.");

    private static void SelectItem(ComboBox combo, string value)
    {
        for (var index = 0; index < combo.Items.Count; index++)
        {
            if ((combo.Items[index] as ComboBoxItem)?.Content as string == value)
            {
                combo.SelectedIndex = index;
                return;
            }
        }
    }

    private void ApplyPS1Preset()
    {
        if (_fillingPresets || PS1PresetComboBox.SelectedIndex < 0) return;
        var preset = EmulatorPresets.PS1[PS1PresetComboBox.SelectedIndex];
        SelectItem(PS1UpscalingComboBox, preset.Upscaling);
        PS1SkipBootlogoCheckBox.IsChecked = preset.SkipBootlogo;
        PS1EmulateAnalogSticksCheckBox.IsChecked = preset.EmulateAnalogSticks;
        PS1Force60HzCheckBox.IsChecked = preset.Force60Hz;
        PS1PresetHint.Text = PresetHint(preset.Description);
    }

    private void ApplyPS2Preset()
    {
        if (_fillingPresets || PS2PresetComboBox.SelectedIndex < 0) return;
        var preset = EmulatorPresets.PS2[PS2PresetComboBox.SelectedIndex];
        SelectItem(PS2EmulatorComboBox, preset.Emulator);
        SelectItem(PS2UpscalingComboBox, preset.Upscaling);
        SelectItem(PS2UprenderComboBox, preset.Uprender);
        SelectItem(PS2DisplayModeComboBox, preset.DisplayMode);
        PS2FixGraphicsCheckBox.IsChecked = preset.FixGraphics;
        PS2ImproveSpeedCheckBox.IsChecked = preset.ImproveSpeed;
        PS2DisableMTVUCheckBox.IsChecked = preset.DisableMTVU;
        PS2DisableInstantVIF1TransferCheckBox.IsChecked = preset.DisableInstantVIF1Transfer;
        if (PS2UseWidescreenPatchCheckBox.IsEnabled) PS2UseWidescreenPatchCheckBox.IsChecked = preset.WidescreenPatchWhenAvailable;
        PS2UseCompressionCheckBox.IsChecked = preset.UseCompression;
        PS2PresetHint.Text = PresetHint(preset.Description);
    }

    #endregion

    #region Shared helpers

    private async Task<string> PickFile(string title, params FilePickerFileType[] types)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.T(title),
            AllowMultiple = false,
            FileTypeFilter = types,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string> PickOutputFolder()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Localizer.T("Please select an output folder"),
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

    private async Task CheckPackage(string pkgFile)
    {
        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            await MessageDialog.ShowError(this, Localizer.T("Check a .pkg"), Localizer.T("Wine is not ready. Press 'Wine setup' first."));
            return;
        }

        VerifyPkgButton.IsEnabled = false;
        VerifyPkgButtonText.Text = Localizer.T("Checking…");
        try
        {
            var report = await Task.Run(() => PackageVerifier.Verify(pkgFile));
            await new PackageReportWindow(report).ShowDialog(this);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowError(this, Localizer.T("Check a .pkg"), Localizer.F("The package could not be checked:\n{0}", ex.Message));
        }
        finally
        {
            VerifyPkgButtonText.Text = Localizer.T("Check a .pkg");
            VerifyPkgButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Common build flow (same as the Windows app): checks before building (with fixes), output folder, build in the
    /// background, automatic package check, history entry and a result window that explains failures in plain words.
    /// </summary>
    private async Task RunBuild(string platformName, string title, string npTitle, string summary,
        Func<string, List<PreflightIssue>> preflight, Func<string, PackageBuildResult> build,
        Button buildButton, TextBlock buildButtonText, ProgressBar progress)
    {
        if (_building) return;

        if (!Platform.IsWindows && !UpdateWineStatus())
        {
            await MessageDialog.ShowError(this, Localizer.T("Cannot create fPKG"),
                Localizer.T("Wine is not ready, so the PS4 tools cannot run. Press 'Wine setup' at the bottom left and follow the steps."));
            return;
        }

        // Problems that do not depend on the output folder are shown before asking for it
        var issues = preflight(null);
        if (!await ConfirmPreflight(issues)) return;

        var outputFolder = await PickOutputFolder();
        if (outputFolder == null) return;

        // Free space on the chosen drive
        var spaceIssues = preflight(outputFolder).Where(issue => issues.All(known => known.Message != issue.Message)).ToList();
        if (spaceIssues.Any(issue => issue.IsError))
        {
            await MessageDialog.ShowError(this, Localizer.T("Cannot create fPKG"), BuildPreflight.Describe(spaceIssues));
            return;
        }

        _building = true;
        buildButton.IsEnabled = false;
        buildButtonText.Text = Localizer.T("Building… (this can take a few minutes)");
        progress.IsVisible = true;

        PackageBuildResult result = null;
        PackageReport report = null;
        var errorDetails = "";
        try
        {
            result = await Task.Run(() => build(outputFolder));
            if (!result.Success) errorDetails = result.Output;
            if (result.Success && AppSettings.Current.CheckPackageAfterBuild && result.PackagePath != "")
            {
                buildButtonText.Text = Localizer.T("Checking the package…");
                var pkgFile = result.PackagePath;
                report = await Task.Run(() => PackageVerifier.Verify(pkgFile, false));
            }
        }
        catch (Exception ex)
        {
            errorDetails = ex.Message;
        }
        finally
        {
            _building = false;
            buildButton.IsEnabled = true;
            buildButtonText.Text = Localizer.T("Build fPKG");
            progress.IsVisible = false;
        }

        BuildHistory.AddOrUpdate(new HistoryEntry
        {
            Platform = platformName, Title = title, NPTitle = npTitle, Settings = summary, OutputFolder = outputFolder,
            PackagePath = result?.PackagePath ?? "", Succeeded = result is { Success: true },
        });

        await new BuildResultWindow(result, outputFolder, report, errorDetails).ShowDialog(this);
    }

    /// <summary>Shows the problems found before a build. Returns false when the build must not start.</summary>
    private async Task<bool> ConfirmPreflight(List<PreflightIssue> issues)
    {
        if (issues.Count == 0) return true;
        if (issues.Any(issue => issue.IsError))
        {
            await MessageDialog.ShowError(this, Localizer.T("Cannot create fPKG"), BuildPreflight.Describe(issues));
            return false;
        }
        return await MessageDialog.Confirm(this, Localizer.T("Before building"), BuildPreflight.Describe(issues) + "\n\n" + Localizer.T("Continue anyway?"));
    }

    public static void OpenFolder(string folder) => Open(Platform.IsWindows ? "explorer" : Platform.IsMacOS ? "open" : "xdg-open", folder);

    private static void OpenUrl(string url) => Open(Platform.IsWindows ? "explorer" : Platform.IsMacOS ? "open" : "xdg-open", url);

    private static void Open(string opener, string target)
    {
        try
        {
            var info = new ProcessStartInfo(opener) { UseShellExecute = false };
            info.ArgumentList.Add(target);
            Process.Start(info);
        }
        catch (Exception)
        {
            // Nothing to do: the path is shown in the window anyway
        }
    }

    /// <summary>Asks a yes/no question from the build thread (callbacks of the build requests) and waits for the answer.</summary>
    private bool AskFromBuildThread(string title, string message) =>
        Dispatcher.UIThread.InvokeAsync(() => MessageDialog.Confirm(this, Localizer.T(title), Localizer.T(message))).GetAwaiter().GetResult();

    private void WarnFromBuildThread(string message) =>
        Dispatcher.UIThread.InvokeAsync(() => MessageDialog.ShowInfo(this, Localizer.T("Warning"), Localizer.T(message))).GetAwaiter().GetResult();

    /// <summary>
    /// Finds the cover (PS1/PS2) through <see cref="CoverSources"/> (ID collection, then the Redump name, then the title)
    /// and fills the icon, and the background when it is empty or the user agrees.
    /// </summary>
    private async Task FindCover(string platformName, string gameIdOrNPTitle, string titleText, TextBox iconBox, TextBox backgroundBox, Button button, TextBlock buttonText)
    {
        if (CoverArt.NormalizeSerial(gameIdOrNPTitle) == "" && string.IsNullOrWhiteSpace(titleText))
        {
            await MessageDialog.ShowInfo(this, Localizer.T("Find cover"), Localizer.T("Select the game first: the game ID (or the NP title) is needed to find its cover."));
            return;
        }
        button.IsEnabled = false;
        buttonText.Text = Localizer.T("Searching…");
        try
        {
            var found = await CoverSources.FindCoverAsync(platformName, gameIdOrNPTitle, titleText);
            if (found == null)
            {
                await MessageDialog.ShowInfo(this, Localizer.T("Find cover"), Localizer.F("No cover was found for {0}. Choose an image yourself.", CoverArt.NormalizeSerial(gameIdOrNPTitle) + " " + titleText));
                return;
            }
            if (found.Source != CoverSource.IdCollection)
                await MessageDialog.ShowInfo(this, Localizer.T("Find cover"), Localizer.T("The cover was found by the game name, not by its ID. Check the icon and the background in the preview before building."));
            var name = CoverArt.NormalizeSerial(gameIdOrNPTitle) + "-" + found.Source;
            var images = await Task.Run(() => CoverArt.CreateImages(found.Image, name));
            await ApplyImages(images, iconBox, backgroundBox);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowError(this, Localizer.T("Find cover"), Localizer.F("The cover could not be downloaded: {0}", ex.Message));
        }
        finally
        {
            buttonText.Text = Localizer.T("Find cover");
            button.IsEnabled = true;
        }
    }

    private async Task ApplyImages(CoverArt.GeneratedImages images, TextBox iconBox, TextBox backgroundBox)
    {
        iconBox.Text = images.IconFile;
        if (string.IsNullOrWhiteSpace(backgroundBox.Text) ||
            await MessageDialog.Confirm(this, Localizer.T("Background"), Localizer.T("Also replace the background with the one made from these images?")))
            backgroundBox.Text = images.BackgroundFile;
    }

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

    #region Drag and drop

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        e.Handled = true;
        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) continue;
            if (ArchiveImport.IsArchive(path)) await HandleDroppedArchive(path);
            else await HandleDroppedFile(path);
        }
    }

    /// <summary>Extracts a dropped .zip or .7z and handles the game files in it as if they were dropped one by one.</summary>
    private async Task HandleDroppedArchive(string archiveFile)
    {
        List<ImportedFile> found;
        try
        {
            // Look inside first: an incomplete or protected archive is reported before any file is extracted
            var report = await Task.Run(() => ArchiveInspector.Inspect(archiveFile));
            if (!report.IsOk)
            {
                await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", report.Describe());
                return;
            }
            found = await Task.Run(() => ArchiveImport.FindGameFiles(ArchiveImport.Extract(archiveFile)));
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.F("The archive could not be opened: {0}", ex.Message));
            return;
        }
        if (found.Count == 0)
        {
            await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.T("No game files were found in this archive. A PS1 game needs its .bin and .cue files, a PS2 or PSP game its .iso file."));
            return;
        }
        foreach (var item in found)
            await HandleDroppedFile(item.Path, item.Kind);
    }

    /// <summary>Puts a dropped file in the right field (switching to the right platform for games).</summary>
    private async Task HandleDroppedFile(string droppedFile, DroppedFileKind? knownKind = null)
    {
        var page = PlatformListBox.SelectedIndex;
        switch (knownKind ?? DroppedFiles.Classify(droppedFile))
        {
            case DroppedFileKind.PS1Disc:
                var bin = DroppedFiles.DiscFile(droppedFile);
                if (bin == "")
                {
                    await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.T("The .BIN file of this .CUE was not found next to it."));
                    return;
                }
                PlatformListBox.SelectedIndex = 0;
                await LoadPS1Disc1(bin);
                break;
            case DroppedFileKind.PS2Disc:
                PlatformListBox.SelectedIndex = 1;
                await LoadPS2Disc1(droppedFile);
                break;
            case DroppedFileKind.PSPDisc:
                PlatformListBox.SelectedIndex = 2;
                await LoadPSPDisc(droppedFile);
                break;
            case DroppedFileKind.Icon:
                new[] { SelectedPS1IconTextBox, SelectedIconTextBox, SelectedPSPIconTextBox }[page].Text = droppedFile;
                break;
            case DroppedFileKind.Background:
                new[] { SelectedPS1BGTextBox, SelectedStartupImageTextBox, SelectedPSPBGImageTextBox }[page].Text = droppedFile;
                break;
            case DroppedFileKind.TxtConfig:
                new[] { SelectedPS1TXTConfigTextBox, SelectedPS2TXTConfigTextBox, SelectedPSPConfigTextBox }[page].Text = droppedFile;
                break;
            case DroppedFileKind.LuaConfig:
                if (page == 2)
                    await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.T("PSP packages do not use LUA configs."));
                else
                    new[] { SelectedPS1LUAConfigTextBox, SelectedPS2LUAConfigTextBox }[page].Text = droppedFile;
                break;
            case DroppedFileKind.MemoryCard:
                PlatformListBox.SelectedIndex = 1;
                SelectedPS2MemoryCardTextBox.Text = droppedFile;
                break;
            case DroppedFileKind.Package:
                await CheckPackage(droppedFile);
                break;
            default:
                await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.F("This file type is not supported: {0}", Path.GetFileName(droppedFile)));
                break;
        }
    }

    #endregion

    #region PS1

    private void WireUpPS1()
    {
        ForceUpperCase(PS1NPTitleTextBox);
        BrowsePS1Disc1Button.Click += async (_, _) =>
        {
            var file = await PickFile("Select a PS1 BIN file.", BinFiles);
            if (file != null) await LoadPS1Disc1(file);
        };
        BrowseImage(BrowsePS1IconButton, SelectedPS1IconTextBox, true);
        BrowseImage(BrowsePS1StartupImageButton, SelectedPS1BGTextBox, false);
        Browse(BrowsePS1Disc2Button, PS1SelectedDisc2TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1Disc3Button, PS1SelectedDisc3TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1Disc4Button, PS1SelectedDisc4TextBox, "Select a PS1 BIN file.", BinFiles);
        Browse(BrowsePS1TXTConfigButton, SelectedPS1TXTConfigTextBox, "Select a TXT config file.", TxtFiles);
        Browse(BrowsePS1LUAConfigButton, SelectedPS1LUAConfigTextBox, "Select a LUA config file.", LuaFiles);
        PS1FindCoverButton.Click += async (_, _) =>
            await FindCover("PS1", _currentPS1GameID != "" ? _currentPS1GameID : PS1NPTitleTextBox.Text, PS1TitleTextBox.Text, SelectedPS1IconTextBox, SelectedPS1BGTextBox, PS1FindCoverButton, PS1FindCoverButtonText);
        PS1PresetComboBox.SelectionChanged += (_, _) => ApplyPS1Preset();

        SelectedPS1IconTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        SelectedPS1BGTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        PS1TitleTextBox.TextChanged += (_, _) => UpdatePS1Preview();
        PS1PS5Radio.IsCheckedChanged += (_, _) => UpdatePS1Preview();

        BuildPS1fPKGButton.Click += async (_, _) =>
        {
            var request = CreatePS1Request();
            await RunBuild("PS1", request.Title, request.NPTitle, BuildHistory.Summarize(request),
                folder => BuildPreflight.CheckPS1(request, folder),
                folder => PS1PackageBuilder.Build(request, folder),
                BuildPS1fPKGButton, BuildPS1fPKGButtonText, PS1BuildProgress);
        };
    }

    private void UpdatePS1Preview()
    {
        var sceSys = Path.Combine(EmulatorsDirectory, "ps1hd", "sce_sys");
        UpdatePreview(PS1Preview, SelectedPS1IconTextBox.Text, SelectedPS1BGTextBox.Text,
            Path.Combine(sceSys, "icon0.png"), Path.Combine(sceSys, "pic1.png"),
            PS1TitleTextBox.Text, PS1PS5Radio.IsChecked == true, PS1IconThumbnail, PS1BackgroundThumbnail, PS1HintsText);
    }

    /// <summary>For a game that is not in the title list: asks the Redump database for the title, when the title box is still empty.</summary>
    private static async void FillTitleFromRedump(string platform, string gameId, TextBox titleBox)
    {
        if (!string.IsNullOrWhiteSpace(titleBox.Text)) return;
        var title = await CoverSources.FindTitleAsync(platform, gameId);
        if (title != "" && string.IsNullOrWhiteSpace(titleBox.Text)) titleBox.Text = title;
    }

    private async Task LoadPS1Disc1(string file)
    {
        // Get the game ID from the BIN file and a title from the master list
        var gameID = await Task.Run(() => PS1Game.GetPS1GameID(file)) ?? "";
        var gameTitle = gameID.Length == 10 ? PS1Game.GetPS1GameTitleFromDatabaseList(gameID) : "";

        // Forget the ID of a previously selected game (it must not leak into the libcrypt check of this one)
        _currentPS1GameID = gameID.Trim();

        if (!string.IsNullOrEmpty(gameID))
        {
            PS1NPTitleTextBox.Text = gameID.Replace("-", "").Trim();
            PS1SelectedDisc1TextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, Localizer.T("Game ID not found"),
                     Localizer.T("Cannot find the game ID within the PS1 BIN file, do you want to use this file anyway ?\nDo not forget to specify a NP title and title if you do so.")))
        {
            PS1SelectedDisc1TextBox.Text = file;
        }

        if (!string.IsNullOrEmpty(gameTitle)) PS1TitleTextBox.Text = gameTitle;
        else if (gameID != "") FillTitleFromRedump("PS1", gameID, PS1TitleTextBox);
    }

    private PS1BuildRequest CreatePS1Request() => new()
    {
        Title = PS1TitleTextBox.Text ?? "",
        NPTitle = PS1NPTitleTextBox.Text ?? "",
        DetectedGameID = _currentPS1GameID,
        Disc1 = PS1SelectedDisc1TextBox.Text ?? "",
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

    #endregion

    #region PS2

    private void WireUpPS2()
    {
        ForceUpperCase(PS2NPTitleTextBox);
        BrowsePS2Disc1Button.Click += async (_, _) =>
        {
            var file = await PickFile("Select a PS2 ISO file.", IsoFiles);
            if (file != null) await LoadPS2Disc1(file);
        };
        BrowseImage(BrowsePS2IconButton, SelectedIconTextBox, true);
        BrowseImage(BrowsePS2StartupImageButton, SelectedStartupImageTextBox, false);
        Browse(BrowsePS2MCButton, SelectedPS2MemoryCardTextBox, "Select a PS2 Memory Card file.", MemoryCardFiles);
        Browse(BrowsePS2TXTConfigButton, SelectedPS2TXTConfigTextBox, "Select a TXT config file.", TxtFiles);
        Browse(BrowsePS2LUAConfigButton, SelectedPS2LUAConfigTextBox, "Select a LUA config file.", LuaFiles);
        Browse(BrowsePS2Disc2Button, SelectedDisc2TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc3Button, SelectedDisc3TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc4Button, SelectedDisc4TextBox, "Select a PS2 ISO file.", IsoFiles);
        Browse(BrowsePS2Disc5Button, SelectedDisc5TextBox, "Select a PS2 ISO file.", IsoFiles);
        PS2FindCoverButton.Click += async (_, _) =>
            await FindCover("PS2", _currentPS2GameID != "" ? _currentPS2GameID : PS2NPTitleTextBox.Text, PS2TitleTextBox.Text, SelectedIconTextBox, SelectedStartupImageTextBox, PS2FindCoverButton, PS2FindCoverButtonText);
        PS2PresetComboBox.SelectionChanged += (_, _) => ApplyPS2Preset();

        PS2EmulatorComboBox.SelectionChanged += (_, _) =>
        {
            UpdatePS2Preview();
            UpdateEmulatorHint();
        };
        SelectedIconTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        SelectedStartupImageTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        PS2TitleTextBox.TextChanged += (_, _) => UpdatePS2Preview();
        PS2PS5Radio.IsCheckedChanged += (_, _) => UpdatePS2Preview();

        BuildPS2fPKGButton.Click += async (_, _) =>
        {
            var request = CreatePS2Request();
            await RunBuild("PS2", request.Title, request.NPTitle, BuildHistory.Summarize(request),
                folder => BuildPreflight.CheckPS2(request, folder),
                folder => PS2PackageBuilder.Build(request, folder),
                BuildPS2fPKGButton, BuildPS2fPKGButtonText, PS2BuildProgress);
        };
    }

    /// <summary>Hint under the emulator field: describes the choice that is selected now.</summary>
    private void UpdateEmulatorHint() =>
        PS2EmulatorHint.Text = SelectedText(PS2EmulatorComboBox) == "Rogue v1"
            ? Localizer.T("Rogue v1 selected. If the game does not start or crashes, switch back to Jak v2 (the default).")
            : Localizer.T("Default: Jak v2. If the game does not start or crashes, try Rogue v1.");

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

    private async Task LoadPS2Disc1(string file)
    {
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
            if (string.IsNullOrEmpty(PS2TitleTextBox.Text)) FillTitleFromRedump("PS2", gameID, PS2TitleTextBox);
            PS2NPTitleTextBox.Text = gameID.Replace(".", "").Replace("_", "").Trim();
            SelectedDisc1TextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, Localizer.T("PS2 Game ID not found"),
                     Localizer.T("Could not find the PS2 game ID within the ISO file.\nDo you want to use this file anyway ?")))
        {
            SelectedDisc1TextBox.Text = file;
        }
    }

    private static void ShowDatabaseOption(CheckBox box, bool available)
    {
        box.IsChecked = available;
        box.IsVisible = available;
    }

    private PS2BuildRequest CreatePS2Request() => new()
    {
        Title = PS2TitleTextBox.Text ?? "",
        NPTitle = PS2NPTitleTextBox.Text ?? "",
        DetectedGameID = _currentPS2GameID,
        GameCRC = _currentPS2GameCRC,
        Disc1 = SelectedDisc1TextBox.Text ?? "",
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

    #endregion

    #region PSP

    private void WireUpPSP()
    {
        ForceUpperCase(PSPNPTitleTextBox);
        BrowsePSPDiscButton.Click += async (_, _) =>
        {
            var file = await PickFile("Select a PSP ISO file.", PspImageFiles);
            if (file != null) await LoadPSPDisc(file);
        };
        BrowseImage(BrowsePSPIconButton, SelectedPSPIconTextBox, true);
        BrowseImage(BrowsePSPBGButton, SelectedPSPBGImageTextBox, false);
        Browse(BrowsePSPConfigButton, SelectedPSPConfigTextBox, "Select a config-title.txt file.", TxtFiles);
        PSPDiscImagesButton.Click += async (_, _) => await UseDiscImages();

        SelectedPSPIconTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        SelectedPSPBGImageTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        PSPTitleTextBox.TextChanged += (_, _) => UpdatePSPPreview();
        PSPPS5Radio.IsCheckedChanged += (_, _) => UpdatePSPPreview();

        BuildPSPfPKGButton.Click += async (_, _) =>
        {
            var request = CreatePSPRequest();
            await RunBuild("PSP", request.Title, request.NPTitle, BuildHistory.Summarize(request),
                folder => BuildPreflight.CheckPSP(request, folder),
                folder => PSPPackageBuilder.Build(request, folder),
                BuildPSPfPKGButton, BuildPSPfPKGButtonText, PSPBuildProgress);
        };
    }

    private void UpdatePSPPreview() =>
        UpdatePreview(PSPPreview, SelectedPSPIconTextBox.Text, SelectedPSPBGImageTextBox.Text, null, null,
            PSPTitleTextBox.Text, PSPPS5Radio.IsChecked == true, PSPIconThumbnail, PSPBackgroundThumbnail, PSPHintsText);

    private async Task UseDiscImages()
    {
        var isoFile = SelectedPSPDiscTextBox.Text;
        if (string.IsNullOrWhiteSpace(isoFile) || !File.Exists(isoFile))
        {
            await MessageDialog.ShowInfo(this, Localizer.T("Images from the disc"), Localizer.T("Select the ISO file of the game first."));
            return;
        }
        var name = !string.IsNullOrEmpty(PSPNPTitleTextBox.Text) ? PSPNPTitleTextBox.Text : Path.GetFileNameWithoutExtension(isoFile);
        var images = await Task.Run(() => CoverArt.CreateImagesFromPspIso(isoFile, name));
        if (images == null)
        {
            await MessageDialog.ShowInfo(this, Localizer.T("Images from the disc"), Localizer.T("This disc has no icon. Choose an image yourself."));
            return;
        }
        await ApplyImages(images, SelectedPSPIconTextBox, SelectedPSPBGImageTextBox);
    }

    private async Task LoadPSPDisc(string file)
    {
        // A compressed .cso is turned into a normal .iso first (kept in the cache folder)
        if (Path.GetExtension(file).Equals(".cso", StringComparison.OrdinalIgnoreCase))
        {
            var csoFile = file;
            try
            {
                file = await Task.Run(() => CsoReader.ConvertToCache(csoFile));
            }
            catch (Exception ex)
            {
                await MessageDialog.ShowInfo(this, "PS Classics fPKG Builder", Localizer.F("The .cso file could not be converted: {0}", ex.Message));
                return;
            }
        }
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
                // The title stored in the disc is better than the one guessed from the file name
                var discTitle = IsoTools.GetPSPTitle(file);
                if (discTitle != "") PSPTitleTextBox.Text = discTitle;
                File.Delete(tempUMDDataFile);
            }

            SelectedPSPDiscTextBox.Text = file;
        }
        else if (await MessageDialog.Confirm(this, Localizer.T("Unknown PSP ISO file"),
                     Localizer.T("Could not find any PSP game information within the ISO file.\nDo you want to use this file anyway ?")))
        {
            SelectedPSPDiscTextBox.Text = file;
        }
    }

    private PSPBuildRequest CreatePSPRequest() => new()
    {
        Title = PSPTitleTextBox.Text ?? "",
        NPTitle = PSPNPTitleTextBox.Text ?? "",
        IsoFile = SelectedPSPDiscTextBox.Text ?? "",
        IconFile = SelectedPSPIconTextBox.Text ?? "",
        BackgroundFile = SelectedPSPBGImageTextBox.Text ?? "",
        ConfigFile = SelectedPSPConfigTextBox.Text ?? "",
        Warn = WarnFromBuildThread,
    };

    #endregion
}
