# OFS Asset Authoring

Minimal Unity project pinned to `6000.3.13f1`, the editor version used by the
supported Ore Factory Squad build.

1. Install Unity `6000.3.13f1` with Windows Build Support through Unity Hub.
2. Open this directory as a Unity project.
3. Put assets under `Assets/ModContent`.
4. Assign an AssetBundle name in each root asset's Inspector.
5. Run `OFS SDK > Validate AssetBundles`, then
   `OFS SDK > Build Windows x64 AssetBundles`.

The builder rejects embedded scripts and missing MonoBehaviours. Output includes
`ofs-bundles.json` with byte length, SHA-256, Unity hash and dependencies for
every bundle.

From the repository root, the same build can run headlessly:

```powershell
./eng/build.ps1
```

Pass `-UnityPath` when the editor is not installed under the default Unity Hub
directory.

This repository contains only authoring tools and original fixture assets. It
does not contain extracted or redistributed game content. The runtime and SDK
are maintained separately in `OFS-Modding/ofs-loader` and `OFS-Modding/ofs-sdk`.
