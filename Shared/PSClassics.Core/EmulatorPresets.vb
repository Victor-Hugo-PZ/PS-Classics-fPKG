''' <summary>
''' Ready-made emulator settings for beginners. Choosing one fills the options of the Emulator section; every option
''' can still be changed afterwards. Names and descriptions are English keys of <see cref="Localizer"/>.
''' The values are the texts of the combo boxes of both apps.
''' </summary>
Public Class PS1Preset
    Public Property Name As String = ""
    Public Property Description As String = ""
    Public Property Upscaling As String = "2"
    Public Property SkipBootlogo As Boolean
    Public Property EmulateAnalogSticks As Boolean
    Public Property Force60Hz As Boolean
End Class

Public Class PS2Preset
    Public Property Name As String = ""
    Public Property Description As String = ""
    Public Property Emulator As String = "Jak v2"
    Public Property Upscaling As String = "None"
    Public Property Uprender As String = "2x2"
    Public Property DisplayMode As String = "Normal"
    Public Property FixGraphics As Boolean = True
    Public Property ImproveSpeed As Boolean
    Public Property DisableMTVU As Boolean
    Public Property DisableInstantVIF1Transfer As Boolean
    ''' <summary>Turn the widescreen patch on when the database has one for the game.</summary>
    Public Property WidescreenPatchWhenAvailable As Boolean
    Public Property UseCompression As Boolean = True
End Class

Public Class EmulatorPresets

    ''' <summary>Every preset name of the platforms that have presets, without repeating the shared ones (for the queue's per-game list).</summary>
    Public Shared Function AllNames() As List(Of String)
        Dim Names As New List(Of String)
        For Each Name As String In PS1.Select(Function(Preset) Preset.Name).Concat(PS2.Select(Function(Preset) Preset.Name))
            If Not Names.Contains(Name) Then Names.Add(Name)
        Next
        Return Names
    End Function

    Public Shared ReadOnly Property PS1 As IReadOnlyList(Of PS1Preset) = New List(Of PS1Preset) From {
        New PS1Preset With {.Name = "Recommended", .Description = "Default settings that work for most games.", .Upscaling = "2"},
        New PS1Preset With {.Name = "Most compatible", .Description = "Original resolution. Try it when a game runs slowly or shows glitches.", .Upscaling = "1"},
        New PS1Preset With {.Name = "Best looking", .Description = "Sharper picture (4x). Use it if the game still runs at full speed.", .Upscaling = "4", .SkipBootlogo = True}
    }

    Public Shared ReadOnly Property PS2 As IReadOnlyList(Of PS2Preset) = New List(Of PS2Preset) From {
        New PS2Preset With {.Name = "Recommended", .Description = "Default settings that work for most games."},
        New PS2Preset With {.Name = "Most compatible", .Description = "Original resolution and safer emulation. Try it when a game freezes, crashes or shows glitches.",
                            .Uprender = "None", .DisableMTVU = True, .DisableInstantVIF1Transfer = True},
        New PS2Preset With {.Name = "Best looking", .Description = "Sharper picture and the widescreen patch when the database has one.",
                            .Upscaling = "Edgesmooth", .WidescreenPatchWhenAvailable = True},
        New PS2Preset With {.Name = "Other emulator (Rogue v1)", .Description = "The second emulator. Try it when the game does not start or crashes with the recommended settings.",
                            .Emulator = "Rogue v1", .Uprender = "None"}
    }

End Class
