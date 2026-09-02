# 001: Normalize Recraft WebP responses before canonical publication

## Cause

Recraft returned a valid WebP raster when the requested AssetCtl output required PNG.

## Fix

AssetCtl now accepts bounded JPEG and WebP provider rasters, then normalizes them to canonical PNG before publication. Existing exact-dimension, alpha, byte/pixel, decode, and SVG mismatch checks remain enforced.

## Lesson

Provider response media types may differ from the manifest's canonical output type. Validate the provider input safely, then normalize it before the final output contract and Godot import boundary.
