# Multiplayer FPS - ECE3200 Assignment 3

A multiplayer first-person game for Windows, built with Unity 6000.3.26f1 and Photon PUN 2. This folder contains the gameplay scripts, prefabs, editor tools and three scenes. The Windows build and course asset pack are not part of the public repository.

## How to play in the Editor

1. [Restore the dependencies](RESTORING.md) before opening the project. For online play, configure your own Photon App ID. Offline modes do not need a server connection.
2. Add the `MultiplayerFPS` folder to Unity Hub. Open `Assets/Scenes/game.unity` and press Play. This scene starts the lobby; the two arena scenes are maps.
3. Select **Offline practice** to explore a map, or **Offline AI match** to play against two bots. Choose **Ground Arena** to see the expanded freight yard.
4. To host an online game, enter a nickname, select **Join lobby**, then **Create a room**. Enter a room name and choose a map. Other players using the same version and Photon app can select the room in the Asia lobby.

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look around |
| Shift / Space | Sprint / jump |
| Left mouse / hold right mouse | Fire / aim |
| R | Reload the selected weapon |
| 1 to 4 / scroll wheel | Select or cycle weapons |
| Hold Tab | Show kills, deaths and scores |
| Y / Enter / Esc | Open room chat / send / cancel |
| Esc outside chat | Open the match menu |

## Basic features

The lobby shows rooms and player counts. In a match, other players see you move, jump and switch weapons. Shots produce recoil. Both clients see the target's health drop, and the target respawns when it reaches zero. Chat messages stay in the current room.

1. **Standard automatic**
2. **Gold automatic**: tracks its ammo and spare magazines separately from the standard weapon.
3. **Shotgun**: fires eight pellets with one shot sound. Press R to load the next shell.
4. **Explosive launcher**: the projectile drops as it travels. The blast damages nearby targets but spares the player who fired it.

## Additional features

- Hold the right mouse button to bring the weapon into an aiming position and narrow the field of view. Release to return to the normal view.
- The host can press Esc and select **SWITCH TO GROUND ARENA** or **SWITCH TO SKY ARENA**. Players change maps together and keep their kills, deaths, scores and remaining time. Late joiners start on the current map.
- In **Offline AI match**, two bots navigate around cover, follow players and shoot. They take damage, respawn and appear on the scoreboard.
- Each match lasts three minutes. All players share a countdown. When it expires, attacks stop and the game displays the final rankings.

Sky Arena keeps the flat tutorial layout. Ground Arena adds a loading lane, two freight containers, four low barriers and six cargo obstacles to the original hilly terrain.
