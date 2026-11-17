Imports System.Windows.Media

''' <summary>
''' Bar of the step-by-step mode. The page decides what each step shows; the bar only keeps the current step,
''' highlights it and asks the page before moving forward (<see cref="CanLeaveStep"/>).
''' </summary>
Public Class StepBar

    Public Const StepCount As Integer = 4
    Private _CurrentStep As Integer

    ''' <summary>Raised after the current step changed.</summary>
    Public Event StepChanged As EventHandler

    ''' <summary>Called with the current step before going to the next one. Return False to stay (the page shows why).</summary>
    Public Property CanLeaveStep As Func(Of Integer, Boolean)

    Public Property CurrentStep As Integer
        Get
            Return _CurrentStep
        End Get
        Set(value As Integer)
            _CurrentStep = Math.Max(0, Math.Min(StepCount - 1, value))
            Refresh()
            RaiseEvent StepChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Public Sub New()
        InitializeComponent()
        Refresh()
    End Sub

    Private Sub BackButton_Click(sender As Object, e As RoutedEventArgs) Handles BackButton.Click
        CurrentStep -= 1
    End Sub

    Private Sub NextButton_Click(sender As Object, e As RoutedEventArgs) Handles NextButton.Click
        If CanLeaveStep IsNot Nothing AndAlso Not CanLeaveStep.Invoke(_CurrentStep) Then Return
        CurrentStep += 1
    End Sub

    Private Sub Refresh()
        BackButton.IsEnabled = _CurrentStep > 0
        NextButton.Visibility = If(_CurrentStep < StepCount - 1, Visibility.Visible, Visibility.Collapsed)
        Dim Borders As Border() = {Step1Border, Step2Border, Step3Border, Step4Border}
        Dim Texts As TextBlock() = {Step1Text, Step2Text, Step3Text, Step4Text}
        For Index As Integer = 0 To StepCount - 1
            Dim IsCurrent As Boolean = Index = _CurrentStep
            Borders(Index).Background = If(IsCurrent, New SolidColorBrush(Color.FromRgb(&H0, &H70, &HD1)), New SolidColorBrush(Color.FromArgb(&H22, &HFF, &HFF, &HFF)))
            Texts(Index).Foreground = If(IsCurrent OrElse Index < _CurrentStep, Brushes.White, New SolidColorBrush(Color.FromRgb(&H9A, &H9A, &H9A)))
            Texts(Index).FontWeight = If(IsCurrent, FontWeights.SemiBold, FontWeights.Normal)
        Next
    End Sub

End Class
