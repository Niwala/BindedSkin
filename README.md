# BindedSkin
 
Relatively simple approach to bind gameObject (or juste positions & rotations) to an existing skin on Unity (CPU only, no mesh baking).
The cubes on the leg in the gif are gameObjects with a classic meshRenderer.
![](Media/Unity_OQG2w7NDzh.gif)

## Setup
Is relatively simple :
- Add the BindedSkin component to your animated object or parent.

![](Media/Unity_3TbspCulUM.png)

- Add the BindedSkinAnchor component to a child object. This child should be placed next to your animated object when it's in bind pose (use the button in BindedSkin to put it in bind pose).

![](Media/Unity_EGm2rtF4Sy.png)

</br>

<b>SkinData:</b>
  - Position: The object will be moved according to the skin
  - Position & rotation: The object will be moved and rotated according to the skin.

<b>Snap Method:</b>
  - On Nearest Vertex: The object will snap to the nearest vertex at the first frame.
  - On Nearest Triangle: Object snapped to nearest triangle. Its coordinates will be maintained between the three points of the triangle using barycentric coordinates. The triangle can therefore be deformed.

</br>

## Optimisation
I've added a little burst/job to the skinning function. That's as far as I went. The idea is mainly to make a proof of concept. So I didn't test to find more optimized values (like for the batch size), and there's probably a way of extending these optimizations further.
