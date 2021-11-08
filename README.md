<h1>Gizmo Extension Tools</h1>
This repository hosts ideas for how we could potentially improve the Scene Gizmo readability.

<h2>How to use</h2>
* Either clone or download this repo as zip files.
* Install package via Window > Package Manager > Plus Sign (top left) > Add package from disk...
* Open the Placement Tools panel via the kebab menu (the three dots in the top-right corner of the Scene View window, screenshot below).

![Placement Tools](https://i.imgur.com/visjpLn.png "Placement Tools")

* Make sure to select the View Tool ![View Tool](https://docs.unity3d.com/uploads/Main/Editor-MoveTool.png) as the active Scene Tool to avoid built-in gizmos clashing with the overrides from this package.

**There are several settings that can be changed in the Placement Tools panel:**
* Gizmo Text Color: modifies the color of the font that the translate/rotate/scale delta is displayed in.
* Gizmo Line Color: modifies the color for all the non-fill graphics for the translate (offset lines, dots), rotate (rotation circle, snap units) and scale tools.
* Gizmo Plane Color: modifies the color for added graphics that have fill, e.g. the offset plane for the translate tool.
* Text Size: modifies the size of the font that the translate/rotate/scale delta is displayed in. 
* Unit Snap Multiplier (Move): multiplier used to multiply the EditorSnapSettings.move[x, y, z] values to have snap units spaced closer or further together.

<h2>What's New</h2>
<h3>Translate Tool</h3>

----

Displays the amount the object was offset in a single axis and a thicker line indicating the offset.

<img src="https://i.imgur.com/7gXQhBo.png" width="500">

Displays the amount the object was offset in a single axis and a thicker line indicating the offset and snap units.

<img src="https://i.imgur.com/zlEMj0J.png" width="500">

Displays the amount the object was offset along a plane, including the total offset plane.

<img src="https://i.imgur.com/HcGcHFq.png" width="500">

Displays the amount the object was offset along a plane, including the total offset plane and snapping reference guides (very WIP).

<img src="https://i.imgur.com/QxfKcqc.png" width="500">

<h3>Rotate Tool</h3>

----

Displays the amount the object was rotated and hightlights the current rotation target with a thicker white line.

<img src="https://i.imgur.com/Mgg8R3g.png" width="500">

Same as above, but with snapping enabled.

<img src="https://i.imgur.com/hZQuYRY.png" width="500">


<h3>Scale Tool (Extremely WIP)</h3>

----

Displays the amount the object was scaled and adds evenly spaced out arc-shaped markers that radiate from the object's center to visually approximate the scale amount.

<img src="https://i.imgur.com/iYZtksK.png" width="500">



