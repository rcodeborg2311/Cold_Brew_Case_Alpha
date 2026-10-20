# Level 2

Level 2 is created inside the existing Unity project and reuses the Level 1 gameplay scripts, input actions, and `Assets/_Game/Data/DefaultTuning.asset`. The scene builder does not change that tuning asset or Level 1 runtime behavior.

## Create and play the scene

1. Open the project with Unity **6000.3.22f1** and wait for scripts to compile.
2. Choose **Tools > Cold Brew Case > Create Level 2 Scene**.
3. Open `Assets/_Game/Scenes/Level02.unity` (or the unique `Level02` filename Unity reports if one already existed) and press **Play**.
4. To make a standalone/Web build of Level 2, add this scene to the Build Profile and select it as the first scene for that build. Keep the existing Level 1 build settings for the Level 1 deployment.

Level 2 keeps the Level 1 tuning values: player speed 5, thief speed 12, guard speed 1.4, 10-second heat sight, 18-second shake, 96-second hunt, and two starting nets. The editor builder references the existing tuning asset rather than creating alternate values.

Controls remain **WASD** to move, **mouse** to aim, **F** to scan, **left click** to throw a net, **Space** to push, and **P** to pause.
