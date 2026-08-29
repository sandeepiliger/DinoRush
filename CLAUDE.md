CLAUDE.md — Dino Rush: Extinction Run

1. PROJECT IDENTITY

Project name:

Dino Rush: Extinction Run

Developer:

Iliger Games

Platform:

Android first

Engine:

Unity + C#

Primary development agent:

Claude Code

Secondary engineering/review agent:

OpenAI Codex

Genre:

Hybrid-casual endless runner

Core fantasy:

«The player controls a dinosaur desperately running through a prehistoric world while the environment progressively collapses behind them.»

The game must be:

- Extremely easy to understand.
- Fun within the first 10 seconds.
- Optimized for mobile.
- Visually impressive without requiring AAA hardware.
- Fast to develop.
- Highly replayable.
- Monetizable through rewarded ads, interstitial ads and optional IAP.
- Designed for future live events and content updates.
- Architected so future Iliger Games titles can reuse the underlying systems.

Do NOT attempt to build an enormous AAA game.

The first production version must be a polished, small, highly playable vertical slice that can be expanded later.

---

2. PRIMARY PRODUCT GOAL

The first objective is NOT to build hundreds of features.

The first objective is:

«Build a game that makes a new player immediately understand the gameplay, enjoy the first run, want to restart after dying, and have a reason to return tomorrow.»

The game must prioritize:

1. Game feel
2. Performance
3. Visual identity
4. Fast restart
5. Retention
6. Replayability
7. Monetization
8. Content scalability

Do not sacrifice gameplay quality merely to add more systems.

---

3. CORE GAMEPLAY

The player automatically runs forward.

The initial control scheme:

- Tap = jump
- Swipe down = duck
- Hold = sprint/boost where appropriate

The controls must feel responsive.

Target input latency should be extremely low.

The game must work comfortably with one hand.

The player must be able to understand the controls without reading a large tutorial.

---

4. CORE GAME LOOP

The primary loop is:

START RUN
↓
RUN
↓
AVOID OBSTACLES
↓
COLLECT COINS
↓
SURVIVE LONGER
↓
WORLD BECOMES MORE DIFFICULT
↓
SPECIAL EVENT / ESCALATION
↓
DIE
↓
GAME OVER
↓
OPTION TO WATCH REWARDED AD FOR REVIVE
↓
REWARD / RESULTS
↓
UPGRADE / COLLECTION / MISSIONS
↓
START AGAIN

A complete run should be possible within seconds.

Restart must be extremely fast.

Never create unnecessary loading screens between runs.

---

5. THE UNIQUE GAME HOOK

The game must NOT simply be:

"Dinosaur jumps over cactus."

The defining mechanic is:

EXTINCTION ESCALATION

The environment progressively becomes more dangerous.

Example:

0–30 seconds:

- Jungle
- Small rocks
- Plants
- Fallen branches

30–60 seconds:

- Predators
- Larger obstacles
- Faster terrain

60–90 seconds:

- Earthquakes
- Falling rocks
- Fire

90–120 seconds:

- Volcano erupts
- Lava appears
- Ash clouds

120+ seconds:

- Meteor shower
- Giant predators
- Collapsing terrain

Eventually:

EXTINCTION MODE

The world becomes chaotic.

The player's goal is to survive as long as possible.

This escalation is the game's primary visual and emotional identity.

---

6. WORLD / BIOMES

Initial release must contain three primary biomes.

BIOME 1 — PREHISTORIC JUNGLE

Environment:

- Dense vegetation
- Ferns
- Trees
- Rocks
- Ancient plants
- Mist
- Mountains in background

Obstacles:

- Fallen trees
- Rocks
- Tree roots
- Small predators
- Flying creatures

Lighting:

Warm natural daylight.

---

BIOME 2 — DESERT

Environment:

- Sand
- Dry vegetation
- Large rocks
- Canyon structures
- Ancient fossils

Obstacles:

- Boulders
- Sand pits
- Falling rocks
- Predator encounters

Lighting:

Strong warm sunlight.

---

BIOME 3 — VOLCANIC LAND

Environment:

- Volcano
- Lava
- Smoke
- Ash
- Burned vegetation
- Cracked ground

Obstacles:

- Lava
- Falling rocks
- Fire
- Collapsing ground
- Meteor fragments

Lighting:

Dramatic but mobile-friendly.

---

7. FUTURE BIOMES

