Game Title
AngryBirdsReplica

Platform and How to Run
Developed with Unity 2022.3.62f3c1.
Built for Windows 64-bit.
Double-click AngryBirdsReplica.exe in the run folder to play. Keep the accompanying data folder and all other runtime files in the same directory as the executable.

Controls
Hold the left mouse button on the red bird, drag backward to aim, and release to launch.
Click Next Level after winning Level 1 to enter Level 2.
Click Retry to restart the current level.

Basic Features
- Working slingshot with a limited drag range.
- Physics-based bird collisions and ground bouncing.
- At least three monsters per level.
- Monsters are defeated by sufficiently strong impacts, including falls and collisions with structures.
- A particle effect appears when a monster is defeated.
- The next bird spawns automatically after a launch, until the level's supply is exhausted.

Improvements / Bonus Features
- Two levels: Level 1 has three enemies, and Level 2 has four.
- Level 2 introduces a different layout with twin towers, multi-storey structures, and stone beams.
- Wood and stone beams use different mass and friction settings for distinct physical behaviour.
- Each level provides five birds. The HUD shows the remaining unlaunched birds and remaining enemies.
- Defeating all enemies triggers a win. After all birds have been used, the game waits for physics motion to settle before determining a loss.
- Next Level advances from Level 1 to Level 2. Completing Level 2 displays "Congratulations!".
- Retry restarts the current level.
- Monsters that cross buffered boundaries beyond the left, right, or bottom of the screen are removed, and the enemy count is updated to prevent them from blocking level completion.