''' <summary>Options of the PS1 emulator configuration ("config-title.txt").</summary>
Public Class PS1ConfigOptions
    ''' <summary>Numbers (1 to 4) of the discs that were selected.</summary>
    Public Property DiscNumbers As IEnumerable(Of Integer) = New Integer() {1}
    ''' <summary>Libcrypt value to write, or an empty string when no libcrypt patch is wanted.</summary>
    Public Property LibcryptValue As String = ""
    ''' <summary>The 9 character NP title. Only used when a LUA config is selected.</summary>
    Public Property NPTitle As String = ""
    Public Property UseLuaConfig As Boolean
    Public Property Upscaling As String = "2"
    Public Property SkipBootlogo As Boolean
    Public Property Guncon As Boolean
    Public Property Force60Hz As Boolean
    Public Property EmulateAnalogSticks As Boolean
    ''' <summary>Content of the user TXT config, or Nothing.</summary>
    Public Property UserConfigText As String = Nothing
End Class

''' <summary>Options of the PS2 emulator configuration ("config-emu-ps4.txt").</summary>
Public Class PS2ConfigOptions
    ''' <summary>Detected game ID without separators, for example "SLUS-21258".</summary>
    Public Property GameID As String = ""
    ''' <summary>Detected game ID as printed in the ISO ("SLUS_212.58"), used for the comment of the database config.</summary>
    Public Property RawGameID As String = ""
    Public Property DiscCount As Integer = 1
    Public Property Uprender As String = ""
    Public Property Upscaling As String = ""
    Public Property DisplayMode As String = ""
    Public Property RestartEmulatorOnDiscChange As Boolean = True
    ''' <summary>0 = none, 1 = port 1, 2 = port 2, 3 = both ports.</summary>
    Public Property MultitapIndex As Integer
    Public Property UsePS3Config As Boolean
    Public Property UseWidescreenPatch As Boolean
    ''' <summary>True when a LUA config was added (the emulator then needs the patches path).</summary>
    Public Property UsePatchesPath As Boolean
    Public Property ImproveSpeed As Boolean
    Public Property FixGraphics As Boolean
    Public Property DisableMTVU As Boolean
    Public Property DisableInstantVIF1Transfer As Boolean
    ''' <summary>Content of the user TXT config, or Nothing.</summary>
    Public Property UserConfigText As String = Nothing
    ''' <summary>Content of the TXT config found in the config database, or Nothing.</summary>
    Public Property DatabaseConfigText As String = Nothing
End Class