Architect the system so these can be added without rewriting the game:

- Ice Age
- Swamp
- Ocean Shore
- Ancient Canyon
- Meteor Apocalypse
- Night Jungle
- Crystal Cave
- Frozen Volcano
- Alien Dinosaur World

Do NOT implement all of these in the first release.

Create interfaces/data structures that make future biomes easy to add.

---

8. PLAYER DINOSAURS

Initial release:

Starter

- Velociraptor

Unlockable:

- T-Rex
- Triceratops
- Stegosaurus
- Spinosaurus
- Ankylosaurus

Future:

- Carnotaurus
- Brachiosaurus
- Parasaurolophus
- Allosaurus
- Dilophosaurus
- Iguanodon
- Pachycephalosaurus

Do not claim scientific accuracy in marketing.

Use visually believable prehistoric designs.

---

9. ART DIRECTION

Target:

STYLIZED REALISM

Do NOT use:

- Cheap cartoon dinosaurs
- Flat generic Unity assets
- Extremely low-quality AI-looking characters
- Excessively realistic movie-quality assets that destroy mobile performance

The target is:

«Realistic anatomy + believable materials + slightly stylized proportions + strong silhouette + excellent animation.»

The dinosaur should look impressive in screenshots while remaining performant on mid-range Android devices.

---

10. DINOSAUR ASSET PIPELINE

The project must support an AI-assisted asset pipeline.

Desired pipeline:

Concept
↓
3D generation / modeling
↓
Blender processing
↓
Retopology
↓
UV
↓
Materials
↓
Rig
↓
Animations
↓
LOD generation
↓
Texture compression
↓
FBX/GLB export
↓
Unity import
↓
Prefab generation
↓
Testing

Claude Code should automate as much of this pipeline as technically possible.

If Blender is installed, use Blender Python scripts wherever practical.

Do not manually perform repetitive asset operations if they can be automated.

---

11. ASSET QUALITY REQUIREMENTS

Every dinosaur must have:

- Clean silhouette
- Correct basic anatomy
- Appropriate scale
- UV mapping
- PBR-compatible materials
- Rig
- Idle animation
- Walk animation
- Run animation
- Sprint animation
- Jump animation
- Attack animation
- Hit animation
- Death animation
- Roar animation

Optional:

- Eat animation
- Celebrate animation
- Special ability animation

---

12. MOBILE OPTIMIZATION FOR DINOSAURS

Every dinosaur must support multiple LODs.

Target:

LOD0:
High quality close-up

LOD1:
Medium quality gameplay

LOD2:
Low quality distant gameplay

Use:

- Optimized meshes
- Compressed textures
- Efficient materials
- Limited shader complexity
- Minimal transparency
- Baked lighting where appropriate

Avoid unnecessary 4K textures on assets that are never seen close-up.

Default to 1K or 2K textures where visually appropriate.

---

13. ANIMATION QUALITY

Animation is more important than polygon count.

Prioritize:

- Natural foot movement
- Proper weight
- Tail movement
- Head movement
- Body compression
- Jump anticipation
- Landing reaction
- Death animation

Running animation must look believable at game speed.

Use animation blending where appropriate.

Do not allow visible foot sliding.

---

14. PLAYER ABILITIES

Initial release:

Normal Jump

Basic jump.

Duck

Avoid flying obstacles.

Sprint

Temporary speed increase.

Future abilities:

- Shield
- Time Slow
- Double Jump
- Magnet
- Invulnerability
- Roar
- Ground Smash

Do not implement all abilities initially.

---

15. PROCEDURAL LEVEL GENERATION

The runner must not use one fixed track.

Create a procedural segment system.

Example:

Segment A:
safe

Segment B:
small obstacle

Segment C:
jump challenge

Segment D:
coin pattern

Segment E:
enemy

Segment F:
mixed obstacle

Segment G:
high difficulty

The generator must:

- Avoid impossible combinations.
- Maintain fair reaction times.
- Prevent unavoidable deaths.
- Scale difficulty gradually.
- Allow deterministic seeds for debugging.

Every generated segment must be validated before spawning.

---

16. DIFFICULTY SYSTEM

Difficulty increases based on:

- Distance
- Time
- Score
- Current biome
- Player progression

Difficulty should influence:

- Runner speed
- Obstacle frequency
- Obstacle combinations
- Reaction time
- Enemy frequency
- Environmental hazards

Never make difficulty increase so quickly that new players immediately quit.

