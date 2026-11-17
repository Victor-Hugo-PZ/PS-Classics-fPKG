PS Classics fPKG Builder (Linux)
=================================

Original version by Victor. Continues the project of SvenGDK.

Requirements
  - Linux x64 (Ubuntu 22.04+, Fedora, Arch, ...)
  - Wine and winetricks: the PS4 publishing tools are Windows programs and run through Wine.
    The .NET runtime is included: nothing else to install.

1. Install Wine and winetricks (once)
     Ubuntu / Debian / Mint:  sudo dpkg --add-architecture i386 && sudo apt update && sudo apt install wine wine32:i386 winetricks
     Fedora:                  sudo dnf install wine winetricks
     Arch:                    sudo pacman -S wine winetricks   (enable the multilib repository first)

2. Unzip the download and start the app
     unzip PS.Classics.fPKG.Builder.*.Linux-x64.zip && cd "PS Classics fPKG Builder v"*
     ./PSClassicsfPKGBuilder
   (If your file manager extracted it without permissions: chmod +x PSClassicsfPKGBuilder PkgCheck install-shortcut.sh)
   Optional: ./install-shortcut.sh adds it to your applications menu.

3. Prepare Wine (once)
   Press "Wine setup" at the bottom left of the app, then "Set up Wine". It creates a Wine folder for the app
   and installs the Visual C++ 2008 runtime the tools need. The status at the bottom left turns to "Wine ready".

Contents
  PSClassicsfPKGBuilder   The app: creates PS1, PS2 and PSP fake packages for PS4/PS5.
  PkgCheck                Command line tool: checks a .pkg and tells you if something is wrong.
  Tools/                  Third-party tools and emulators. Keep this folder next to the programs.

Using the app
  The Guide button explains every field. Settings: language (English / Português), step-by-step mode,
  package check after building, update notice. History: the packages you built and how they ran on the console.
  Tip: drag and drop the game (.bin, .cue, .iso), images or a .pkg anywhere on the window.

PkgCheck (command line)
  ./PkgCheck game.pkg                 Full check with the SDK checker (through Wine; can take minutes).
  ./PkgCheck game.pkg --quick         Structural checks only (instant, no Wine needed).
  ./PkgCheck game.pkg --tool-output   Also prints the complete output of the SDK checker.
  Exit code: 0 = no problems, 1 = problems found, 2 = the check could not run.

Advanced
  PSFPKG_WINE=/path/to/wine         Use a specific Wine binary.
  PSFPKG_WINEPREFIX=/path/to/prefix Use another Wine prefix (default: ~/.local/share/PSClassicsfPKGBuilder/wine-prefix).
