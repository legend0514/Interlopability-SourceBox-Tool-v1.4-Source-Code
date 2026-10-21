# What to inspect

This is an unsigned experimental game mod. Review the code and build it yourself
where practical. Keep antivirus protections enabled. No malware-free guarantee
or independent security audit is claimed.

## Expected behavior

- The installer uses Windows Package Manager when .NET Desktop Runtime 8 x64
  or Node.js is missing. Installation may require Windows approval.
- The launcher detects Steam libraries and installs adapters/configuration in
  selected game folders. Review CrossplaySetup.cs, SdkConfiguration.cs,
  DesktopInstall.cs and installer-beta.ps1 before granting write access.
- Setup can download Metamod, CounterStrikeSharp, Blender and SourceIO from
  the URLs pinned in CsLoaderPreparation.cs and SetupDownloads.cs. Downloads
  use recorded checksums. Game model conversion reads installed game content.
- Runtime.cs and ServiceHost.cs start game and helper processes. OwnedProcess
  records process identity and may terminate owned processes when Stop is used.
- Core networking is local TCP on 127.0.0.1:27841; GMod uses a local bridge on
  27842. Games also use native local server ports. This is not internet matchmaking.
- GMod uses Lua, Source 1 uses native adapters/modified SDK game modules and
  CS2 uses a CounterStrikeSharp plugin through Metamod. Native preview code
  accesses game-rendering interfaces; review it as code running inside a game.
- GameWindowLayout.cs repositions windows and registers explicit focus hotkeys.
  This includes cursor-release calls; it is not a promise that all focus bugs
  are fixed.
- Settings, setup caches, process receipts and logs are written under the
  SourceBox local application-data directory. ConfigProtection.cs backs up
  selected native cfg files and protects existing files during the session. Cleanup
  preserves newer settings instead of restoring stale backups. It does not
  cover every Steam Cloud/userdata setting or recover from every interruption.
- Support export is user-triggered. It redacts common paths/identifiers but
  users should still inspect an export before sharing it.

## Not included in this source archive

No executable/DLL build outputs, game archives, extracted game models/textures,
Workshop addon implementations, personal logs, crash dumps, saves or settings.
The icon and short intro are the launcher's presentation assets.

## Reporting

Include version, affected game, reproduction steps and a redacted log excerpt.
Do not post Steam credentials, access tokens, save files or private crash dumps.
There is no authenticated vulnerability-reporting service configured in this
archive. Contact the person who distributed your copy privately for sensitive
reports.