Create a centralized DifficultyConfig.

Never hardcode difficulty values throughout unrelated scripts.

---

17. COINS

Coins are the primary soft currency.

Players earn coins through:

- Distance
- Coin pickups
- Missions
- Daily rewards
- Ads
- Events

Coins may be spent on:

- Dinosaur skins
- Cosmetic upgrades
- Temporary boosts

Avoid pay-to-win mechanics in the first version.

---

18. DINOSAUR COLLECTION

Create a collection screen.

Each dinosaur has:

- Name
- Rarity
- Description
- Unlock condition
- Preview
- Selected state

Rarity:

- Common
- Rare
- Epic
- Legendary

Do not make rarity purely cosmetic in code.

Use ScriptableObjects/data assets for dinosaur definitions.

---

19. MISSIONS

Create daily and lifetime missions.

Examples:

- Run 500 meters.
- Run 2,000 meters.
- Collect 100 coins.
- Jump over 20 obstacles.
- Play 3 runs.
- Survive for 60 seconds.
- Use a power-up.
- Watch one rewarded ad.

Mission architecture must be data-driven.

Adding a new mission should not require modifying core gameplay code.

---

20. DAILY REWARD

Implement a 7-day reward cycle.

Example:

Day 1:
Coins

Day 2:
Coins

Day 3:
Rare reward

Day 4:
Coins

Day 5:
Gems/rare currency

Day 6:
Skin fragment

Day 7:
Premium reward

After day 7:

Loop or start a new reward cycle.

Prevent obvious abuse through local timestamp manipulation where practical.

Do not require an account for the first version.

---

21. DAILY CHALLENGE

One challenge per day.

Examples:

- Survive 90 seconds.
- Collect exactly 50 coins.
- Avoid 30 obstacles.
- Reach a target distance.
- Complete the run without power-ups.

Daily challenge must be deterministic for all players where practical.

This enables future leaderboards.

---

22. LEADERBOARD

Initial release:

Local best score.

If backend integration is introduced:

Global leaderboard.

Do not make online multiplayer part of V1.

Architect the scoring system so a secure server-backed leaderboard can be added later.

Never trust client-side scores in a competitive global leaderboard.

---

23. MONETIZATION

Use a hybrid model.

REWARDED ADS

Primary rewarded placements:

1. Revive after death
2. Double coins
3. Bonus reward
4. Daily reward multiplier
5. Optional boost

Rewards must be clearly communicated before the user chooses to watch.

Do not force a rewarded ad.

Use Google Mobile Ads Unity integration.

During development:

USE TEST ADS ONLY.

Never ship test ad IDs in production.

Initialize the ad SDK once at app startup.

Preload rewarded ads appropriately.

Handle failed loads gracefully.

Do not crash or block gameplay if ads fail.

---

24. INTERSTITIAL ADS

Use sparingly.

Possible placement:

After several completed runs.

Never show an interstitial:

- Immediately on app launch
- During active gameplay
- Immediately after the user watches a rewarded ad
- In a way that interrupts a critical interaction

Use frequency caps.

The ad system must be configurable without changing gameplay code.

---

25. AD REVENUE ANALYTICS

Track:

- Ad request
- Ad loaded
- Ad failed
- Ad shown
- Ad closed
- Reward earned
- Impression revenue where available

Build an AdManager abstraction.

Gameplay code must NOT directly call Google Mobile Ads APIs.

Example:

Gameplay:

"AdManager.ShowRewarded(RewardType.Revive)"

Not:

"RewardedAd.Show(...)"

This keeps monetization replaceable.

---

26. IN-APP PURCHASES

Initial products:

REMOVE_ADS

One-time purchase.

STARTER_PACK

Optional.

COIN_PACK_SMALL

Optional.

COIN_PACK_MEDIUM

Optional.

PREMIUM_DINO

Optional.

Do not build a huge shop initially.

Use Google Play Billing.

Purchase flow must:

1. Display product
2. Start purchase
3. Process result
4. Verify appropriately
5. Grant content
6. Acknowledge/consume as appropriate
7. Recover gracefully after interruption

Never grant premium purchases multiple times because of duplicate callbacks.

Use stable product IDs.

Keep product IDs in configuration.

---

27. MONETIZATION DESIGN PRINCIPLE

The player must be able to enjoy the complete game without paying.

Monetization should feel like:

"Would you like this?"

Not:

