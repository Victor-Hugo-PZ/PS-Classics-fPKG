Imports LibOrbisPkg.SFO

''' <summary>Creates the "param.sfo" of the generated PS4 fake package and the CONTENT_ID values of each platform.</summary>
Public Class SfoBuilder

    ''' <summary>Content ID of a PS1 fPKG, for example "UP9000-SLUS00594_00-SLUS00594PS1FPKG".</summary>
    Public Shared Function PS1ContentID(NPTitle As String) As String
        Return "UP9000-" + NPTitle + "_00-" + NPTitle + "PS1FPKG"
    End Function

    ''' <summary>Content ID of a PS2 fPKG, for example "UP9000-SLUS21258_00-SLUS212580000001". <paramref name="GameID"/> is the detected ID ("SLUS_212.58").</summary>
    Public Shared Function PS2ContentID(NPTitle As String, GameID As String) As String
        Return String.Concat(New String() {"UP9000-", NPTitle, "_00-", GameID.Replace(".", "").Replace("_", "").Trim(), "0000001"})
    End Function

    ''' <summary>Content ID of a PSP fPKG, for example "UP9000-ULUS10041_00-ULUS10041PSPFPKG".</summary>
    Public Shared Function PSPContentID(NPTitle As String) As String
        Return String.Concat(New String() {"UP9000-", NPTitle, "_00-", NPTitle, "PSPFPKG"})
    End Function

    ''' <summary>Builds a "gd" (game data) param.sfo. PS2 packages additionally carry REMOTE_PLAY_KEY_ASSIGN.</summary>
    Public Shared Function Build(ContentID As String, Title As String, TitleID As String, Optional IncludeRemotePlayKeyAssign As Boolean = False) As Byte()
        Dim NewPS4ParamSFO As New ParamSfo()
        NewPS4ParamSFO.SetValue("APP_TYPE", SfoEntryType.Integer, "1", 4)
        NewPS4ParamSFO.SetValue("APP_VER", SfoEntryType.Utf8, "01.00", 8)
        NewPS4ParamSFO.SetValue("ATTRIBUTE", SfoEntryType.Integer, "0", 4)
        NewPS4ParamSFO.SetValue("CATEGORY", SfoEntryType.Utf8, "gd", 4)
        NewPS4ParamSFO.SetValue("CONTENT_ID", SfoEntryType.Utf8, ContentID, 48)
        NewPS4ParamSFO.SetValue("DOWNLOAD_DATA_SIZE", SfoEntryType.Integer, "0", 4)
        NewPS4ParamSFO.SetValue("FORMAT", SfoEntryType.Utf8, "obs", 4)
        NewPS4ParamSFO.SetValue("PARENTAL_LEVEL", SfoEntryType.Integer, "5", 4)
        If IncludeRemotePlayKeyAssign Then
            NewPS4ParamSFO.SetValue("REMOTE_PLAY_KEY_ASSIGN", SfoEntryType.Integer, "0", 4)
        End If
        NewPS4ParamSFO.SetValue("SYSTEM_VER", SfoEntryType.Integer, "0", 4)
        NewPS4ParamSFO.SetValue("TITLE", SfoEntryType.Utf8, Title, 128)
        NewPS4ParamSFO.SetValue("TITLE_ID", SfoEntryType.Utf8, TitleID, 12)
        NewPS4ParamSFO.SetValue("VERSION", SfoEntryType.Utf8, "01.00", 8)

        Return NewPS4ParamSFO.Serialize()
    End Function

End Class
