# Changelog

## Unreleased

- Renamed export modes to align with FModel terminology: `graphics` became `textures`, and `spatial` was split into `models` and `animations`. The new `models` mode exports meshes, skeletons, materials, and landscapes, while `animations` exports animation assets only.
- Added `export --script-bytecode` to include Unreal script bytecode in JSON exports when supported. The flag is accepted for all export modes but only affects `json`.

## v0.0.1

- Initial release