"You cannot play unless you pay."

Never use manipulative purchase mechanics.

---

28. ANALYTICS

Create an AnalyticsManager abstraction.

Events to track:

Acquisition

- first_open
- tutorial_started
- tutorial_completed

Gameplay

- run_started
- run_completed
- player_died
- distance_reached
- obstacle_hit
- biome_entered

Progression

- dinosaur_unlocked
- dinosaur_selected
- mission_completed
- daily_reward_claimed

Monetization

- rewarded_ad_offered
- rewarded_ad_completed
- interstitial_shown
- purchase_started
- purchase_completed

Retention

- session_started
- session_ended
- day_return

Never put analytics SDK-specific calls throughout the game.

Use:

"AnalyticsManager.TrackEvent(...)"

---

29. SAVE SYSTEM

Create a robust SaveManager.

Save:

- Selected dinosaur
- Unlocked dinosaurs
- Coins
- Missions
- Daily reward state
- Best score
- Settings
- Tutorial state
- Purchase state

Use versioned save data.

Example:

"saveVersion = 1"

Future migrations must be possible.

Do not blindly deserialize old save files without validation.

Handle corrupted save data gracefully.

---

30. GAME STATES

Create centralized game states.

Minimum:

BOOT
MENU
TUTORIAL
READY
PLAYING
PAUSED
REVIVE
GAME_OVER
SHOP
COLLECTION
MISSIONS
SETTINGS

Do not allow random scripts to directly control global game state.

---

31. SCENE ARCHITECTURE

Prefer a small number of scenes.

Suggested:

Bootstrap
MainMenu
Game

UI should be instantiated or managed cleanly.

Avoid dozens of scenes unless justified.

---

32. CODE ARCHITECTURE

Use clean modular C#.

Suggested folders:

Assets/
Game/
Art/
Audio/
Animations/
Materials/
Prefabs/
Scenes/
ScriptableObjects/
UI/
Resources/

Scripts/
Core/
Player/
World/
Obstacles/
Gameplay/
Progression/
Economy/
Missions/
Daily/
Collection/
Ads/
IAP/
Analytics/
Save/
UI/
Audio/
Utilities/

Editor/
AssetTools/
BuildTools/
Validation/

---

33. CORE SYSTEMS

Create these systems as independent modules:

GameManager
GameStateManager
PlayerController
PlayerAnimationController
RunManager
ObstacleSpawner
ProceduralSegmentGenerator
DifficultyManager
BiomeManager
CoinManager
ScoreManager
MissionManager
DailyRewardManager
CollectionManager
SaveManager
AudioManager
UIManager
AdManager
IAPManager
AnalyticsManager
SettingsManager
PoolManager

Do not create one giant GameManager containing the entire game.

---

34. OBJECT POOLING

Use object pooling for:

- Obstacles
- Coins
- Particles
- Environmental props
- Enemies
- Segments

Do not instantiate/destroy hundreds of objects continuously during gameplay.

Avoid unnecessary garbage collection.

---

35. PERFORMANCE TARGETS

Primary target:

60 FPS on modern mid-range Android.

Secondary acceptable target:

30 FPS on lower-end supported devices.

Avoid:

- Per-frame allocations
- Excessive LINQ in Update
- Expensive physics
- Excessive real-time lights
- Huge particle systems
- Unnecessary post-processing
- High-poly distant objects
- Uncompressed textures

Profile before optimizing blindly.

---

36. GRAPHICS

Use a mobile-friendly rendering setup.

Prefer:

- URP/mobile-friendly configuration
- Baked lighting where appropriate
- Limited dynamic lights
- LOD
- Occlusion where useful
- Texture compression
- Simple shaders
- Efficient particles

Graphics quality should be configurable.

Create:

LOW
MEDIUM
HIGH

where technically appropriate.

Default to a sensible quality level based on device performance.

---

37. AUDIO

Implement:

- Footsteps
- Jump
- Land
- Coin pickup
- Obstacle hit
- Death
- Roar
- UI click
- Reward
- Achievement

Background music:

- Jungle
- Desert
- Volcano
- Extinction mode

Audio must have:

Music volume
SFX volume
Mute option

---

38. CAMERA

Third-person side-running camera.

Camera must:

- Keep dinosaur readable.
- Show upcoming obstacles.
- Avoid excessive shake.
- Use controlled camera effects.
- Support biome transitions.

Camera shake must be subtle and configurable.

