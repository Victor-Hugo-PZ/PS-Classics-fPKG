PS Classics fPKG Builder (Windows)
===================================

Original version by Victor. Continues the project of SvenGDK.

Requirements
  - Windows 10 or 11
  - Nothing else: the .NET runtime is included, just run the .exe

Contents
  PS Classics fPKG Builder.exe   The app: creates PS1, PS2 and PSP fake packages for PS4/PS5.
  PkgCheck.exe                   Command line tool: checks a .pkg and tells you if something is wrong.
  Tools\                         Third-party tools and emulators. Keep this folder next to the programs.

The app
  Run "PS Classics fPKG Builder.exe". The Guide button explains every field.
  The "Check a .pkg" button checks a package from inside the app.
  Settings: language (English / Português), step-by-step mode, package check after building, update notice.
  History: the packages you built, the options used and how each one ran on the console.
  Tip: drag and drop the game (.bin, .cue, .iso), images or a .pkg anywhere on the window.

PkgCheck (command line)
  PkgCheck.exe game.pkg              Full check with the SDK checker (can take minutes on large games).
  PkgCheck.exe game.pkg --quick      Structural checks only (instant, no Tools folder needed).
  PkgCheck.exe game.pkg --tool-output   Also prints the complete output of the SDK checker.
  Exit code: 0 = no problems, 1 = problems found, 2 = the check could not run.

  The report has two groups: PROBLEMS (may stop the package from installing or running) and
  "Expected for fake packages" (Sony certification rules that every fake package fails; safe to ignore).
