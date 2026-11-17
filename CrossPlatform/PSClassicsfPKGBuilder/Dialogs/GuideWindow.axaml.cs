using Avalonia.Controls;
using Avalonia.Threading;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>Step-by-step guide for new users (same content as the Windows app).</summary>
public partial class GuideWindow : Window
{
    public GuideWindow()
    {
        InitializeComponent();
        UiTranslator.Attach(this);
    }

    /// <summary>Opens the guide at the section about installing the package on the console.</summary>
    public void ShowInstallSection() =>
        Opened += (_, _) => Dispatcher.UIThread.Post(() => InstallSection.BringIntoView(), DispatcherPriority.Loaded);
}
