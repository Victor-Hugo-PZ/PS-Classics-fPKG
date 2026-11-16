# PS Classics fPKG Builder

> **Maintenance notice:** the original project was abandoned by its author, [SvenGDK](https://github.com/Victor-Hugo-PZ/PS-Classics-fPKG/commits?author=SvenGDK) ([original repository](https://github.com/SvenGDK/PS-Classics-fPKG-Builder)). This fork **continues its development**: bug fixes, dependency updates, automated tests and documentation. All credit for the original work goes to SvenGDK.

- Easily create PS1, PS2 & PSP game fake packages for the PS4 / PS5 on macOS, Linux and Windows
- Supports .BIN files (PS1) & .ISO files (PS2 & PSP)
- Detects PS1 game protection and can apply the required patch
- Automatic PS1 game TOC generation for games using CDDA music
- Customize the icon & background
- Configurate the used emulator
- Select up to 4 discs on PS1 & up to 5 discs on PS2
- Automatically apply game configs (Windows & Linux only atm)

## Notes for macOS users
- Runs on macOS 12.0 or newer
  - Intel or Apple ARM
- Wine runs under the hood to get the publishing tools working on macOS, this is why the archive size is way bigger for macOS.
  - This is experimental but works.
- This is a portable app, if you want to move it into the 'Applications' folder then you have to move the entire extracted directory including its 'Tools' folder in 'Applications'.
- You will need to add an exception when opening the app for the first time (CMD+Open) :
  - https://support.apple.com/en-gb/guide/mac-help/mh40616/mac
 
## Notes for Linux users
- Available as installable `.deb` file or portable `.tar.gz`
- Use the portable version when using Debian
- The portable build requires manual installation of following packages (if not installed yet) :
    - `7zip` `binutils` `wine64` `winetricks`
- When using Debian install `winehq-stable` instead of `wine64` & `winetricks`, additionally install the `vcrun2008` runtime in your wine prefix
- Simply double-click on makePSClassicsfPKG to run the portable version

#### Installation using .deb (Tested on Mint & Ubuntu only)
- Install with `sudo apt install ./makePSClassicsfPKG_1.0.0_amd64.deb`
- Set required folder permissions:
  - `sudo chmod -R a+rwx /usr/lib/makePSClassicsfPKG/Cache`
  - `sudo chmod -R a+rwx /usr/lib/makePSClassicsfPKG/Tools`
- Run using the created shortcut or `makePSClassicsfPKG`

PS Classics fPKG Builder uses the following tools from other developers:

| Tool | Created by | Repository |
| --- | --- | --- |
| `crc` | asahui | [https://gist.github.com/asahui/a6af64606a9476a40442274335f5feaf](https://gist.github.com/asahui/a6af64606a9476a40442274335f5feaf)
| `Cue2toc` | Goatman13 | [https://github.com/Goatman13/Cue2toc](https://github.com/Goatman13/Cue2toc)
| `pspdecrypt` | John-K & More | [https://github.com/John-K/pspdecrypt](https://github.com/John-K/pspdecrypt)
| `sfo` | hippie68 | [https://github.com/hippie68/sfo](https://github.com/hippie68/sfo)
| `unar` | The Unarchiver | [https://theunarchiver.com/command-line](https://theunarchiver.com/command-line)
| `wine` | WineHQ | [https://www.winehq.org/](https://www.winehq.org/)
| `LibOrbisPkg` | OpenOrbis (LGPL-3.0) | [https://github.com/OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)
| `DiscUtils` | Kenneth Bell & contributors | [https://github.com/DiscUtils/DiscUtils](https://github.com/DiscUtils/DiscUtils)

## Building from source (Windows)
Requirements: Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download). Visual Studio is optional.

```
git clone --recurse-submodules https://github.com/Victor-Hugo-PZ/PS-Classics-fPKG.git
cd PS-Classics-fPKG
dotnet build "Windows/PS Classics fPKG Builder.sln"
```

- [LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) is a git submodule (`external/LibOrbisPkg`) and is compiled together with the app. If you cloned without `--recurse-submodules`, run `git submodule update --init`.
- The third-party tools and emulators are **not** part of this repository (they are git-ignored). Extract the `Tools` folder of the [original Windows release](https://github.com/SvenGDK/PS-Classics-fPKG-Builder/releases/tag/v1) into the repository root (`Tools/`). The build copies it next to the executable when it exists.
- Run the app with `dotnet run --project "Windows/PS Classics fPKG Builder"`.

## Running the tests
```
dotnet test "Windows/PS Classics fPKG Builder.sln"
```
- Unit tests cover the game databases, emulator configuration, `param.sfo`, GP4 editing, ISO/UDF helpers and the PSP EBOOT patching.
- Integration tests run the real tools (`7z`, `strings`, `crc`, `cue2toc`, `gengp4`, `orbis-pub-cmd`) and build complete PS1, PS2 and PSP packages from small synthetic discs. They are **skipped** when the `Tools` folder is not available.

## Project status
This fork is being modernized step by step:

- [x] Windows app migrated from .NET Framework 4.8.1 to .NET 10 (SDK-style project)
- [x] LibOrbisPkg built from source instead of a local DLL
- [x] Build logic extracted from the window into testable classes (`Windows/.../Core`)
- [x] Automated unit and integration tests (Windows)
- [x] Fixed: PS1 discs 3/4 ignored, unterminated `--path-patches`, lost PS2 emulator fixes, pipe deadlocks, leaked file handles, working-directory dependent paths
- [x] Linux app (`Linux/`): moved to .NET 10, LibOrbisPkg from source, same fixes for the game ID parsing, pipe deadlocks and `--path-patches` quote, with tests (`dotnet test Linux/makePSClassicsfPKG.sln`). **Not run on a real Linux/Wine setup yet**, it only builds and passes the unit tests
- [ ] Linux app: extract the build logic out of `MainWindow.axaml.cs` (like the Windows `Core` classes) and test it
- [ ] macOS app (`macOS/`): known crashes and config bugs (see `CLAUDE.md`)
- [x] Windows: PS1 TOC for every disc (without leaving files in the game folder), PS2 game ID derived from the NP title when it is not detected, warning when the `Tools` folder is missing
- [ ] Verify on a real console: PS2 `-vu0-opt-flags` (single dash in the original), the PS3 config (`--lopnor-config`) now enabled for database configs, and the new `disc2.toc` to `disc4.toc` of multi-disc PS1 games
