## 🧩 MorphMuse — Surface Modeling Plugin for CamBam

**MorphMuse** is a custom plugin for [CamBam](http://www.cambam.info/), designed to generate smooth, triangulated surfaces from 2D polylines. It enables advanced mesh creation workflows by combining open and closed curves, sampling them intelligently, and building structured surfaces suitable for CNC, 3D modeling, or artistic design.

### ✨ Features

- Generate surfaces between:
  - One open polyline (generatrix) and multiple closed contours (guide layers)
  - Two open polylines (experimental mode)
- Recursive offsetting of closed curves to build layered structures
- Parametric sampling with adjustable density and tolerance
- Surface triangulation with automatic vertex indexing
- Cap and lateral surface generation for closed volumes
- Layer management and automatic color tagging
- Logging and diagnostics for geometry alignment and mesh quality

### 🛠️ Technologies

- Built with **C#** targeting **.NET Framework 4.6 / 4.8**
- Integrates directly with **CamBam's CAD and geometry APIs**
- Uses custom classes for surface building, curve sampling, and mesh generation

### 📦 Installation

1. Copy the compiled DLL into CamBam's plugin folder.
2. Restart CamBam.
3. Access MorphMuse from the plugin menu.

### 📐 Use Cases

- CNC surface machining
- Artistic modeling from 2D sketches
- Parametric mesh generation
- Educational geometry tools

### 🧠 Credits

Developed by **Ricardo Y Maeda**  
Special thanks to the CamBam community and plugin ecosystem.
