using System.Windows;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>The result window of the WPF app (WPF controls, run on an STA thread).</summary>
public class BuildResultWindowTests
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
    public void AFailureWithAFixShowsTheFixButton()
    {
        RunSta(() =>
        {
            var failed = new PackageBuildResult { Success = false };
            var window = new BuildResultWindow(failed, @"C:\out", null, "The .CUE file of disc 1 is missing");

            var fix = (FrameworkElement)window.FindName("FixButton");
            Assert.Equal(Visibility.Visible, fix.Visibility);
            Assert.Equal(FixAction.None, window.RequestedAction);
        });
    }

    [Fact]
    public void AFailureWithoutAFixHidesTheFixButton()
    {
        RunSta(() =>
        {
            var failed = new PackageBuildResult { Success = false };
            var window = new BuildResultWindow(failed, @"C:\out", null, "something nobody expected");

            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("FixButton")).Visibility);
        });
    }

    [Fact]
    public void ASuccessHidesTheFixButton()
    {
        RunSta(() =>
        {
            var done = new PackageBuildResult { Success = true, PackagePath = @"C:\out\game.pkg" };
            var window = new BuildResultWindow(done, @"C:\out", null, "");

            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("FixButton")).Visibility);
        });
    }
}
