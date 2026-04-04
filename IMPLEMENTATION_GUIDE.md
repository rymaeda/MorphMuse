# MorphMuse Plugin - Two Open Curves Implementation Guide

## Overview

This document describes the modifications made to the MorphMuse CamBam plugin to support sweep surface generation from **two open curves** instead of requiring one closed and one open curve. The plugin now intelligently disambiguates which curve serves as the **rail** (the path to be offset) and which serves as the **form** (the profile that defines offset distances and heights).

## Architecture Changes

### 1. New Dialog Component: `CurveSelectionDialog.cs`

**Purpose:** Provides an interactive Windows Forms dialog that allows users to explicitly select which of the two open curves should be the rail and which should be the form.

**Key Features:**
- Displays both curves by their name (or ID if unnamed) in two separate dropdown menus
- Prevents selection of the same curve for both roles (automatic swap on conflict)
- Returns `SelectedRail` and `SelectedForm` properties for downstream processing

**Usage Pattern:**
```csharp
using (var dialog = new CurveSelectionDialog(openCurves))
{
    if (dialog.ShowDialog() == DialogResult.OK)
    {
        Polyline rail = dialog.SelectedRail;
        Polyline form = dialog.SelectedForm;
        // Process...
    }
}
```

### 2. Enhanced `PolylineManager.cs`

**New Properties:**
- `OpenRailPoly`: The open curve selected as the rail (path to offset)
- `OpenFormPoly`: The open curve selected as the form (profile definition)

**New Constructor:**
```csharp
public PolylineManager(Polyline openRail, Polyline openForm, bool isTwoOpen)
```
This constructor is invoked when two open curves are detected, storing both curves and marking the mode as two-open.

**Enhanced `TryCreateFromSelection()` Method:**
Now handles three scenarios:
1. **One closed + one open** (original behavior): Generates offsets of the closed curve using the open curve as a profile
2. **One closed + two open** (not yet supported): Displays a message
3. **Two open curves** (NEW): Launches the `CurveSelectionDialog` to disambiguate roles

When two open curves are selected, the dialog is displayed, and upon confirmation, a `PolylineManager` instance is created with the selected rail and form curves.

### 3. Enhanced `MorphMuseController.cs`

**New Method: `PrepareOpenCurves()`**
Mirrors the logic of `PrepareClosedCurves()` but operates on two open curves:
- Processes the **form curve** (OpenFormPoly) through `OpenPolylineProcessor` to extract simplified reference points
- Interprets reference points as offset distances (X) and heights (Y), just as in the closed-rail case
- Calls the new `GenerateParallelOpenPolylinesByGeratrizOrder()` method to generate offset sections of the **rail curve** (OpenRailPoly)
- Returns a list of sampled curves ready for triangulation

**Modified `Execute()` Method:**
- Detects whether the selection is two open curves via the flag: `isTwoOpen = selectionManager.CounterClosedP == 0 && selectionManager.CounterOpenP == 2`
- Routes to either `PrepareClosedCurves()` or `PrepareOpenCurves()` accordingly
- Passes `isClosed: !isTwoOpen` to `GenerateLateralSurface()` to control alignment behavior
- **Skips cap generation** when `isTwoOpen` is true, since two open curves do not require a closing surface

### 4. Enhanced `GenerateParallelClosedPolylines.cs` (LayerGenerator)

**New Methods:**

#### `GenerateParallelOpenPolylines(Polyline openBase, List<Point3F> openReferencePoints)`
Generates offset polylines from an open base curve:
- Iterates through reference points extracted from the form curve
- For each reference point, interprets `refPt.X` as the offset distance and `refPt.Y` as the target Z height
- Calls `CreateOffsetPolyline()` on the open rail curve (same as for closed curves)
- Adjusts Z coordinates of all points in each offset to match the form curve's height profile
- Returns a list of offset polylines

#### `GenerateParallelOpenPolylinesByGeratrizOrder(Polyline openBase, List<Point3F> openReferencePoints)`
Wrapper method that organizes offset polylines by generatrix order:
- Calls `GenerateParallelOpenPolylines()` once per reference point
- Preserves the order and grouping of contours, ensuring consistent triangulation
- Returns a `List<List<Polyline>>` matching the structure expected by `CurveSampler`

## Workflow: Two Open Curves

### Step-by-Step Execution

1. **User Selection:** User selects two open polylines in CamBam and invokes the MorphMuse plugin
2. **Validation:** `PolylineManager.TryCreateFromSelection()` detects two open curves
3. **Disambiguation Dialog:** `CurveSelectionDialog` is displayed, allowing the user to choose which curve is the rail and which is the form
4. **Curve Processing:**
   - The **form curve** is processed by `OpenPolylineProcessor` to extract simplified reference points
   - Reference points are interpreted as (offset, height) pairs
