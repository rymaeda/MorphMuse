# MorphMuse Plugin - Changes Summary

## Overview
Extended the MorphMuse plugin to support **two open curves** in addition to the existing **one closed + one open curve** workflow.

## Key Changes

### 1. New File: `CurveSelectionDialog.cs`
- **Purpose:** Interactive Windows Forms dialog for disambiguating which curve is the rail and which is the form
- **Display:** Shows curves by their ID and layer name
- **Features:** Prevents selecting the same curve for both roles

### 2. Modified: `PolylineManager.cs`
- **Added Properties:**
  - `OpenRailPoly`: The open curve selected as the rail (path to offset)
  - `OpenFormPoly`: The open curve selected as the form (profile definition)
  
- **Added Constructor:**
  - `PolylineManager(Polyline openRail, Polyline openForm, bool isTwoOpen)` for two-open mode

- **Enhanced Method:**
  - `TryCreateFromSelection()` now handles the case of two open curves by launching the disambiguation dialog

### 3. Modified: `MorphMuseController.cs`
- **Added Method:**
  - `PrepareOpenCurves()`: Processes two open curves similar to how `PrepareClosedCurves()` works

- **Enhanced Method:**
  - `Execute()` now detects two-open mode and routes to the appropriate preparation method
  - Skips cap generation for two-open curves (no closing surface needed)

### 4. Modified: `GenerateParallelClosedPolylines.cs` (LayerGenerator)
- **Added Methods:**
  - `GenerateParallelOpenPolylines()`: Generates offset polylines from an open base curve
  - `GenerateParallelOpenPolylinesByGeratrizOrder()`: Organizes offset polylines by generatrix order

## Backward Compatibility
✅ **Fully maintained** - All existing closed + open workflows remain unchanged

## API Compatibility Note
The `CurveSelectionDialog` uses:
- `curve.ID` - Unique identifier of the polyline
- `curve.Layer.Name` - Name of the layer containing the curve

These are standard CamBam API properties and should work across versions.

## Workflow Comparison

| Scenario | Input | Dialog | Output |
|----------|-------|--------|--------|
| **Original** | 1 closed + 1 open | None | Closed surface with cap |
| **New** | 2 open curves | Yes (role selection) | Open surface (no cap) |

## Testing Checklist

- [ ] Compile without errors
- [ ] Select 1 closed + 1 open curve → works as before
- [ ] Select 2 open curves → dialog appears
- [ ] Dialog allows selecting different curves for rail and form
- [ ] Surface generates correctly for two open curves
- [ ] Surface is created in "MorphSurface" layer
- [ ] No cap is generated for two-open mode

## Files Modified

| File | Changes |
|------|---------|
| `CurveSelectionDialog.cs` | **NEW** - Dialog for role disambiguation |
| `PolylineManager.cs` | Added properties and constructor for two-open mode |
| `MorphMuseController.cs` | Added `PrepareOpenCurves()` method and enhanced `Execute()` |
| `GenerateParallelClosedPolylines.cs` | Added methods for open curve offset generation |

## Files Unchanged

- `Program.cs`
- `SurfaceBuilderCopilot.cs`
- `CurveSampler.cs`
- `OpenPolylineProcessor.cs`
- `PolylineSimplifier.cs`
- `Geometry3F.cs`
- `EarClippingTriangulator.cs`
- `SettingsManager.cs`
- `ProjectLogger.cs`
- `ConvexCapBuilder.cs`
- `FindConvergenceCentersAndCurves.cs`

## Implementation Details

### Offset Interpretation
- **X coordinate of form curve** → Offset distance from rail curve
- **Y coordinate of form curve** → Height (Z coordinate) of the surface

### Surface Generation
1. Extract reference points from form curve (offset, height pairs)
2. For each reference point, generate an offset of the rail curve
3. Elevate each offset to the corresponding height
4. Triangulate between consecutive offset curves
5. Add the resulting surface to the drawing

### Key Difference from Closed Mode
- **Closed mode:** Offsets are generated inward/outward from a closed curve; alignment is rotated to match previous contour
- **Open mode:** Offsets are generated from an open curve; no rotation (open curves have natural direction); no cap is generated

## Compilation Requirements
- CamBam API assemblies must be referenced
- .NET Framework version must match the original plugin
- `System.Windows.Forms` namespace required for dialog

## Future Enhancements
- Support for 1 closed + 2 open curves (lofting between profiles)
- Preview mode for surfaces before generation
- Offset validation warnings
- Form curve interpolation options