''' <summary>Builds the text of the emulator configuration files. Pure functions without file system access.</summary>
Public Class EmulatorConfigs

    Public Shared Function BuildPS1(Options As PS1ConfigOptions) As String
        Dim Config As New System.Text.StringBuilder()
        Config.Append("--ps4-trophies=0").Append(vbCrLf)
        Config.Append("--ps5-uds=0").Append(vbCrLf)
        Config.Append("--trophies=0").Append(vbCrLf)

        For Each DiscNumber As Integer In Options.DiscNumbers
            Config.Append("--image=""data/disc" + DiscNumber.ToString() + ".bin""").Append(vbCrLf)
        Next

        If Not String.IsNullOrEmpty(Options.LibcryptValue) Then
            Config.Append("--libcrypt=" + Options.LibcryptValue).Append(vbCrLf)
        End If

        If Options.UseLuaConfig Then
            Config.Append("--ps1-title-id=" + Options.NPTitle).Append(vbCrLf)
        End If

        Config.Append("--scale=" + Options.Upscaling).Append(vbCrLf)
        If Options.SkipBootlogo Then Config.Append("--bios-hide-sce-osd=1").Append(vbCrLf)
        If Options.Guncon Then Config.Append("--guncon").Append(vbCrLf)
        If Options.Force60Hz Then Config.Append("--gpu-scanout-fps-override=60").Append(vbCrLf)
        If Options.EmulateAnalogSticks Then Config.Append("--sim-analog-pad=0x2020").Append(vbCrLf)

        If Options.UserConfigText IsNot Nothing Then
            Config.Append("#User imported config").Append(vbCrLf)
            Config.Append(Options.UserConfigText).Append(vbCrLf)
        End If

        Return Config.ToString()
    End Function

    ''' <summary>The "fixes" block of the PS2 configuration, selected by the checkboxes of the UI.</summary>
    Public Shared Function BuildPS2Fixes(Options As PS2ConfigOptions) As String
        Dim Fixes As String = ""
        If Options.ImproveSpeed Then
            'Note: "-vu0-opt-flags" has a single dash in the original project. It was kept as is because it could not be verified on a console.
            Fixes += vbCrLf + "#Improve Speed" + vbCrLf + "-vu0-opt-flags=1" + vbCrLf + "--vu1-opt-flags=1" + vbCrLf + "--cop2-opt-flags=1" + vbCrLf + "--vu0-const-prop=0" + vbCrLf + "--vu1-const-prop=0" + vbCrLf + "--vu1-jr-cache-policy=newprog" + vbCrLf + "--vu1-jalr-cache-policy=newprog" + vbCrLf + "--vu0-jr-cache-policy=newprog" + vbCrLf + "--vu0-jalr-cache-policy=newprog"
        End If
        If Options.FixGraphics Then
            Fixes += vbCrLf + "#Fix Graphics" + vbCrLf + "--fpu-no-clamping=0" + vbCrLf + "--fpu-clamp-results=1" + vbCrLf + "--vu0-no-clamping=0" + vbCrLf + "--vu0-clamp-results=1" + vbCrLf + "--vu1-no-clamping=0" + vbCrLf + "--vu1-clamp-results=1" + vbCrLf + "--cop2-no-clamping=0" + vbCrLf + "--cop2-clamp-results=1"
        End If
        If Options.DisableMTVU Then
            Fixes += vbCrLf + "#Disable MTVU" + vbCrLf + "--vu1=jit-sync"
        End If
        If Options.DisableInstantVIF1Transfer Then
            Fixes += vbCrLf + "#Disable Instant VIF1 Transfer" + vbCrLf + "--vif1-instant-xfer=0"
        End If
        Return Fixes
    End Function

    Public Shared Function BuildPS2(Options As PS2ConfigOptions) As String
        Dim Content As String = "--path-vmc=""/tmp/vmc""" + vbCrLf + "--config-local-lua=""""" + vbCrLf + "--ps2-title-id=" + Options.GameID +
            vbCrLf + "--max-disc-num=" + Options.DiscCount.ToString() +
            vbCrLf + "--gs-uprender=" + LCase(Options.Uprender) +
            vbCrLf + "--gs-upscale=" + LCase(Options.Upscaling) +
            vbCrLf + "--host-audio=1" + vbCrLf +
            "--rom=""PS20220WD20050620.crack""" + vbCrLf +
            "--verbose-cdvd-reads=0" + vbCrLf +
            "--host-display-mode=" + LCase(Options.DisplayMode)

        If Not Options.RestartEmulatorOnDiscChange Then
            Content += vbCrLf + "#Disable emu reset on disc change" + vbCrLf + "--switch-disc-reset=0"
        End If

        Select Case Options.MultitapIndex
            Case 1
                Content += vbCrLf + "#Enable Multitap" + vbCrLf + "--mtap1=always"
            Case 2
                Content += vbCrLf + "#Enable Multitap" + vbCrLf + "--mtap2=always"
            Case 3
                Content += vbCrLf + "#Enable Multitap" + vbCrLf + "--mtap1=always" + vbCrLf + "--mtap2=always"
        End Select

        If Options.UsePS3Config Then Content += vbCrLf + "--lopnor-config=1"
        If Options.UseWidescreenPatch Then Content += vbCrLf + "--path-trophydata=""/app0/trophy_data"""
        If Options.UsePatchesPath Then Content += vbCrLf + "--path-patches=""/app0/patches"""

        Dim Fixes As String = BuildPS2Fixes(Options)

        If Options.UserConfigText IsNot Nothing Then
            Content = Content + vbCrLf + Fixes + vbCrLf + "#User Config" + vbCrLf + Options.UserConfigText
        Else
            If Not String.IsNullOrEmpty(Options.DatabaseConfigText) Then
                Content += vbCrLf + "#" + Options.RawGameID + vbCrLf + Options.DatabaseConfigText
            End If
            Content += vbCrLf + Fixes
        End If

        Return Content
    End Function

    ''' <summary>The PSP configuration, optionally followed by the content of a user config file.</summary>
    Public Shared Function BuildPSP(Optional UserConfigText As String = Nothing) As String
        Dim Config As String = "--ps4-trophies=0" + vbCrLf +
                               "--ps5-uds=0" + vbCrLf +
                               "--trophies=0" + vbCrLf +
                               "--image=""data/USER_L0.IMG""" + vbCrLf +
                               "--antialias=SSAA4x" + vbCrLf +
                               "--multisaves=true" + vbCrLf +
                               "--notrophies=true" + vbCrLf + vbCrLf
        If UserConfigText IsNot Nothing Then Config += UserConfigText
        Return Config
    End Function

End Class
