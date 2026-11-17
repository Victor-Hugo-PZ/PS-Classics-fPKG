using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PSClassicsfPKGBuilder;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Language of the user interface: saved choice or the system language
        PS_Classics_fPKG_Builder.Localizer.Language = PS_Classics_fPKG_Builder.AppSettings.Current.EffectiveLanguage();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