---

39. UI

Required screens:

Main Menu

Show:

- Play
- Selected dinosaur
- Coins
- Collection
- Missions
- Daily reward
- Settings

Game HUD

Show:

- Score
- Distance
- Coins
- Pause

Do not clutter the screen.

Game Over

Show:

- Score
- Best score
- Distance
- Coins earned
- Revive button
- Double coins button
- Retry

Collection

Show all dinosaurs.

Missions

Show active missions and progress.

Daily Reward

Show reward cycle.

Shop

Show purchasable content.

---

40. TUTORIAL

Tutorial must be extremely short.

Teach:

1. Tap to jump.
2. Swipe down to duck.
3. Avoid obstacles.
4. Collect coins.

The player should be playing within seconds.

Do not create a 5-minute tutorial.

---

41. ONBOARDING

First launch:

Splash
→
Game introduction
→
One short interactive tutorial
→
First run
→
Reward
→
Main menu

Do not request unnecessary permissions.

---

42. LOCALIZATION

Architecture must support localization.

Initial language:

English.

Prepare localization keys rather than hardcoding UI text.

Future:

Hindi
Spanish
Portuguese
German
French
Japanese
Korean
Indonesian

---

43. PRIVACY / PERMISSIONS

Request only necessary permissions.

Do not collect unnecessary personal data.

Do not hardcode secrets into the repository.

Any API keys, ad IDs, signing credentials or production secrets must be supplied through secure configuration.

Never commit:

- Keystores
- Passwords
- API secrets
- Service-account private keys
- Production credentials

---

44. ANDROID CONFIGURATION

Target Android according to current Google Play requirements at release time.

Use Android App Bundle (AAB) for Play Store distribution.

Configure:

- Application ID
- Version
- Version code
- App label
- Launcher icon
- Splash screen
- ARM64
- Appropriate min/target SDK
- IL2CPP where appropriate
- Managed stripping carefully
- Release signing

Never commit the production keystore.

The final build must be reproducible.

---

45. BUILD AUTOMATION

Create editor scripts for:

Build Android Development
Build Android Release
Build AAB
Validate Project
Validate Assets
Validate Scenes
Validate Product IDs

Example editor commands:

"Iliger > Build > Android Development"

"Iliger > Build > Android Release"

"Iliger > Tools > Validate Project"

---

46. AUTOMATED VALIDATION

Before considering a milestone complete, run:

- Compilation
- Unit tests
- Asset validation
- Scene validation
- Missing-reference detection
- Build validation
- Android build

Fix errors rather than ignoring them.

Do not hide compiler warnings or errors simply to make builds pass.

---

47. TESTING

Create automated tests for:

- Score calculation
- Coin calculation
- Difficulty scaling
- Mission progression
- Daily reward logic
- Save/load
- Dinosaur unlocks
- Purchase state
- Reward logic
- Procedural generation validation

Create gameplay test utilities.

---

48. PROCEDURAL GENERATION SAFETY

The generator must guarantee:

- No impossible jump.
- No unavoidable obstacle.
- No obstacle spawning inside player.
- No broken segment transitions.
- Minimum reaction time.
- Valid coin paths.
- Valid difficulty progression.

Create automated procedural-generation tests.

Generate thousands of segments in editor tests and verify validity.

---

49. DEBUG TOOLS

Create a development debug panel.

Functions:

- Add coins
- Unlock all dinosaurs
- Reset save
- Set biome
- Set difficulty
- Set score
- Spawn obstacle
- Spawn power-up
- Complete mission
- Trigger extinction mode
- Simulate ad reward
- Simulate purchase

Debug tools must only be available in development builds.

Never expose them in production.

---

50. CONTENT DATA

Use ScriptableObjects for:

DinosaurData
BiomeData
ObstacleData
MissionData
RewardData
ShopProductData
DailyRewardData
DifficultyData
SegmentData

Avoid hardcoding content inside C# classes.

The goal is:

«Adding a new dinosaur should mostly require creating a data asset rather than rewriting code.»

---

51. FUTURE LIVE OPS

Architecture must support:

- Limited-time events
- Seasonal skins
- Daily challenges
- Weekly challenges
- New dinosaurs
- New biomes
- New obstacles
- New missions
- Remote configuration

Do not implement a complete live-ops backend in V1.

Prepare the architecture only.

---

52. REMOTE CONFIG

Create an abstraction:

