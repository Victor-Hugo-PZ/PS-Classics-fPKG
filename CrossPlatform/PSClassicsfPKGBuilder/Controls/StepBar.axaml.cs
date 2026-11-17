using Avalonia.Controls;
using Avalonia.Media;

namespace PSClassicsfPKGBuilder.Controls;

/// <summary>
/// Bar of the step-by-step mode (same as the Windows app). The page decides what each step shows; the bar keeps the
/// current step, highlights it and asks the page before moving forward (<see cref="CanLeaveStep"/>).
/// </summary>
public partial class StepBar : UserControl
{
    public const int StepCount = 4;
    private int _currentStep;

    public event EventHandler StepChanged;

    /// <summary>Called with the current step before going to the next one. Return false to stay (the page shows why).</summary>
    public Func<int, Task<bool>> CanLeaveStep { get; set; }

    public StepBar()
    {
        InitializeComponent();
        BackButton.Click += (_, _) => CurrentStep--;
        NextButton.Click += async (_, _) =>
        {
            if (CanLeaveStep != null && !await CanLeaveStep(_currentStep)) return;
            CurrentStep++;
        };
        Refresh();
    }

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            _currentStep = Math.Clamp(value, 0, StepCount - 1);
            Refresh();
            StepChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Refresh()
    {
        BackButton.IsEnabled = _currentStep > 0;
        NextButton.IsVisible = _currentStep < StepCount - 1;
        var borders = new[] { Step1Border, Step2Border, Step3Border, Step4Border };
        var texts = new[] { Step1Text, Step2Text, Step3Text, Step4Text };
        for (var index = 0; index < StepCount; index++)
        {
            var isCurrent = index == _currentStep;
            borders[index].Background = new SolidColorBrush(isCurrent ? Color.Parse("#0070D1") : Color.Parse("#22FFFFFF"));
            texts[index].Foreground = isCurrent || index < _currentStep ? Brushes.White : new SolidColorBrush(Color.Parse("#9A9A9A"));
            texts[index].FontWeight = isCurrent ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }
}
