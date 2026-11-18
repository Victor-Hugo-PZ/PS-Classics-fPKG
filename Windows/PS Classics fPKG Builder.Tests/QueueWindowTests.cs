using System.Windows;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>The queue window of the WPF app (WPF controls, run on an STA thread).</summary>
public class QueueWindowTests
{
    private static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new Xunit.Sdk.XunitException("STA test failed: " + error);
    }

    [Fact]
    public void TheWindowOpensEmptyWithTheStartButtonOff()
    {
        RunSta(() =>
        {
            var window = new QueueWindow();

            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("EmptyText")).Visibility);
            Assert.False(((System.Windows.Controls.Control)window.FindName("StartButton")).IsEnabled);
            Assert.True(((System.Windows.Controls.ComboBox)window.FindName("PresetComboBox")).Items.Count >= 3);
        });
    }

    [Fact]
    public void AddedArchivesAreListedAndEnableStart()
    {
        RunSta(() =>
        {
            var window = new QueueWindow();
            window.AddArchives(new[] { @"C:\games\Tekken 3 (USA).7z", @"C:\games\notes.txt" });

            var list = (System.Windows.Controls.ListBox)window.FindName("ItemsListBox");
            Assert.Single(list.Items);
            Assert.Contains("Tekken 3 (USA).7z", (string)list.Items[0]);
            Assert.True(((System.Windows.Controls.Control)window.FindName("StartButton")).IsEnabled);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("EmptyText")).Visibility);
        });
    }
}
