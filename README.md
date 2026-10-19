# SourceBox beta v1.4 source

Source snapshot for reviewing the six-game crossplay prototype. This is source
code, not the playable download. Browse the folders below directly on GitHub.

This snapshot includes the TF2 class-selection and window-proportion fixes.
The matching download is named v1.4.2; its internal app version remains v1.4.
Game testing for those fixes is still pending.

- [outputs/sourcebox-launcher/src](outputs/sourcebox-launcher/src): desktop launcher and its automated tests.
- [outputs/sourcebox](outputs/sourcebox): coordinator, adapters, portable v1.4 sheets and tools.
- [development/sheets](development/sheets): sanitized expanded-world development configuration.
- [release-config/sheets](release-config/sheets): the actual portable v1.4 configuration. Expanded
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
