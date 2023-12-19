# BindedSkin
 
Binds objects to a SkinnedMeshRenderer.
 - Works only on the CPU side with Jobs + Burst.
 - Does not use the BakeMesh() function and does not retrieve data from the GPU.<br>Simply copy the weight of bones onto vertices to apply the same transformation matrices to GameObjects.
 - Can place objects on verteices or triangles using barycentric coordinates.
 - Can control position and rotation.

<br>

The cubes on the leg in the gif are gameObjects with a classic meshRenderer.
![](Media/Unity_OQG2w7NDzh.gif)

## Setup
Is relatively simple :
- Add the BindedSkin component to your animated object or parent.

![](Media/Unity_3TbspCulUM.png)

- Add the BindedSkinAnchor component to a child object. This child should be placed next to your animated object when it's in bind pose (use the button in BindedSkin to put it in bind pose).

![](Media/Unity_EGm2rtF4Sy.png)

- You can start the game, skininig is done in runtime.

</br>

<b>SkinData:</b>
  - Position: The object will be moved according to the skin
  - Position & rotation: The object will be moved and rotated according to the skin.

<br>

<b>Snap Method:</b>
  - On Nearest Vertex: The object will snap to the nearest vertex at the first frame.
  - On Nearest Triangle: Object snapped to nearest triangle. Its coordinates will be maintained between the three points of the triangle using barycentric coordinates. The triangle can therefore be deformed.

</br>

## Optimisation
 - Computations are performed on sync jobs. This allows relatively fast processing despite the large number of objects.

## Dependancies
 - This project requires Unity's Burst package.
