# Happy Golem

# **INCINDER**

Gothic Roguelike Dungeon Crawler

DES315 Abertay University 19/04/2026



##### Overview

Incinder is a gothic roguelike dungeon crawler. The game includes procedural generated dungeon layouts, turn-based combat, coin flip combat with patrolling enemies and unique item/abilities rewards. The goal is to locate and reach the chalice to win the game and escape the dungeon before dying.



##### System Requirements

The game ships as a standalone Windows executable and requires no installation. The following minimum specifications are recommended:



* OS: Windows 10 or Windows 11 (64-bit)
* CPU: Dual-core processor, 2.0GHz or higher
* RAM: 4GB
* GPU: Dedicated graphics card with shader model 3.0 support or higher
* Storage: Sufficient free space for the build and Unity runtime dependencies (estimated under 500MB)
* Display: 1920x1080 resolution or higher recommended





##### Dependencies

No additional software installation is required. All runtime dependencies are bundled inside the executable's data folder. Do not move, renamed or delete any files from the fata folder alongside the .exe. The game will fail to launch without them.



The following third-party assets are used within the build:



* CS Rohesia Gothic Font - Desktop License (1 user, up to 5 commercial projects). Licensed under a Desktop License purchased from Creative Supply Co. Do not redistribute the font files.
* Wwise Audio Middleware - AudioKinetic Wwise is embedded for all in game audio. no separate Wwise installation is required to run the build.
* VFX URP - Fire Package Cartoon VFX by Wallcoeur Unity Asset Store — Standard Unity Asset Store EULA https://assetstore.unity.com/packages/vfx/particles/fire-explosions/vfx-urp-fire-package-305098
* Unity Engine 6 (6000.x) runtime libraries, included automatically in the Unity build output.



##### How to Run

1. Extract the submitted zip file to a location of your choice.
2. Open the extracted folder and locate Incinder.exe.
3. Double click the executable to launch the game. Windows Defender SmartScreen may display a warning on first launch. click "More Info" then "Run anyway" to proceed.
4. The game will display a main menu. Press Play to begin a new run with a freshly generated dungeon.



Note: do not run the executable directly from inside the zip archive. Always extract first.



##### Controls



###### Exploration



|**Input**|**Action**|
|-|-|
|Escape|Pause the game|
|Left Click|Move player to clicked location|
|Right Click (Hold)|Rotate Camera|
|W|Move Camera forward|
|A|Move Camera left|
|S|Move Camera right|
|D|Move Camera backward|
|E|Interact (Open chests, activate win condition when in range, open doors)|





###### Combat

Combat triggers automatically when an enemy spots the player or the player enters close proximity. During your turn:



|**Input**|**Action**|
|-|-|
|Left Click (floor)|Move within your available movement range|
|1|Activate Ability Slot 1|
|2|Activate Ability Slot 2|
|3|Activate Ability Slot 3|
|Left Click (After ability)|Confirm ability target|
|Space|Flip coin|
|Tab|End your turn and pass to enemy phase|
|Escape|Cancel active ability targeting|





###### UI



|**Input**|**Action**|
|-|-|
|Hover (ability/ item)|Show tooltip with description and stats|
|Minimap|Reveals rooms the player has visited.|
|Minimap +|Zoom into the current map view|
|Minimap -|Zoom out of the current map view|





##### Gameplay Summary



Exploration: Navigate the procedurally generated dungeon room by toom. Enemies patrol their rooms using vision cones, avoid their line of sigh or engage in combat. Staying in shadow reduces enemy effectiveness.



Combat: Combat is turn-based and uses a win/lose streak mechanic: Too many loses and the chance to win is increased, too many wins and the chance is decreased. Pressing space with an ability chosen allows for a coin flip to increase the attack damage of the attack. Abilities and items found in treasure chests can shift the odds in your favour.



Objectives: Find the Chalice in the final room and press E to escape the dungeon and win. if your health reaches zero, the game ends.



##### Known Issues



* Players can walk into enemy models
* Some torches can't be targeted
* Really far movement clicks don't move the player



##### Credits



###### Programming

Laurence Allen — Gameplay \& Engine/Tools Programmer

Ethan McCaffrey — Engine/Tools \& Gameplay Programmer

&#x20;

###### Art

Emma Gowdy — Artist

Kira Hetesi — Artist

Matthew Van Aardt — Tech Artist



###### Design

Cohen Kelly — Level Designer

Charlie Banks — Narrative Designer

Laurence Allen —  Gameplay Designer

Ethan McCaffrey — Gameplay Designer

Matthew Van Aardt — Gameplay Designer

&#x20;

###### Audio

Adam Kidd — Audio Designer

Laurence Allen — Composer



###### Production

Matthew Van Aardt — Producer

Charlie Banks — Co-Producer



###### Additional Credits

Gareth Robinson — Academic Supervisor

Asset \& Licensed Credits

Audio Middleware: Wwise by Audiokinetic

Font: CS Rohesia by Craft Supply Co.

