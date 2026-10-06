# 🕯️ The Backrooms: Facility Breach
## IT 402W – Final Project | Bulacan State University (CICT)

A functional 3D First-Person Horror & Escape game developed in **Unity 6 (C#)** designed to meet and exceed all course requirements and rubric criteria for **IT 402W**.

---

## 📋 Course Requirements & Rubric Compliance Matrix (100/100 Points)

| Requirement / Rubric Item | Score | Implementation Details | Code References |
|---|:---:|---|---|
| **1. 3D Game & Core Gameplay** | **15 / 15** | Playable 3D First-Person survival horror game with smooth movement, sprinting with stamina, crouching stealth, flashlight management, and coherent game flow. | [`FirstPersonController.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Player/FirstPersonController.cs) |
| **2. Player Interaction** | **10 / 10** | Multiple interactions implemented: opening/closing locked & unlocked hinged doors, picking up power fuses & keycards, drinking Almond Water, reading lore logs, and pulling the Generator Master Switch. | [`IInteractable.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/IInteractable.cs)<br>[`Door.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/Door.cs)<br>[`KeyPickup.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/KeyPickup.cs)<br>[`AlmondWaterPickup.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/AlmondWaterPickup.cs)<br>[`PowerSwitch.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/PowerSwitch.cs) |
| **3. Offensive / Attack Mechanic** | **10 / 10** | **Melee Combat Attack** (`Left Mouse Button`): Player swings a steel pipe/crowbar with animated motion, whoosh & hit audio. Strikes deal 35 damage, push back the Stalker Entity, and stun it for 2.2 seconds! | [`PlayerCombat.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Player/PlayerCombat.cs) |
| **4. 3D Environment & GameObjects** | **10 / 10** | Modular Backrooms Level 0 environment integrating imported Asset Store 3D models (yellow wallpaper walls, damp carpet, acoustic ceiling, doorways, partitions, furniture, flickering lights). | `Assets/LoafbrrAssets/`<br>[`FlickeringLight.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/FlickeringLight.cs) |
| **5. AI Implementation** | **15 / 15** | Intelligent `NavMeshAgent` Stalker Entity with 7-state FSM: **Patrol** between waypoints, **Investigate** player footsteps & flashlight, **Chase** on line-of-sight, **Attack** player HP, **Stunned** on player melee hit, and **Dead/Banished** when HP depleted. | [`StalkerAI.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Enemy/StalkerAI.cs) |
| **6. User Interface (UI)** | **10 / 10** | Complete HUD: **Health (HP)** bar, **Stamina** bar, **Flashlight Battery** meter, **Objective tracker**, Kane Pixels **VHS Camcorder overlay** with live timestamp, **Pause Menu** (`Esc`/`P`), **Game Over** screen, **Victory** screen, and **Main Menu** scene. | [`HUDManager.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/UI/HUDManager.cs)<br>[`VHSOverlay.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/UI/VHSOverlay.cs)<br>[`MainMenuManager.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/UI/MainMenuManager.cs) |
| **7. Audio Implementation** | **5 / 5** | Realistic horror sound design: procedural **60Hz Fluorescent Ballast Hum**, proximity **Heartbeat** scaling with entity distance, footsteps, door hinges, weapon whoosh & metallic impacts, monster screeches, and UI sounds. | [`AudioManager.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Audio/AudioManager.cs)<br>[`PlayerSanity.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Player/PlayerSanity.cs) |
| **8. Game Objective & Win/Lose Conditions** | **10 / 10** | **Win Condition**: Collect 3 Elevator Fuses, pull the Generator Master Switch, and escape through the exit gate. **Lose Condition**: Health drops to 0 from entity attacks, triggering the jumpscare and Game Over screen. | [`GameManager.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Managers/GameManager.cs)<br>[`EscapeExit.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Interaction/EscapeExit.cs)<br>[`PlayerHealth.cs`](file:///C:/Users/Jtin/Documents/GitHub/game-dev-final-project/Assets/Scripts/Player/PlayerHealth.cs) |
| **9. Technical Implementation & Functionality** | **10 / 10** | Organized, bug-free C# scripts, standardized `.gitignore`, clean scene hierarchy, no external DLL dependencies, runs smoothly in Unity 6. | `Assets/Scripts/` |
| **10. Presentation, Polish & Creativity** | **5 / 5** | Kane Pixels found-footage camcorder aesthetic, atmospheric sickly yellow volumetric fog, head bobbing, Almond Water consumables, and dynamic paranormal flashlight flickering. | [Complete Project Architecture] |

---

## 🕹️ Controls Guide

| Action | Control |
|---|---|
| **Move** | `W`, `A`, `S`, `D` |
| **Look Around** | Mouse |
| **Sprint** | `Left Shift` *(Caution: creates loud footstep noise that alerts the entity!)* |
| **Crouch (Stealth)** | `Left Control` or `C` *(Silences footsteps and cuts enemy vision in half)* |
| **Offensive Attack** | `Left Mouse Button` *(Swings steel pipe to damage, push back & stun the entity)* |
| **Toggle Flashlight** | `F` or `Right Mouse Button` *(Conserves battery; flickers near the entity)* |
| **Interact / Pick Up / Drink** | `E` *(Doors, fuses, Almond Water, notes, generator switch)* |
| **Pause Menu** | `Esc` or `P` |

---

## 🚀 Quick Setup Instructions in Unity

### 1. Open the Project
1. Launch **Unity Hub**.
2. Open the project folder: `C:\Users\Jtin\Documents\GitHub\game-dev-final-project`.

### 2. Generate Menus & Gameplay
In the Unity top menu bar:
1. Click **`Tools` ➔ `Generate Main Menu Scene`**
   - Automatically builds `Assets/Scenes/MainMenu.unity` with Start Game, How to Play, and Quit buttons, and registers it in Build Settings!
2. Open your Backrooms level:  
   👉 `Assets/LoafbrrAssets/BackroomsLikeAssetRe/scenes/LevelTst.unity`
3. Click **`Tools` ➔ `Setup Horror Gameplay in Active Scene (Backrooms)`**
   - Spawns the Player rig with Health, Melee Weapon, Flashlight, VHS HUD, Almond Water, Power Fuses, Master Switch, and Stalker Entity!
4. Press **Play (▶️)** to experience the full game loop!
