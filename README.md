# SourceBox beta v1.5 source

Source snapshot for reviewing the six-game crossplay prototype. This is source
code, not the playable download. Browse the source folders below on GitHub.

[Download SourceBox beta v1.5](https://github.com/legend0514/Interlopability-SourceBox-Tool/releases/tag/v1.5)

Version 1.5 includes verified GMod bridge reuse, player lifecycle fixes, E+R
feedback improvements and disabled Share Cube. Ordinary prop synchronization
and portals remain. The portable package includes the Shared Room; expanded
map destinations are not bundled. Deathmatch portal rendering and missing audio
remain under investigation. Fresh-PC gameplay is not independently verified.

- [outputs/sourcebox-launcher/src](outputs/sourcebox-launcher/src): desktop launcher and its automated tests.
- [outputs/sourcebox](outputs/sourcebox): coordinator, adapters, portable v1.5 sheets and tools.
- [development/sheets](development/sheets): sanitized expanded-world development configuration.
- [release-config/sheets](release-config/sheets): the actual portable v1.5 configuration. Expanded
  worlds are disabled in that download.
- [sdk](sdk): pinned Valve SDK patch and added source files.
- [BUILD.md](BUILD.md): build prerequisites and current reproducibility limits.
- [SECURITY.md](SECURITY.md): installation behavior and review entry points.
- [release-provenance.json](release-provenance.json): the corresponding download hash and SDK revision.
- [source-manifest.json](source-manifest.json): hashes of the source snapshot files.

Personal logs, crash dumps, credentials, installed games and extracted game
assets are excluded. Personal developer paths were replaced with
`C:/SourceBox-dev`; sanitized file names are listed in the provenance file.
Those paths are placeholders to configure, not an installation requirement.

Source availability enables review. It does not certify that the program is
malware-free or establish that every distributed binary was built from this
snapshot. The full release has not passed an independent reproducible-build
comparison or fresh-PC gameplay acceptance. See LICENSE.txt before reuse.
