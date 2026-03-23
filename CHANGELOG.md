# Changelog

## Unreleased

- None

## v0.1.0

- Renamed export modes to align with FModel terminology: `graphics` became `textures`, and `spatial` was split into `models` and `animations`. The new `models` mode exports meshes, skeletons, materials, and landscapes, while `animations` exports animation assets only.
- Added `export --script-bytecode` to include Unreal script bytecode in JSON exports when supported. The flag is accepted for all export modes but only affects `json`.
- Fixed detex initialization for texture exports and aligned its startup setup with the existing zlib and oodle native dependency initialization near `uas.exe`.
- Fixed CLI version reporting so `--version` works without requiring export/list options, and set local dev builds to default to assembly version `0.0.0.0`.

## v0.0.1

- Initial release

