MaNGOSWoWRegeneration
=======================

Generic WoW `/Data` folder regenerator. Downloads the client data files straight from
Blizzard's legacy distribution CDN, for 4.3.4 (15595), 5.1.0 (16357), 5.1.1 (16685) and
5.4.8 (18414).

Two front ends share the same core:

+ `WoWRegeneration.Gui.exe` — Windows GUI: pick version, locale, OS and destination,
  watch per file and overall progress, cancel and resume at any time.
+ `WoWRegeneration.exe` — console version, answers the same questions as prompts.

Build
-----

Open `WoWRegeneration.sln` in Visual Studio (2013 or newer, .NET Framework 4.8) and build,
or from a developer prompt:

    msbuild WoWRegeneration.sln /p:Configuration=Release

How it works
------------

The tool reads the `.mfil` manifest published next to the client files, keeps the entries
matching the chosen locale and OS, and downloads them into `WoW<version>/Data`.

+ Every file is checked against the size announced by the manifest. A short or corrupt
  file is deleted and fetched again instead of being trusted.
+ Interrupted transfers resume from the byte they stopped at, using HTTP range requests,
  so a broken 2 GB download does not start over.
+ Progress is stored in `session.xml` next to the executable. Closing the tool and
  starting it again picks up where it left off.
+ Failed files are listed at the end and retried on the next run.

A full locale is roughly 15 GB for 4.3.4 and 22 GB for 5.4.8, so expect the download to
take a while.