5. **Offset Generation:** For each reference point, an offset of the **rail curve** is generated at the specified distance and elevated to the specified height
6. **Surface Triangulation:** Consecutive offset curves are triangulated using adaptive edge selection (same algorithm as for closed curves)
7. **Output:** A surface mesh is created and added to the CamBam drawing

### Geometric Interpretation

| Aspect | Closed + Open | Two Open |
|--------|---------------|----------|
| **Rail** | Closed curve (path) | Open curve (path) |
| **Form** | Open curve (profile) | Open curve (profile) |
| **Offsets** | Inward/outward from closed curve | Inward/outward from open curve |
| **Heights** | Extracted from form curve's Y values | Extracted from form curve's Y values |
| **Cap** | Yes (closes the top surface) | No (both ends remain open) |
| **Alignment** | Rotated to match previous contour | No rotation (open curves have natural direction) |

## Key Implementation Details

### Offset Interpretation for Open Curves

When offsetting an open curve:
- **Positive offset:** Moves the curve outward (perpendicular to the tangent)
- **Negative offset:** Moves the curve inward
- **Handling:** CamBam's `CreateOffsetPolyline()` method is used, which returns an array of polylines (typically one for valid offsets, zero for invalid offsets)

### Height Assignment

The Y coordinate of each reference point from the form curve is assigned as the Z coordinate of all points in the corresponding offset polyline. This creates a "vertical sweep" where the rail curve is offset and elevated according to the form curve's profile.

### Surface Continuity

The adaptive triangulation algorithm in `SurfaceBuilderCopilot.BuildSurfaceBetweenCurves()` ensures smooth transitions between consecutive offset curves by choosing the shortest cross-connection at each step, avoiding twisted or self-intersecting surfaces.

## Backward Compatibility

All existing functionality for **closed + open** curve combinations remains unchanged:
- `PrepareClosedCurves()` is unchanged
- `GenerateCapSurface()` is still called for closed curves
- The `isClosed` parameter in `GenerateLateralSurface()` ensures proper alignment for closed curves

## Testing Recommendations

1. **Basic Two-Open Test:**
   - Create two simple open curves (e.g., lines or arcs)
   - Select both and invoke the plugin
   - Verify the dialog appears and allows role selection
   - Confirm the surface is generated correctly

2. **Offset Validation:**
   - Use curves with known offset distances
   - Verify that the generated surface respects the offset values

3. **Height Variation:**
   - Use a form curve with varying Y values
   - Confirm that the generated surface shows corresponding height variation

4. **Edge Cases:**
   - Test with very short curves
   - Test with curves that have very small or very large offsets
   - Test with curves that cannot be offset (should gracefully skip invalid offsets)

5. **Backward Compatibility:**
   - Test existing closed + open workflows to ensure no regression

## Compilation Notes

The modified code is written in C# and targets the CamBam plugin architecture. Ensure that:
- CamBam's API assemblies are referenced in the project
- The `System.Windows.Forms` namespace is available for the dialog
- The project is compiled with the same .NET Framework version as the original plugin

## Future Enhancements

Potential improvements for future versions:
1. **Closed + Two Open:** Support one closed curve and two open curves (e.g., for lofting between two profiles)
2. **Rail Curve Simplification:** Add options to simplify the rail curve before offsetting
3. **Form Curve Interpolation:** Support non-linear interpolation of heights between reference points
4. **Preview Mode:** Display a preview of the surface before final generation
5. **Offset Validation:** Warn the user if any offset cannot be generated due to geometric constraints

## File Manifest

| File | Status | Changes |
|------|--------|---------|
| `Program.cs` | Unchanged | Entry point remains the same |
| `MorphMuseController.cs` | Modified | Added `PrepareOpenCurves()`, enhanced `Execute()` |
| `PolylineManager.cs` | Modified | Added `OpenRailPoly`, `OpenFormPoly`, new constructor, enhanced `TryCreateFromSelection()` |
| `GenerateParallelClosedPolylines.cs` | Modified | Added `GenerateParallelOpenPolylines()`, `GenerateParallelOpenPolylinesByGeratrizOrder()` |
| `CurveSelectionDialog.cs` | **New** | Interactive dialog for role disambiguation |
| `SurfaceBuilderCopilot.cs` | Unchanged | Existing `isClosed` parameter handles both modes |
| `CurveSampler.cs` | Unchanged | Works with both closed and open contours |
| `OpenPolylineProcessor.cs` | Unchanged | Processes form curves identically |
| All other services | Unchanged | No modifications required |

## Conclusion

The implementation seamlessly extends the MorphMuse plugin to support two open curves while maintaining full backward compatibility with existing workflows. The addition of an interactive disambiguation dialog ensures a smooth user experience, and the reuse of existing offset and triangulation algorithms minimizes code duplication and maintenance burden.