"GameConfigProvider"

Initial implementation:

Local config.

Future implementation:

Remote configuration.

Configurable values should include:

- Starting speed
- Difficulty curve
- Coin rewards
- Mission rewards
- Ad frequency
- Revive availability
- Shop pricing display
- Event availability

Never hardcode balancing values throughout scripts.

---

53. APP STARTUP

Startup sequence:

Bootstrap
→
Initialize Save
→
Initialize Analytics
→
Initialize Ads
→
Initialize Audio
→
Initialize Game Services
→
Load Main Menu

Failures in optional services must not prevent the game from starting.

---

54. OFFLINE-FIRST GAMEPLAY

Core gameplay must work without network access.

The player should be able to:

- Start a run
- Play
- Earn local coins
- Complete local missions
- View collection
- View scores

Online services should be optional.

Ads naturally require connectivity.

Online leaderboards can require connectivity.

---

55. ERROR HANDLING

Never allow external services to crash gameplay.

Examples:

Ad unavailable:
Continue normally.

Analytics unavailable:
Continue normally.

Purchase unavailable:
Show clear message.

Save corrupted:
Attempt recovery.

Network unavailable:
Use offline mode.

---

56. SECURITY

Never trust client-side values for competitive rewards.

Do not store sensitive credentials in PlayerPrefs.

Do not expose production secrets.

Do not implement fake purchases.

Do not circumvent Google Play Billing.

Do not use unauthorized advertising SDK behavior.

---

57. INTELLECTUAL PROPERTY

Do NOT copy:

- Chrome Dino assets
- Jurassic Park assets
- Disney assets
- Existing game characters
- Copyrighted music
- Trademarked logos
- Proprietary models
- Other developers' artwork

The game must have an original visual identity.

Dinosaur species names may be used as scientific/common names, but individual character designs must be original.

Only use third-party assets when their license permits commercial use.

Keep a LICENSES/THIRD_PARTY_ASSETS.md file.

---

58. STORE IDENTITY

Working title:

Dino Rush: Extinction Run

Possible tagline:

Run. Survive. Escape Extinction.

Potential icon:

A dramatic dinosaur head facing forward with a collapsing volcanic world behind it.

Do not copy the Chrome Dino icon.

---

59. GOOGLE PLAY STORE ASSETS

Prepare:

- App icon
- Feature graphic
- Screenshots
- Promotional artwork
- Short description
- Full description

Do not generate misleading screenshots.

Gameplay screenshots must represent the actual product.

---

60. PERFORMANCE BUDGET

Establish budgets.

Example initial targets:

- APK/AAB size: keep as small as reasonably possible.
- Memory: avoid excessive runtime allocations.
- Draw calls: minimize.
- Texture memory: controlled.
- FPS: 60 target.
- Startup: fast.
- Scene transition: fast.
- Restart run: near-instant.

Measure actual performance.

Do not claim performance without profiling.

---

61. FIRST DEVELOPMENT MILESTONE

Do NOT build monetization first.

Build:

Milestone 1

- Unity project
- Player dinosaur
- Running
- Jump
- Duck
- Camera
- One obstacle
- Collision
- Score
- Death
- Restart

Acceptance criteria:

A player can launch the game, start running, jump over obstacles, die, and restart.

---

62. SECOND MILESTONE

Add:

- Procedural segments
- Coins
- Increasing difficulty
- Better camera
- Basic environment
- Basic audio
- Object pooling

Acceptance:

The player can play repeatedly without obvious procedural bugs.

---

63. THIRD MILESTONE

Add:

- Jungle
- Desert
- Volcano
- Biome transitions
- Extinction escalation
- Better visual effects

Acceptance:

The game looks like a real commercial mobile game rather than a prototype.

---

64. FOURTH MILESTONE

Add:

- Dinosaur collection
- Unlock system
- Missions
- Daily rewards
- Local best score
- Shop UI

---

65. FIFTH MILESTONE

Add:

- Rewarded ads
- Interstitial ads
- Remove Ads
- Starter pack
- Basic IAP

Use test advertisements during development.

---

66. SIXTH MILESTONE

Add:

- Analytics
- Crash monitoring integration if selected
- Build automation
- Device testing
- Performance profiling
- Save migration
- Localization foundation

---

67. SEVENTH MILESTONE

Production polish:

- Animation polish
- Sound polish
- UI polish
- Particle effects
- Camera effects
- Haptics
- Loading optimization
- Android optimization

