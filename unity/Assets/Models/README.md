# Models

Drop FBX, OBJ, or other Unity-native 3D model formats here.

`SetupModelViewerScene` picks the **first .fbx** in this folder alphabetically and wires it into the model viewer as the default test model. Unity imports FBX at edit time and bakes the meshes into the APK — you get proper material conversion, optimised geometry, and no runtime loading cost.

## When to use this folder vs runtime loading

| Use bundled FBX (here) | Use runtime `ModelLoader` |
|---|---|
| Known, fixed project models | User-selected or downloaded at runtime |
| Ship with the APK | Fetched from filesystem or URL |
| Unity import pipeline (LODs, lightmap UVs, material graph) | glTF/GLB only |
| Best performance | Flexibility |

## Adding a new model

1. Drop the `.fbx` file here
2. Run `SetupModelViewerScene` — it'll pick up the first alphabetical `.fbx`
3. Build and deploy

For multiple models or runtime swapping, edit `unity/Assets/Scripts/Editor/QuestBuildTools.cs:LoadBundledFBX` to pick a specific file.
