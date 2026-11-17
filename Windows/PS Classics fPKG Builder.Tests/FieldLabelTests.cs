using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>Help labels of the WPF form (WPF controls, run on an STA thread).</summary>
public class FieldLabelTests
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
    public void IsRequired_ShowsRequiredOrOptionalBadge()
    {
        RunSta(() =>
        {
            var label = new FieldLabel { LabelText = "Disc 1", IsRequired = true };
            Assert.Equal("Required", ((System.Windows.Controls.TextBlock)label.FindName("BadgeText")).Text);
            Assert.True(label.IsRequired);

            label.IsRequired = false;
            Assert.Equal("Optional", ((System.Windows.Controls.TextBlock)label.FindName("BadgeText")).Text);
            Assert.False(label.IsRequired);
        });
    }

    [Fact]
    public void LabelAndHelpTextAreStored()
    {
        RunSta(() =>
        {
            var label = new FieldLabel { LabelText = "Icon", HelpText = "Some help" };
            Assert.Equal("Icon", ((System.Windows.Controls.TextBlock)label.FindName("LabelTextBlock")).Text);
            Assert.Equal("Some help", label.HelpText);
        });
    }
}