---

68. EIGHTH MILESTONE

Release candidate:

- Production package ID
- Production signing configuration
- Production ad IDs
- Production IAP products
- Store assets
- Privacy policy integration
- Play Console configuration
- AAB build
- Internal testing
- Closed testing

Never use production ad IDs during development.

---

69. CLAUDE CODE OPERATING RULES

Claude Code must:

1. Read this entire CLAUDE.md before making changes.
2. Inspect the existing project before modifying it.
3. Never assume files exist.
4. Never overwrite working code unnecessarily.
5. Prefer small, testable changes.
6. Build after significant changes.
7. Run relevant tests.
8. Fix compiler errors immediately.
9. Avoid creating duplicate systems.
10. Reuse existing abstractions.
11. Keep systems modular.
12. Document non-obvious decisions.
13. Never introduce unnecessary dependencies.
14. Never hardcode secrets.
15. Never use production ad IDs in development.
16. Never fabricate successful test results.
17. Never say something works without actually validating it.
18. Keep the project buildable after every milestone.

---

70. WHEN SOMETHING IS MISSING

If a required external asset/tool/service is unavailable:

DO NOT silently invent a fake implementation that will later break.

Instead:

1. Create a clean abstraction.
2. Create a temporary placeholder.
3. Document exactly what is missing.
4. Continue developing independent systems.

Example:

If Google Mobile Ads is not installed:

Create:

"IAdProvider"

and:

"MockAdProvider"

so gameplay can continue during development.

Later connect the real provider.

---

71. ASSET AUTOMATION RULE

When an asset can be generated procedurally or automatically, prefer automation.

Examples:

- Blender Python
- Unity Editor scripts
- Texture processing scripts
- Batch import settings
- LOD generation
- Prefab generation
- Material assignment
- Asset validation

Do not manually repeat the same operation 20 times.

---

72. PLACEHOLDER POLICY

During early development:

Use simple placeholders.

Do NOT spend hours generating final art before gameplay is proven.

The order should be:

Gameplay prototype
→
Fun validation
→
Art replacement
→
Polish
→
Optimization

---

73. FINAL ART REPLACEMENT

Once gameplay is stable:

Replace placeholders with production assets.

Every production asset must pass:

- Visual quality
- Polygon budget
- Texture budget
- LOD check
- Material check
- Animation check
- Mobile performance check
- License check

---

74. DEVELOPMENT ORDER

Follow this exact priority:

PHASE A
Core movement

PHASE B
Obstacles

PHASE C
Procedural generation

PHASE D
Difficulty

PHASE E
Biome system

PHASE F
Extinction escalation

PHASE G
UI

PHASE H
Progression

PHASE I
Collection

PHASE J
Missions

PHASE K
Daily rewards

PHASE L
Ads

PHASE M
IAP

PHASE N
Analytics

PHASE O
Optimization

PHASE P
Testing

PHASE Q
Play Store release

Do not jump ahead unnecessarily.

---

75. MVP DEFINITION

The first publishable MVP must contain:

Gameplay

- Running
- Jumping
- Ducking
- Obstacles
- Coins
- Score
- Difficulty
- Death
- Restart

Content

- 1 dinosaur
- 3 biomes
- 10+ obstacle types
- 50 missions
- Daily challenge
- Daily reward

Monetization

- Rewarded revive
- Rewarded coin multiplier
- Interstitial ads with sensible frequency
- Remove Ads IAP

Systems

- Save
- Analytics
- Settings
- Audio
- Object pooling
- Performance optimization

Platform

- Android
- AAB
- Release configuration
- Play Store assets

---

76. WHAT NOT TO BUILD IN MVP

Do NOT initially build:

- Multiplayer
- Real-time PvP
- Guilds
- Chat
- Complex backend
- 100 dinosaurs
- 20 biomes
- Battle pass
- Complex crafting
- Huge inventory
- Procedural 3D world generation
- Account registration
- Social network
- PvP matchmaking

These can come later if metrics justify them.

---

77. SUCCESS METRICS

After launch, evaluate:

Acquisition

- Store conversion rate
- Install volume

Engagement

- D1 retention
- D7 retention
- D30 retention
- Sessions/user
- Session length

Gameplay

- Average run duration
- Average distance
- Retry rate
- Tutorial completion

Monetization

- Rewarded ad opt-in
- Ads/user/day
- ARPDAU
- IAP conversion
- Revenue/user

