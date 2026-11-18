PS Classics fPKG Builder (macOS)
=================================

Original version by Victor. Continues the project of SvenGDK.

Requirements
  - macOS 12 or later. Download the arm64 zip for Apple Silicon (M1/M2/M3/M4) or the x64 zip for Intel Macs.
  - Wine and winetricks: the PS4 publishing tools are Windows programs and run through Wine.
    The .NET runtime is included: nothing else to install.

1. Install Wine and winetricks (once), with Homebrew (https://brew.sh):
     brew install --cask --no-quarantine wine-stable
     brew install winetricks
   On Apple Silicon, macOS may ask to install Rosetta 2: accept it (Wine needs it).

2. Open the app the first time
   The app is not signed with an Apple developer certificate, so macOS blocks the first launch:
     - Move "PS Classics fPKG Builder.app" to Applications.
     - Right-click (or Control-click) the app and choose Open, then Open again in the dialog.
     - On macOS 15 or later: try to open it once, then go to System Settings > Privacy & Security
       and press "Open Anyway".
   Alternative (Terminal):  xattr -dr com.apple.quarantine "/Applications/PS Classics fPKG Builder.app"

3. Prepare Wine (once)
   Press "Wine setup" at the bottom left of the app, then "Set up Wine". It creates a Wine folder for the app
   and installs the Visual C++ 2008 runtime the tools need. The status at the bottom left turns to "Wine ready".

Where things are
  The packages are saved in the folder you choose. Temporary build files are kept in
  ~/Library/Application Support/PSClassicsfPKGBuilder (the Cache folder can be deleted at any time).

Using the app
  The Guide button explains every field. Settings: language (English / Português), step-by-step mode,
  package check after building, update notice. History: the packages you built and how they ran on the console.
  Tip: drag and drop the game (.bin, .cue, .iso), images or a .pkg anywhere on the window. A .zip or .7z of a game can be dropped too: the app extracts it and finds the game files.
  Queue: the Queue button builds several games from .zip or .7z files, one after the other, with no questions.
  PSP games in .cso (compressed) are accepted: the app converts them to .iso by itself.

PkgCheck (command line), inside the app bundle:
  "/Applications/PS Classics fPKG Builder.app/Contents/MacOS/PkgCheck" game.pkg [--quick] [--tool-output]
  Exit code: 0 = no problems, 1 = problems found, 2 = the check could not run.
