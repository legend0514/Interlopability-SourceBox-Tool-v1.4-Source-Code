sourcebox beta v1.5.3

bug fixes:

-fixed the launcher waiting on one game before starting the others
-added connection status recovery when a game connects late
-added disk space checks before copying crossplay files and backups
-improved cs2 compiler errors and included the logs in support exports
-fixed cache mounting rules that could mix converted assets into native tf2

new stuff:

-added prepare destinations in the destinations tab
-builds destination maps locally from your installed games
-keeps imported game assets out of the download

still being tested:

-the combined five-map gmod host compiled with lighting, and the cs2 chamber test compiled too. gameplay on those new builds still needs testing
-full cs2 destination compilation hasnt been verified. cs2 uses basic topology and doesnt include all source 1 scenery
-destination prep currently needs the maps from gmod, tf2, portal 2 and half life deathmatch source, plus the native map tools
-the reported tf2 class selection and portal 2 startup crashes arent confirmed fixed yet

extract the whole zip and run Install SourceBox.cmd. cs2 needs workshop tools. this is still an experimental beta
