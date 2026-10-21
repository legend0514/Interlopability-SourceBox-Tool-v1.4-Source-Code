sourcebox beta v1.5.2

bug fixes:

-fixed artwork unavailable in the launcher, all 6 weapon images are included now
-fixed one game's setup failure blocking the other games from being prepared
-fixed cs2 compiler warnings being treated as fatal errors during room setup
-added setup progress and actual compiler errors instead of just a log path
-fixed unsafe replay calls in tf2's class menu and freezecam
-fixed gmod pausing crossplay when opening its menu
-fixed gmod process recovery failing after its parent process changed
-removed leftover share cube requests in cs2 and portal 2
-stopped unchanged portal 2 portals from repeatedly activating
-improved protection for existing game settings and portal 2 save paths
-fixed portal gun addon discovery and optional content fallback handling

tested locally:

-all 6 launcher weapon images load without the development files
-48 launcher checks and 62 coordinator checks passed
-all 9 tf2 classes selected twice through the actual class menu path
-cs2 shared room compiled successfully with the installed workshop tools
-portal 2 and cs2 connected and sent live player updates
-gmod props passed through portals and gmod damage reached hldm:s

still a beta. this doesn't mean every reported crash or portal rendering issue is fixed. clean pc gameplay, some missing-asset combinations and the remaining visual/audio reports still need testing.

this download includes the shared room. expanded imported maps aren't bundled. cs2 still needs workshop tools for local content conversion. game assets stay on your pc.

download sourcebox-beta-v1.5.2.zip to play. the -source.zip is the matching code for inspection, not the app.


---

sourcebox beta v1.5.1

bug fixes:

-fixed hl deathmatch source and portal 2 disconnecting during the new ragdoll tests

-disabled experimental ragdolls in the public build for now, instant respawn still works

-fixed the old version number still showing on the character screen

-added language selection in settings

-added english, spanish, french, german and portuguese for launcher menus and common status messages

-made it detect your system language automatically and remember the language you pick

-kept technical logs in english so troubleshooting doesnt turn into a guessing game

notes:

-this download still includes the shared room, the expanded maps arent bundled yet

-the cs2 expanded-map lighting work isnt in this download, dont wanna claim thats fixed for everyone yet

-hl deathmatch source and portal 2 passed local damage and respawn checks, other pcs still need testing

-34 launcher checks and 62 coordinator tests passed, plus translation checks

extract the whole zip and run Install SourceBox.cmd. close your old sourcebox session before updating.

