Imports System.Text

''' <summary>Reads the text values of a PARAM.SFO file (the "PSF" format of PSP, PS3 and PS4 games).</summary>
Public Class SfoReader

    ''' <summary>
    ''' The text entries (UTF-8 strings) of a PARAM.SFO as key -> value. Returns an empty dictionary when the data is not a PARAM.SFO.
    ''' Layout: "\0PSF", version, key table offset, data table offset, entry count, then 16 byte entries
    ''' (key offset u16, format u16, length u32, max length u32, data offset u32).
    ''' </summary>
    Public Shared Function ReadStrings(Data As Byte()) As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(StringComparer.Ordinal)
        If Data Is Nothing OrElse Data.Length < 20 OrElse Data(0) <> 0 OrElse Data(1) <> AscW("P"c) OrElse Data(2) <> AscW("S"c) OrElse Data(3) <> AscW("F"c) Then Return Result

        Dim KeyTable As Integer = CInt(BitConverter.ToUInt32(Data, 8))
        Dim DataTable As Integer = CInt(BitConverter.ToUInt32(Data, 12))
        Dim Count As Integer = CInt(BitConverter.ToUInt32(Data, 16))
        If Count < 0 OrElse Count > 1000 Then Return Result

        For Index As Integer = 0 To Count - 1
            Dim Entry As Integer = 20 + Index * 16
            If Entry + 16 > Data.Length Then Exit For
            Dim KeyOffset As Integer = BitConverter.ToUInt16(Data, Entry)
            Dim Format As Integer = BitConverter.ToUInt16(Data, Entry + 2)
            Dim Length As Integer = CInt(BitConverter.ToUInt32(Data, Entry + 4))
            Dim ValueOffset As Integer = CInt(BitConverter.ToUInt32(Data, Entry + 12))
            'Only text formats: 0x0004 (UTF-8, not terminated) and 0x0204 (UTF-8 with terminator)
            If Format <> &H4 AndAlso Format <> &H204 Then Continue For
            Dim KeyStart As Integer = KeyTable + KeyOffset
            Dim ValueStart As Integer = DataTable + ValueOffset
            If KeyStart >= Data.Length OrElse ValueStart < 0 OrElse ValueStart + Length > Data.Length Then Continue For

            Dim KeyEnd As Integer = Array.IndexOf(Data, CByte(0), KeyStart)
            If KeyEnd < 0 Then Continue For
            Dim Key As String = Encoding.ASCII.GetString(Data, KeyStart, KeyEnd - KeyStart)
            Dim Value As String = Encoding.UTF8.GetString(Data, ValueStart, Length)
            Result(Key) = Value.TrimEnd(ChrW(0))
        Next
        Return Result
    End Function

    ''' <summary>The TITLE of a PARAM.SFO, or an empty string.</summary>
    Public Shared Function ReadTitle(Data As Byte()) As String
        Dim Values As Dictionary(Of String, String) = ReadStrings(Data)
        Dim Title As String = Nothing
        Return If(Values.TryGetValue("TITLE", Title), Title.Trim(), "")
    End Function

End Class
