Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class PSClassicsfPKGBuilder

#Region "Shared helpers"

    ''' <summary>Tells the user about the result of a package build.</summary>
    Private Shared Sub ReportResult(BuildResult As PackageBuildResult, OutputFolder As String)
        If BuildResult.Success Then
            If MsgBox("PKG created!" + vbCrLf + "Do you want to open the output folder ?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                Process.Start(New ProcessStartInfo("explorer", """" + OutputFolder + """") With {.UseShellExecute = True})
            End If
        Else
            MsgBox("PKG creation failed!" + vbCrLf + BuildResult.Output, MsgBoxStyle.Critical, "Cannot create fPKG")
        End If
    End Sub

    Private Shared Sub ShowBuildError(ex As Exception)
        MsgBox("The fPKG could not be created:" + vbCrLf + ex.Message, MsgBoxStyle.Critical, "Cannot create fPKG")
    End Sub

    Private Shared Sub ShowWarning(Message As String)
        MsgBox(Message, MsgBoxStyle.Exclamation)
    End Sub

#End Region

#Region "PS1"

    Dim CurrentPS1GameID As String = ""

    Private Sub BrowsePS1Disc1Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc1Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS1 BIN file.", .Multiselect = False, .Filter = "BIN (*.bin)|*.bin"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then

            'Get the game ID from the BIN file using strings
            Dim GameID As String = PS1Game.GetPS1GameID(OFD.FileName)
            Dim GameTitle As String = ""

            'Try to get a game title from the master list
            If GameID.Length = 10 Then
                GameTitle = PS1Game.GetPS1GameTitleFromDatabaseList(GameID)
            End If

            If Not String.IsNullOrEmpty(GameID) Then
                CurrentPS1GameID = GameID.Trim()
                PS1NPTitleTextBox.Text = GameID.Replace("-"c, "").Trim()
                PS1SelectedDisc1TextBox.Text = OFD.FileName
            Else
                If MsgBox("Cannot find the game ID within the PS1 BIN file, do you want to use this file anyway ?" + vbCrLf + "Do not forget to specify a NP title and title if you do so.", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                    PS1SelectedDisc1TextBox.Text = OFD.FileName
                End If
            End If
            If Not String.IsNullOrEmpty(GameTitle) Then
                PS1TitleTextBox.Text = GameTitle
            End If
        Else
            MsgBox("Aborted.", MsgBoxStyle.Information)
        End If
    End Sub

    Private Sub BrowsePS1IconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1IconButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG icon file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS1IconTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1StartupImageButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1StartupImageButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG Startup Background file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS1BGTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1Disc2Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc2Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS1 BIN file.", .Multiselect = False, .Filter = "BIN (*.bin)|*.bin"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            PS1SelectedDisc2TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1Disc3Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc3Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS1 BIN file.", .Multiselect = False, .Filter = "BIN (*.bin)|*.bin"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            PS1SelectedDisc3TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1Disc4Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc4Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS1 BIN file.", .Multiselect = False, .Filter = "BIN (*.bin)|*.bin"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            PS1SelectedDisc4TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1TXTConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1TXTConfigButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a TXT config file.", .Multiselect = False, .Filter = "TXT (*.txt)|*.txt"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS1TXTConfigTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS1LUAConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1LUAConfigButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a LUA config file.", .Multiselect = False, .Filter = "LUA (*.lua)|*.lua"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS1LUAConfigTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BuildPS1fPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPS1fPKGButton.Click

        'Checks before fPKG creation
        If String.IsNullOrEmpty(PS1SelectedDisc1TextBox.Text) Then
            MsgBox("No disc 1 specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PS1TitleTextBox.Text) Then
            MsgBox("No game title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PS1NPTitleTextBox.Text) Then
            MsgBox("No NP title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If PS1NPTitleTextBox.Text.Length <> 9 Then
            MsgBox("'NP Title' length mismatching, only 9 characters are allowed, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If

        Dim FBD As New FolderBrowserDialog() With {.Description = "Please select an output folder", .ShowNewFolderButton = True}
        If FBD.ShowDialog() = Forms.DialogResult.OK Then
            Try
                BuildPS1fPKG(FBD.SelectedPath)
            Catch ex As Exception
                ShowBuildError(ex)
            End Try
        Else
            MsgBox("Aborted", MsgBoxStyle.Information)
        End If

    End Sub

    Private Sub BuildPS1fPKG(PKGOutputFolder As String)
        Dim Request As New PS1BuildRequest() With {
            .Title = PS1TitleTextBox.Text,
            .NPTitle = PS1NPTitleTextBox.Text,
            .DetectedGameID = CurrentPS1GameID,
            .Disc1 = PS1SelectedDisc1TextBox.Text,
            .Disc2 = PS1SelectedDisc2TextBox.Text,
            .Disc3 = PS1SelectedDisc3TextBox.Text,
            .Disc4 = PS1SelectedDisc4TextBox.Text,
            .IconFile = SelectedPS1IconTextBox.Text,
            .BackgroundFile = SelectedPS1BGTextBox.Text,
            .TxtConfigFile = SelectedPS1TXTConfigTextBox.Text,
            .LuaConfigFile = SelectedPS1LUAConfigTextBox.Text,
            .Upscaling = PS1UpscalingComboBox.Text,
            .SkipBootlogo = (PS1SkipBootlogoCheckBox.IsChecked = True),
            .Guncon = (PS1GunconCheckBox.IsChecked = True),
            .Force60Hz = (PS1Force60HzCheckBox.IsChecked = True),
            .EmulateAnalogSticks = (PS1EmulateAnalogSticksCheckBox.IsChecked = True),
            .ConfirmLibcrypt = Function() MsgBox("This game is Libcrypt protected. Do you want to add --libcrypt to the configuration file ?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes
        }

        ReportResult(PS1PackageBuilder.Build(Request, PKGOutputFolder), PKGOutputFolder)
    End Sub
#End Region

#Region "PS2"

    Dim CurrentPS2GameID As String = ""
    Dim CurrentPS2GameCRC As String = ""

    Private Sub BrowsePS2Disc1Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc1Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then

            Dim PS2GameID As String = PS2Game.GetPS2GameID(OFD.FileName)
            Dim ExtractedPS2GameELFPath As String = IsoTools.GetELFfromISO(OFD.FileName, PS2GameID)
            CurrentPS2GameID = PS2GameID

            Dim PS2GameCRC As String = IsoTools.GetGameCRC(ExtractedPS2GameELFPath)
            CurrentPS2GameCRC = PS2GameCRC

            If Not String.IsNullOrEmpty(PS2GameID) AndAlso Not String.IsNullOrEmpty(PS2GameCRC) Then

                Dim ConfigsDirectory As String = AppPaths.ToolsDirectory + "\PS4\ps2-configs"

                If IsoTools.IsConfigAvailable(PS2GameID, ConfigsDirectory + "\configs_txt.dat") Then
                    PS2AddTXTConfigFromDatabaseCheckBox.IsChecked = True
                    PS2AddTXTConfigFromDatabaseCheckBox.Visibility = Visibility.Visible
                Else
                    PS2AddTXTConfigFromDatabaseCheckBox.IsChecked = False
                    PS2AddTXTConfigFromDatabaseCheckBox.Visibility = Visibility.Hidden
                End If

                If IsoTools.IsConfigAvailable(PS2GameID, ConfigsDirectory + "\configs_lua.dat") Then
                    PS2AddLUAConfigFromDatabaseCheckBox.IsChecked = True
                    PS2AddLUAConfigFromDatabaseCheckBox.Visibility = Visibility.Visible
                Else
                    PS2AddLUAConfigFromDatabaseCheckBox.IsChecked = False
                    PS2AddLUAConfigFromDatabaseCheckBox.Visibility = Visibility.Hidden
                End If

                If IsoTools.IsConfigAvailable(PS2GameID + ".CONFIG", ConfigsDirectory + "\configs_ps3.dat") Then
                    PS2AddPS3ConfigFromDatabaseCheckBox.IsChecked = True
                    PS2AddPS3ConfigFromDatabaseCheckBox.Visibility = Visibility.Visible
                Else
                    PS2AddPS3ConfigFromDatabaseCheckBox.IsChecked = False
                    PS2AddPS3ConfigFromDatabaseCheckBox.Visibility = Visibility.Hidden
                End If

                If IsoTools.IsConfigAvailable(PS2GameCRC + ".lua", ConfigsDirectory + "\widescreen.dat") Then
                    PS2UseWidescreenPatchCheckBox.IsChecked = True
                    PS2UseWidescreenPatchCheckBox.IsEnabled = True
                Else
                    PS2UseWidescreenPatchCheckBox.IsEnabled = False
                End If

                PS2TitleTextBox.Text = PS2Game.GetPS2GameTitleFromDatabaseList(PS2GameID.Replace(".", "").Replace("_", "-").Trim())
                PS2NPTitleTextBox.Text = PS2GameID.Replace(".", "").Replace("_", "").Trim()
                SelectedDisc1TextBox.Text = OFD.FileName
            Else
                If MsgBox("Could not find the PS2 game ID within the ISO file." + vbCrLf + "Do you want to use this file anyway ?", MsgBoxStyle.YesNo, "PS2 Game ID not found") = MsgBoxResult.Yes Then
                    SelectedDisc1TextBox.Text = OFD.FileName
                End If
            End If

        End If
    End Sub

    Private Sub BrowsePS2IconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2IconButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG icon file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedIconTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2StartupImageButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2StartupImageButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG background file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedStartupImageTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2MCButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2MCButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 Memory Card file.", .Multiselect = False, .Filter = "VM2 Card (*.vm2)|*.vm2|PS2 Card (*.ps2)|*.ps2|BIN Card (*.bin)|*.bin"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS2MemoryCardTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2TXTConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2TXTConfigButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a TXT config file.", .Multiselect = False, .Filter = "TXT (*.txt)|*.txt"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS2TXTConfigTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2LUAConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2LUAConfigButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a LUA config file.", .Multiselect = False, .Filter = "LUA (*.lua)|*.lua"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPS2LUAConfigTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2Disc2Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc2Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedDisc2TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2Disc3Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc3Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedDisc3TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2Disc4Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc4Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedDisc4TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePS2Disc5Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc5Button.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PS2 ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedDisc5TextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BuildPS2fPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPS2fPKGButton.Click

        'Checks before fPKG creation
        If String.IsNullOrEmpty(SelectedDisc1TextBox.Text) Then
            MsgBox("No disc 1 specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PS2TitleTextBox.Text) Then
            MsgBox("No game title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PS2NPTitleTextBox.Text) Then
            MsgBox("No NP title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If PS2NPTitleTextBox.Text.Length <> 9 Then
            MsgBox("'NP Title' length mismatching, only 9 characters are allowed, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If

        'Select an output folder
        Dim FBD As New FolderBrowserDialog() With {.Description = "Please select an output folder", .ShowNewFolderButton = True}
        If FBD.ShowDialog() = Forms.DialogResult.OK Then
            Try
                BuildPS2fPKG(FBD.SelectedPath)
            Catch ex As Exception
                ShowBuildError(ex)
            End Try
        Else
            MsgBox("Aborted", MsgBoxStyle.Information)
        End If
    End Sub

    Private Sub BuildPS2fPKG(PKGOutputFolder As String)
        Dim Request As New PS2BuildRequest() With {
            .Title = PS2TitleTextBox.Text,
            .NPTitle = PS2NPTitleTextBox.Text,
            .DetectedGameID = CurrentPS2GameID,
            .GameCRC = CurrentPS2GameCRC,
            .Disc1 = SelectedDisc1TextBox.Text,
            .Disc2 = SelectedDisc2TextBox.Text,
            .Disc3 = SelectedDisc3TextBox.Text,
            .Disc4 = SelectedDisc4TextBox.Text,
            .Disc5 = SelectedDisc5TextBox.Text,
            .Emulator = PS2EmulatorComboBox.Text,
            .IconFile = SelectedIconTextBox.Text,
            .BackgroundFile = SelectedStartupImageTextBox.Text,
            .MemoryCardFile = SelectedPS2MemoryCardTextBox.Text,
            .TxtConfigFile = SelectedPS2TXTConfigTextBox.Text,
            .LuaConfigFile = SelectedPS2LUAConfigTextBox.Text,
            .AddTxtConfigFromDatabase = (PS2AddTXTConfigFromDatabaseCheckBox.IsChecked = True),
            .AddLuaConfigFromDatabase = (PS2AddLUAConfigFromDatabaseCheckBox.IsChecked = True),
            .AddPS3ConfigFromDatabase = (PS2AddPS3ConfigFromDatabaseCheckBox.IsChecked = True),
            .UseWidescreenPatch = (PS2UseWidescreenPatchCheckBox.IsChecked = True),
            .Uprender = PS2UprenderComboBox.Text,
            .Upscaling = PS2UpscalingComboBox.Text,
            .DisplayMode = PS2DisplayModeComboBox.Text,
            .RestartEmulatorOnDiscChange = (PS2RestartEmulatorOnDiscChangeCheckBox.IsChecked = True),
            .MultitapIndex = PS2MultitapComboBox.SelectedIndex,
            .ImproveSpeed = (PS2ImproveSpeedCheckBox.IsChecked = True),
            .FixGraphics = (PS2FixGraphicsCheckBox.IsChecked = True),
            .DisableMTVU = (PS2DisableMTVUCheckBox.IsChecked = True),
            .DisableInstantVIF1Transfer = (PS2DisableInstantVIF1TransferCheckBox.IsChecked = True),
            .UseCompression = (PS2UseCompressionCheckBox.IsChecked = True)
        }

        ReportResult(PS2PackageBuilder.Build(Request, PKGOutputFolder), PKGOutputFolder)
    End Sub
#End Region

#Region "PSP"

    Private Sub BrowsePSPIconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPIconButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG icon file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPSPIconTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePSPBGButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPBGButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PNG background file.", .Multiselect = False, .Filter = "PNG (*.png)|*.png"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPSPBGImageTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BrowsePSPDiscButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPDiscButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a PSP ISO file.", .Multiselect = False, .Filter = "ISO (*.iso)|*.iso"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then

            If IsoTools.FileExistInISO(OFD.FileName, "\PSP_GAME\PARAM.SFO") Then
                Dim TempUMDDataFile As String = AppPaths.CacheDirectory + "\temp_umd_data.bin"
                Dim ExtractedUMDDataPath As String = IsoTools.ExtractFileFromPSPISO(OFD.FileName, "UMD_DATA.BIN", TempUMDDataFile)

                If Not String.IsNullOrEmpty(ExtractedUMDDataPath) Then
                    PSPNPTitleTextBox.Text = Text.Encoding.ASCII.GetString(PspTools.ReadBytes(TempUMDDataFile, 0, 10)).Replace("-", "")
                    PSPTitleTextBox.Text = Regex.Replace(Path.GetFileNameWithoutExtension(OFD.FileName), "\((.*?)\)", "")
                    PSPTitleTextBox.Text = Regex.Replace(PSPTitleTextBox.Text, " {2,}", "")
                    File.Delete(TempUMDDataFile)
                End If

                SelectedPSPDiscTextBox.Text = OFD.FileName
            Else
                If MsgBox("Could not find any PSP game information within the ISO file." + vbCrLf + "Do you want to use this file anyway ?", MsgBoxStyle.YesNo, "Unknown PSP ISO file") = MsgBoxResult.Yes Then
                    SelectedPSPDiscTextBox.Text = OFD.FileName
                End If
            End If

        End If
    End Sub

    Private Sub BrowsePSPConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPConfigButton.Click
        Dim OFD As New OpenFileDialog() With {.Title = "Select a config-title.txt file.", .Multiselect = False, .Filter = "TXT (*.txt)|*.txt"}
        If OFD.ShowDialog() = Forms.DialogResult.OK Then
            SelectedPSPConfigTextBox.Text = OFD.FileName
        End If
    End Sub

    Private Sub BuildPSPfPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPSPfPKGButton.Click

        'Checks before fPKG creation
        If String.IsNullOrEmpty(SelectedPSPDiscTextBox.Text) Then
            MsgBox("No disc 1 specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PSPTitleTextBox.Text) Then
            MsgBox("No game title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If String.IsNullOrEmpty(PSPNPTitleTextBox.Text) Then
            MsgBox("No NP title specified, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If
        If PSPNPTitleTextBox.Text.Length <> 9 Then
            MsgBox("'NP Title' length mismatching, only 9 characters are allowed, fPKG creation will be aborted.", MsgBoxStyle.Critical, "Cannot create fPKG")
            Exit Sub
        End If

        Dim FBD As New FolderBrowserDialog() With {.Description = "Please select an output folder", .ShowNewFolderButton = True}
        If FBD.ShowDialog() = Forms.DialogResult.OK Then
            Try
                BuildPSPfPKG(FBD.SelectedPath)
            Catch ex As Exception
                ShowBuildError(ex)
            End Try
        End If

    End Sub

    Private Sub BuildPSPfPKG(PKGOutputFolder As String)
        Dim Request As New PSPBuildRequest() With {
            .Title = PSPTitleTextBox.Text,
            .NPTitle = PSPNPTitleTextBox.Text,
            .IsoFile = SelectedPSPDiscTextBox.Text,
            .IconFile = SelectedPSPIconTextBox.Text,
            .BackgroundFile = SelectedPSPBGImageTextBox.Text,
            .ConfigFile = SelectedPSPConfigTextBox.Text,
            .Warn = AddressOf ShowWarning
        }

        ReportResult(PSPPackageBuilder.Build(Request, PKGOutputFolder), PKGOutputFolder)
    End Sub
#End Region

End Class
