# Exfil Rewards

An SPT mod that gives you money for successfully getting out of a raid.

Works for both PMC and Scav raids. Rewards are based on the raid result, kills, and survival time.

## Features

- PMC rewards
- Scav rewards
- Kill rewards
- Survival time bonus
- Run-through rewards
- Post-raid reward popup

## What happens after a successful raid?

After every successful extraction (**Survived** or **Run Through**), a popup will show you the **total bonus money earned from that raid**.

Press **Confirm**, then head back to the main menu.

Your bonus rewards will be delivered through **in-game mail**.

## Rewards

### PMC

- Base extraction: **₽100,000 / $800 / €600**
- Per kill: **₽25,000 / $200 / €150**

### Scav

- Base extraction: **₽55,000 / $400 / €275**
- Per kill: **₽12,000 / $150 / €100**

### Survival bonus

You get an additional **0.5% of the base extraction reward for every minute survived**.

### Run-through

A run-through gives **50% of the normal base reward**.

Kill rewards and the survival bonus still apply.

## No reward for

Rewards are only given for:

- Survived
- Run Through

No reward is given for:

- Killed
- Left
- Missing in Action
- Transit

## Installation

1. Download `ExfilRewards_1.0.zip` from the **Releases** page.
2. Extract the ZIP.
3. You should see these two folders:

```text
BepInEx
SPT_Runtime
```

4. Drag and drop **both the folders** directly into your main SPT game directory.

This is the folder where `EscapeFromTarkov.exe` is located.

**Don't open the `BepInEx` or `SPT_Runtime` folders before copying them.** Just drop both folders into the main game directory.

After installing, the files should be located at:

### Client
----------------------
```text
BepInEx/
└── plugins/
    └── ExfilRewardsClient/
        └── ExfilRewards.Client.dll
```
----------------------

### Server
----------------------
```text
SPT_Runtime/
└── user/
    └── mods/
        └── ExfilRewards/
            └── ExfilRewards.Server.dll
```
----------------------

5. Start SPT normally.

That's it.

## Requirements

- **SPT v4.1x**

This mod supports SPT 4.1x.

## Building

The source code for both the server and client is included in this repository.

You'll need the required SPT, BepInEx and Unity assemblies to build the projects.

Build:

- `ExfilRewards.Server`
- `ExfilRewards.Client`

The projects include packaging targets for creating the required mod folders.

## Source

The source code is included in this repository.

The `refs` folders are build dependencies and are not included in the release ZIP.

## Issues

If something isn't working, open an issue and include:

- SPT version
- Exfil Rewards version
- What happened
- Relevant logs if possible

## License

**All rights reserved.**

Copyright © 2026 **froze (V-E-I-N)**

You may download and use this mod for personal use.

Redistribution, re-uploading, mirroring, repackaging, modifying and redistributing, or including this mod in another modpack or collection is **not allowed without permission from froze (V-E-I-N)**.

For permission to redistribute or use this mod in another project, contact **froze (V-E-I-N)**.

---

Made by **froze (V-E-I-N)**
