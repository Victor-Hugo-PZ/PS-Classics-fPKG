''' <summary>A file that is added to the GP4 project.</summary>
Public Class Gp4FileEntry
    Public Property TargetPath As String
    Public Property OriginalPath As String
    Public Property Compression As Boolean

    Public Sub New(TargetPath As String, OriginalPath As String, Compression As Boolean)
        Me.TargetPath = TargetPath
        Me.OriginalPath = OriginalPath
        Me.Compression = Compression
    End Sub
End Class

''' <summary>
''' Text based edits of the GP4 project written by "gengp4_patch.exe".
''' The GP4 file is plain XML but the PS4 publishing tools need three small adjustments after it has been generated.
''' </summary>
Public Class Gp4Editor

    ''' <summary>Downgrades the XML version (1.1 to 1.0) and selects scenario 0 as default.</summary>
    Public Shared Function NormalizeHeader(Gp4Content As String) As String
        Return Gp4Content.Replace("<?xml version=""1.1""", "<?xml version=""1.0""").Replace("<scenarios default_id=""1"">", "<scenarios default_id=""0"">")
    End Function

    Public Shared Function FileEntryXml(Entry As Gp4FileEntry) As String
        Dim CompressionValue As String = If(Entry.Compression, "enable", "disable")
        Return vbCrLf + "    <file targ_path=""" + Entry.TargetPath + """ orig_path=""" + Entry.OriginalPath + """ pfs_compression=""" + CompressionValue + """/>"
    End Function

    ''' <summary>Adds file entries right before the closing files tag.</summary>
    Public Shared Function AddFiles(Gp4Content As String, Entries As IEnumerable(Of Gp4FileEntry)) As String
        Dim EntriesXml As String = ""
        For Each Entry As Gp4FileEntry In Entries
            EntriesXml += FileEntryXml(Entry)
        Next
        Return Gp4Content.Replace("</files>", EntriesXml + vbCrLf + "</files>")
    End Function

    ''' <summary>Edits a GP4 file in place: normalizes the header and optionally adds file entries.</summary>
    Public Shared Sub PatchFile(Gp4File As String, Optional Entries As IEnumerable(Of Gp4FileEntry) = Nothing)
        Dim Content As String = NormalizeHeader(System.IO.File.ReadAllText(Gp4File))
        If Entries IsNot Nothing Then
            Content = AddFiles(Content, Entries)
        End If
        System.IO.File.WriteAllText(Gp4File, Content)
    End Sub

End Class
