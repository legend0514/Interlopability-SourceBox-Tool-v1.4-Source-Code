# SourceBox beta v1.5.3 source

Source snapshot for reviewing the six-game crossplay prototype. This is source
code, not the playable download. Nothing runs merely by extracting this ZIP.

- `outputs/sourcebox-launcher/src`: desktop launcher and its automated tests.
- `outputs/sourcebox`: coordinator, adapters, portable v1.5.3 sheets and tools.
- `development/sheets`: sanitized expanded-world development configuration.
- `release-config/sheets`: the actual portable v1.5.3 configuration. Expanded
  worlds are disabled in that download.
- `sdk`: pinned Valve SDK patch and added source files.
- `BUILD.md`: build prerequisites and current reproducibility limits.
- `SECURITY.md`: installation behavior and review entry points.
- `release-provenance.json`: the corresponding download hash and SDK revision.
- `source-manifest.json`: hashes of the source snapshot files.

Personal logs, crash dumps, credentials, installed games and extracted game
assets are excluded. Personal developer paths were replaced with
`C:/SourceBox-dev`; sanitized file names are listed in the provenance file.
Those paths are placeholders to configure, not an installation requirement.

Source availability enables review. It does not certify that the program is
malware-free or establish that every distributed binary was built from this
snapshot. The full release has not passed an independent reproducible-build
comparison or fresh-PC gameplay acceptance. See LICENSE.txt before reuse.
