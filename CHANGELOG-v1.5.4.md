sourcebox beta v1.5.4

bug fixes:

-fixed destination prep looking in the wrong folder for tf2 materials
-fixed gmod startup expecting a child process when the original verified process stays running
-added a regression check that a gmod ownership error doesnt block the other selected games
-made the missing frenzy message identify half life deathmatch source and its steam app id

53 launcher checks passed. the exact tester failure hasnt been reproduced on their pc yet

destination prep still needs all five source maps. missing-map skipping isnt implemented. native gameplay of the new destination builds and full cs2 destination compilation still need testing. the reported tf2 class-selection and portal 2 startup crashes arent confirmed fixed

extract the whole zip and run Install SourceBox.cmd. the -source.zip is for code inspection, not playing
