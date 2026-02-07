# Codename Blackout

![Unity Version](https://img.shields.io/badge/Unity-6000.1.2f1-blue)
![Game Genre](https://img.shields.io/badge/Genre-Stealth%20Action-red)
![Platform](https://img.shields.io/badge/Platform-PC-green)

## 📖 Story

**Codename Blackout** is a top-down stealth action game set during the Vietnam War. You take control of an anonymous soldier from the Vietnamese army whose mission is critical and dangerous. Deep behind enemy lines, you must infiltrate heavily guarded enemy installations, navigate through hostile territory, and plant explosives to destroy key strategic targets.

The mission is simple but deadly: **reach the designated destination and plant your bomb** to cripple the enemy's operations. However, the path is fraught with danger - enemy guards patrol the area with keen eyes and will chase you down if spotted. Your only advantages are your stealth, your wits, and your ability to remain unseen.

Will you complete your mission and return safely, or will you be caught by the enemy forces?

## 🎮 Gameplay Overview

Codename Blackout is a stealth-based tactical game where patience, strategy, and quick thinking are your best weapons. Navigate through enemy-controlled territory, avoid detection by guards, and strategically use items to aid your mission.

### Core Mechanics

- **Stealth Movement**: Control your soldier with precision as you sneak through enemy bases
- **Guard AI**: Enemy guards patrol designated routes and will chase you if you enter their field of view
- **Bomb Placement**: Place bombs strategically at your objective location to complete the mission
- **Item System**: Collect power-ups to enhance your abilities temporarily
- **Score System**: Complete missions faster and eliminate more guards to achieve higher scores

## ✨ Features

### 🎯 Strategic Stealth Gameplay
- Navigate through three challenging maps with increasing difficulty
- Avoid or eliminate patrolling guards with different behaviors
- Use environmental obstacles to break line of sight and hide from pursuers

### 🔥 Dynamic Combat System
- Place bombs to eliminate groups of guards
- Manage limited bomb resources carefully
- Use explosions strategically to create diversions or clear paths

### 💎 Power-Up System
Three types of collectible items enhance your capabilities:
- **Bomb Plus**: Increase the number of bombs you can carry
- **Extra Range**: Extend your bomb's explosion radius
- **Speed Up**: Temporarily boost your movement speed

### 🎵 Immersive Audio
- Background music to set the tension
- Sound effects for actions and alerts
- Audio feedback for mission success and failure

### 📊 Scoring System
Your performance is evaluated based on:
- **Time**: Complete missions quickly to maximize your score
- **Eliminations**: Bonus points for taking out red guards (50 points) and yellow guards (30 points)
- **Initial Score**: 300 points base score, minus time taken

```
Final Score = 300 - Seconds Taken + (Red Guards × 50) + (Yellow Guards × 30)
```

## 🎮 Controls

### Movement
- **W/↑** - Move Up
- **A/←** - Move Left
- **S/↓** - Move Down
- **D/→** - Move Right

### Actions
- **Space Bar** - Place Bomb
- **ESC** - Pause Game

*Note: The game uses Unity's new Input System for responsive controls.*

## 🗺️ Game Levels

The game features **three mission maps**, each with unique layouts and challenges:

1. **Map 1** - Tutorial/Easy: Learn the basics of stealth and bomb placement
2. **Map 2** - Intermediate: More guards and complex patrol routes
3. **Map 3** - Hard: The ultimate test with maximum enemy presence

## 🛠️ Technical Information

### Built With
- **Engine**: Unity 6000.1.2f1
- **Rendering**: Universal Render Pipeline (URP)
- **Input System**: Unity's New Input System
- **2D Features**: 
  - Rigidbody2D physics
  - 2D lighting system
  - Sprite animations
  - Tilemap system

### Project Structure
```
CodenameBlackout/
├── Assets/
│   ├── Actions/           # Input action assets
│   ├── Animations/        # Character and object animations
│   ├── Audio/            # Sound effects and music
│   ├── Prefabs/          # Reusable game objects
│   ├── Scenes/           # Game levels (MainMenu, Map 1-3)
│   ├── Scripts/
│   │   ├── Gameplay/     # Core game mechanics
│   │   │   ├── Player/   # Player movement and bomb controller
│   │   │   ├── Guards/   # AI pathfinding and chasing
│   │   │   ├── Items/    # Power-up system
│   │   │   └── Explosion/ # Bomb and explosion logic
│   │   └── Manager/      # Game, UI, Audio, and Score managers
│   ├── Sprites/          # 2D graphics and textures
│   ├── Tiles/            # Tilemap assets
│   └── UI/               # User interface elements
├── Packages/             # Unity package dependencies
└── ProjectSettings/      # Unity project configuration
```

### Key Systems

#### Player System
- **MovementController**: Handles player movement with Rigidbody2D physics
- **BombController**: Manages bomb placement and explosion mechanics
- **AnimatedSpriteRenderer**: Controls directional sprite animations

#### Enemy AI
- **GuardPathFindingPatrol**: Implements A* pathfinding for guard patrols
- **GuardPathChasingPlayer**: AI behavior for detecting and chasing the player
- **PathfindingGridManager**: Manages the navigation grid for AI

#### Game Management
- **GameManager**: Controls game states (MainMenu, Playing, Paused, Win, GameOver)
- **ScoreManager**: Tracks and calculates player performance
- **UIManager**: Handles all user interface elements
- **AudioManager**: Manages background music and sound effects

## 🚀 Getting Started

### Prerequisites
- Unity Hub
- Unity 6000.1.2f1 (or compatible version)
- Git for version control

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/motuen66/CodenameBlackout.git
   cd CodenameBlackout
   ```

2. **Open in Unity Hub**
   - Open Unity Hub
   - Click "Add" and select the project folder
   - Ensure Unity 6000.1.2f1 is installed
   - Open the project

3. **Open Main Scene**
   - Navigate to `Assets/Scenes/MainMenu.unity`
   - Press the Play button in Unity Editor

### Building the Game

1. Go to **File → Build Settings**
2. Ensure all scenes are added (MainMenu, Map 1, Map 2, Map 3)
3. Select your target platform (PC, Mac, Linux)
4. Click **Build** and choose output directory

## 🎯 How to Play

1. **Start the game** from the Main Menu
2. **Read the mission brief** to understand your objective
3. **Sneak through the map** avoiding guard detection
4. **Collect power-ups** to enhance your abilities
5. **Reach the target location** and place your bomb
6. **Escape or eliminate enemies** strategically
7. **Complete the mission** and aim for the highest score!

### Tips for Success
- 🕵️ Stay out of guard sight cones indicated by their lights
- ⏱️ Move quickly but carefully - time affects your score
- 💣 Save bombs for groups of enemies or critical moments
- 🎯 Use obstacles and walls to break line of sight when chased
- ⚡ Speed boosts help you outrun guards temporarily
- 🎮 Practice makes perfect - learn guard patrol patterns

## 🎨 Game States

The game features multiple states:
- **Main Menu**: Start screen with game options
- **Mission Brief**: Story and objective display before gameplay
- **Playing**: Active gameplay state
- **Paused**: Pause menu with options
- **Win**: Mission success screen with score
- **Game Over**: Mission failure screen

## 📝 Credits

Developed by **DefaultCompany** (Team)

### Technologies Used
- Unity Engine
- Universal Render Pipeline
- Kwaaktje Pathfinder 2D (A* pathfinding implementation)
- Unity New Input System
- TextMesh Pro

## 📄 License

This project is part of an educational/portfolio game development project.

## 🐛 Known Issues

- None currently reported

## 🔮 Future Enhancements

Potential features for future versions:
- Additional maps and mission types
- More enemy types with varied behaviors
- Additional power-ups and weapons
- Multiplayer co-op mode
- Save/load game functionality
- Achievement system

## 📧 Contact

For questions, feedback, or contributions, please open an issue on the GitHub repository.

---

**Remember**: Stealth is your greatest weapon. Stay hidden, move carefully, and complete your mission. Good luck, soldier! 🎖️
