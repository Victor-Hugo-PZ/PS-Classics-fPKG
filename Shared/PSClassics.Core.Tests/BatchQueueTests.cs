using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class BatchQueueTests
{
    [Fact]
    public void Add_KeepsOnlyNewZipAnd7zFiles()
    {
        var queue = new BatchQueue();

        string P(string name) => Path.Combine("games", name); // paths that work on every OS
        var added = queue.Add(new[] { P("a.zip"), P("b.7z"), P("c.iso"), P("A.ZIP"), P("notes.txt") });

        Assert.Equal(2, added);
        Assert.Equal(new[] { "a.zip", "b.7z" }, queue.Items.Select(i => i.DisplayName));
        Assert.Equal(0, queue.Add(new[] { P("a.zip") }));
    }

    [Fact]
    public void ClearDone_RemovesOnlyTheFinishedGames()
    {
        var queue = new BatchQueue();
        queue.Add(new[] { "a.zip", "b.zip", "c.zip" });
        queue.Items[0].Status = BatchStatus.Done;
        queue.Items[1].Status = BatchStatus.Failed;

        Assert.Equal(1, queue.ClearDone());
        Assert.Equal(new[] { "b.zip", "c.zip" }, queue.Items.Select(i => i.DisplayName));
    }

    [Fact]
    public void ToBuild_IsWaitingAndFailedGames_AndResetFailedMakesThemWaitAgain()
    {
        var queue = new BatchQueue();
        queue.Add(new[] { "a.zip", "b.zip", "c.zip", "d.zip" });
        queue.Items[0].Status = BatchStatus.Done;
        queue.Items[1].Status = BatchStatus.Failed;
        queue.Items[1].Message = "boom";
        queue.Items[2].Status = BatchStatus.Working;

        Assert.Equal(new[] { "b.zip", "d.zip" }, queue.ToBuild().Select(i => i.DisplayName));
        queue.ResetFailed();
        Assert.Equal(BatchStatus.Waiting, queue.Items[1].Status);
        Assert.Equal("", queue.Items[1].Message);
    }

    [Fact]
    public void Summary_CountsDoneGames()
    {
        var queue = new BatchQueue();
        queue.Add(new[] { "a.zip", "b.zip", "c.zip" });
        queue.Items[0].Status = BatchStatus.Done;
        queue.Items[2].Status = BatchStatus.Done;

        Assert.Equal("2 of 3 packages were made.", queue.Summary());
    }

    [Fact]
    public void SetPreset_ChangesOnlyGamesThatCanStillBeBuilt_AndEmptyGoesBackToTheQueuePreset()
    {
        var queue = new BatchQueue();
        queue.Add(new[] { "a.zip", "b.zip", "c.zip", "d.zip" });
        queue.Items[1].Status = BatchStatus.Done;
        queue.Items[2].Status = BatchStatus.Working;
        queue.Items[3].Status = BatchStatus.Failed;

        Assert.Equal(2, queue.SetPreset(queue.Items, "Best looking"));
        Assert.Equal(new[] { "Best looking", "", "", "Best looking" }, queue.Items.Select(i => i.PresetName));

        Assert.Equal(1, queue.SetPreset(new[] { queue.Items[0] }, ""));
        Assert.Equal("", queue.Items[0].PresetName);
    }

    [Fact]
    public void Describe_ShowsThePresetOfAGameThatHasItsOwn()
    {
        var item = new BatchItem { ArchiveFile = Path.Combine("g", "x.zip"), PresetName = "Best looking" };

        Assert.Equal("• x.zip [" + Localizer.T("Best looking") + "]", item.Describe());
    }

    [Theory]
    [InlineData(BatchStatus.Waiting, "", "x.zip", "• x.zip")]
    [InlineData(BatchStatus.Working, "Extracting…", "x.zip", "▶ x.zip - Extracting…")]
    [InlineData(BatchStatus.Done, "game.pkg", "x.zip", "✔ Tekken 3 (x.zip) - game.pkg")]
    [InlineData(BatchStatus.Failed, "No game files", "x.zip", "✘ Tekken 3 (x.zip) - No game files")]
    public void Describe_ShowsTheStateTheNameAndTheMessage(BatchStatus status, string message, string file, string expected)
    {
        var item = new BatchItem { ArchiveFile = Path.Combine("g", file), Status = status, Message = message, Title = status is BatchStatus.Done or BatchStatus.Failed ? "Tekken 3" : "" };

        Assert.Equal(expected, item.Describe());
    }
}