Quality

- Crash-free users
- ANR rate
- FPS
- Memory
- Battery consumption

Do not optimize for downloads alone.

---

78. DECISION FRAMEWORK AFTER LAUNCH

If retention is poor:

Improve gameplay.

If retention is good but monetization is poor:

Improve monetization.

If monetization is good but acquisition is poor:

Improve store listing and marketing.

If retention and monetization are both good:

Invest in more content.

Do not automatically add features when metrics are poor.

Find the actual problem first.

---

79. FUTURE REUSABLE ILIGER GAMES FRAMEWORK

The following systems should eventually be extracted into reusable packages:

- Save system
- Analytics abstraction
- Ads abstraction
- IAP abstraction
- Audio manager
- UI framework
- Object pooling
- Mission system
- Daily rewards
- Collection system
- Procedural generation framework
- Build automation
- Device diagnostics
- Remote configuration

The long-term goal is:

«Build the first game once, then reuse the infrastructure for future Iliger Games titles.»

---

80. CLAUDE CODE WORKFLOW

At the beginning of every task:

STEP 1

Read CLAUDE.md.

STEP 2

Inspect project structure.

STEP 3

Identify existing relevant systems.

STEP 4

Explain the implementation plan briefly.

STEP 5

Implement the smallest complete change.

STEP 6

Compile.

STEP 7

Run tests.

STEP 8

Fix errors.

STEP 9

Inspect the resulting project.

STEP 10

Report:

- What changed
- Files changed
- Tests run
- Build status
- Remaining issues

Do not claim completion if validation failed.

---

81. CODEx REVIEW WORKFLOW

When Codex is used as a secondary agent, ask it to:

- Review architecture.
- Find bugs.
- Find performance problems.
- Find memory leaks.
- Review Android compatibility.
- Review monetization integration.
- Review save logic.
- Review procedural generation.
- Review security.
- Review IAP handling.
- Review ads handling.
- Review tests.

Codex should fix problems where safe.

Do not allow two agents to simultaneously make conflicting architectural changes.

---

82. DEFINITION OF DONE

A feature is NOT complete merely because code exists.

A feature is complete when:

- Code compiles.
- No obvious runtime errors exist.
- Relevant tests pass.
- UI works.
- Mobile input works.
- Save state works if applicable.
- Performance is acceptable.
- Edge cases are handled.
- Documentation is updated where necessary.

---

83. FINAL RELEASE CHECKLIST

Before production release:

[ ] Clean project build

[ ] No compiler errors

[ ] No major warnings

[ ] No missing references

[ ] No placeholder assets

[ ] No test ad IDs

[ ] No development purchase configuration

[ ] Production application ID

[ ] Production signing

[ ] AAB generated

[ ] Android device tested

[ ] Low-end device tested

[ ] Mid-range device tested

[ ] High-end device tested

[ ] Offline mode tested

[ ] Ad failure tested

[ ] Purchase failure tested

[ ] Save corruption tested

[ ] App resume tested

[ ] App background/foreground tested

[ ] Rotation behavior verified

[ ] Audio settings verified

[ ] Analytics verified

[ ] Privacy requirements reviewed

[ ] Store screenshots prepared

[ ] Store description prepared

[ ] App icon prepared

[ ] Feature graphic prepared

[ ] Closed testing build uploaded

---

84. MOST IMPORTANT INSTRUCTION

Do not optimize this project for:

"How much code can we generate?"

Optimize it for:

«"How quickly can we produce a genuinely fun, polished Android game that real players want to play again?"»

Prefer:

10 excellent systems

over:

100 mediocre systems.

Prefer:

one excellent dinosaur

over:

20 unfinished dinosaurs.

Prefer:

one excellent biome

over:

10 empty biomes.

Prefer:

a 30-second addictive run

over:

a giant feature list.

The player experience is the product.

---

85. FIRST COMMAND

When Claude Code starts working on this project, do NOT immediately generate the entire game.

First:

1. Inspect the Unity project.
2. Verify Unity/Android tooling.
3. Create the project architecture.
4. Create the core bootstrap.
5. Create the player controller.
6. Create the first playable run.
7. Build and test Android.
8. Only then proceed to the next milestone.

At every stage, maintain a buildable project.

START WITH THE SMALLEST PLAYABLE VERSION.

Then iterate toward the complete Dino Rush: Extinction Run product.
