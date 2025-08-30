The files in this directory are taken with permission from [https://github.com/homothetyhk/RandomizerMod](RandomizerMod) under the LGPL license (also present in this directory).

## Changes made

### HelperPlatformBuilder.cs

* Moved and renamed from RandomizerMod/IC/PlatformList.cs
* Added start-dependent helper platforms from RandomizerMod/IC/Export.cs's ExportStart method
* Changed namespaces and imports according to new file structure
* Changed arguments of `GetPlatformList` to use Archipelago's settings objects rather than RandomizerMod's


### StartLocationSceneEditsModule

* Moved and renamed from RandomizerMod/IC/RandomizerModule.cs
* Changed namespaces and imports according to new file structure
* Scoped to only handle scene edits to prevent starting softlocks
* Adapted to use Archipelago's settings rather than RandomizerMod's
* Formatted to match this project's formatting style
