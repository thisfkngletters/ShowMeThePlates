# ShowMeThePlates

**See the condition of your armor plates, helmet inserts, ballistic masks and leg armor at a glance, in raid and in your inventory, without opening a single menu.**

ShowMeThePlates is a client-side BepInEx plugin for SPT 4.1. It adds small, clean panels to your screen that show the durability of every plate in your vest, every insert in your helmet, your ballistic mask and your leg armor (with [Manimal's Leg Armor]). It also writes the same information on top of the item icons in your inventory, stash and loot, and warns you the moment a plate breaks.

> Requires SPT 4.1.x. Client-only: nothing to install on the server.

---

## Features

### 1. In-raid panel
- Four independent panels: **Vests / Rigs**, **Helmets**, **Facecovers** (ballistic masks) and **Leg Armor**. Each one can be turned on or off, moved, resized and mirrored on its own.
- Shows the **vest, helmet, mask and leg armor** silhouettes, filled up according to how much durability they have left.
- Lists **each plate** (Front, Rear, Left side, Right side) and **each helmet insert** (Top, Nape, Eyes, Jaw, Ears) with its durability.
- Vests / Rigs: choose what to list with three switches: **Show plates**, **Show inserts (vests WITHOUT plate slots)** and **Show inserts (ALL vests)**. Soft inserts are chest, back, sides, neck, groin, shoulders and forearms, with the game's own icons.
- **Ballistic masks** (Facecover slot, e.g. the CQCM ballistic mask): the mask's own durability, plus each armored insert if the mask has any.
- **Leg armor** from [Manimal's Leg Armor](https://github.com/danauraborealis/Manimal-s-Leg-Armor): the leg armor total plus each insert (L. Thigh, R. Thigh, L. Calf, R. Calf). Nothing to set up: if the mod is not installed, the panel simply stays empty.
- Wearing **two armored vests** (e.g. an armored rig and a body armor, with mods that allow it): all plates and inserts are listed under one vest, and the ones from the second vest show its short name, e.g. `F. Plate - 53/53 (APC)`.
- **Auto Hide mode** (default): the panel stays out of your way and appears only when it matters:
  - when a plate or insert **loses durability** (you got hit),
  - when you **hold the show key** (R by default) for half a second,
  - while any component is below your **low durability threshold** (optional).
- Plates and inserts that just took damage **flash soft red** for a few seconds, so you can tell which one was hit.
- The panel **hides itself automatically in menus** and while the mouse cursor is free (inventory, pause, etc.).

### 2. Break alert
- When a plate or insert reaches **0 durability** you get a red T*****-style notification and a **sound**, so you know you are no longer protected there even in the middle of a fight.
- The alert can also be shown as blinking text under the panel (**Alert type**).

### 3. Inventory overlay
- Every vest, helmet, leg armor (and mask with inserts) icon in your inventory, stash and loot shows a **small list of its plates / inserts** with their durability, color coded, e.g. `F 56`, `R 60`, `LS 40`.
- Vests without plate slots (soft armor only) show their **soft inserts** instead, and plate carriers with empty slots show **No Plates**.
- The **armor class badge** (the game's own III, IV, V, VI icons) appears next to each line. **Ballistic masks** show only their class badge (the game already shows their durability).
- Hover an item and the **tooltip lists every plate and insert** with its class and durability.
- Handy when you are comparing carriers at the flea market or deciding which plates to put in which vest.

### 4. Everything is configurable
Press **F12** and look for **ShowMeThePlates**. You can move the panel (or mirror it for the right side of the screen), resize it, change the text size, turn the glow and the damage flash on or off, pick when it appears, and decide what the inventory overlay shows. Every option has a description.

---

## Installation
1. Download the latest release.
2. Extract it into your SPT folder, so that you end up with:

```
BepInEx/plugins/ShowMeThePlates/ShowMeThePlates.dll
BepInEx/plugins/ShowMeThePlates/Broken_Plate_Sound.wav
```

3. Start the game. Press **F12** to change settings.

To uninstall, delete the `ShowMeThePlates` folder.

---

## How to read it

### Plate letters
| Text | Meaning |
|------|---------|
| **F** | Front plate |
| **R** | Rear (back) plate |
| **LS** | Left side plate |
| **RS** | Right side plate |

### Helmet letters
| Text | Meaning |
|------|---------|
| **T** | Top |
| **N** | Nape |
| **E** | Eyes |
| **J** | Jaw |
| **Ea** | Ears |
| **G** | **Gear**: any armored extra piece attached to the helmet, like the faceshield of the Altyn or the visor of other helmets. |

### Soft insert letters (inventory icon, vests without plate slots)
| Text | Meaning |
|------|---------|
| **Cs** | Chest soft insert |
| **Bs** | Back soft insert |
| **LS / RS** | Left / right side soft insert |
| **Ns** | Neck soft insert |
| **Gs** | Groin soft insert |
| **GRs** | Groin rear soft insert |
| **LSh / RSh** | Left / right shoulder |
| **LF / RF** | Left / right forearm |
| **LT / RT** | Left / right thigh (leg armor) |
| **LC / RC** | Left / right calf (leg armor) |

Vests with plate slots show only their plates on the icon, or **No Plates** if the slots are empty.

Only the parts your item actually has are shown. A vest with only front and rear plates will show just `F` and `R`.

### Colors
By default the fill level tells you the story and the text stays white. Turn on **Color by wear** and it turns green, yellow, orange and finally red as the durability drops. In the inventory the numbers are always color coded.

---

## Situations that can be confusing

**"I pressed R and the panel did not appear, I reloaded instead."**
R is also the reload key. The panel needs you to **hold** the key for half a second, so a normal tap only reloads. If it still bothers you, change **Show key** to another one, or increase **Key hold time**. You will also see the panel without pressing anything every time a plate takes damage.

**"The panel is not on my screen."**
Check these, in order:
1. **Raid only** is enabled: the panel does not show in the main menu or hideout.
2. **Hide in menus / inventory** hides it whenever the mouse cursor is free. If you want to position it while F12 is open, turn that option off for a moment.
3. **Display mode** is *Auto Hide*: the panel appears when you take damage or hold the key. Choose *Always On* if you want it permanent.
4. The vest has **no plates and no soft armor inserts**, so there is nothing to show.

**"My vest has soft armor and no plates, what is shown?"**
Its soft armor inserts (Chest Soft, Back Soft, LS. Soft, RS. Soft, Neck Soft, Groin Soft, Groin R. Soft, L. Shoulder, R. Shoulder), as long as **Show inserts (vests WITHOUT plate slots)** is on (*3. Vests / Rigs*, on by default). To see the inserts of plate carriers too, turn on **Show inserts (ALL vests)**. To see only plates, turn both off.

**"The helmet total is bigger than the sum of the inserts I see."**
The helmet total is the helmet itself **plus** its inserts **plus** the Gear piece (faceshield or visor). Each line is shown separately, and the overall number is the sum of all of them.

**"There is a `G` line on my helmet, what is it?"**
It is the armored extra piece mounted on the helmet, for example the Altyn faceshield. It has its own durability and armor class, so it is listed like any other insert.

**"A plate flashed red but did not warn me."**
The *flash* tells you which plate took damage. The *alert* (notification and sound) only appears when a plate reaches **0**.

**"I changed the defaults but nothing changed."**
BepInEx keeps your old values in `BepInEx/config/com.showmetheplates.cfg`. Delete that file while the game is closed and it will be created again with the new defaults.

**"The inventory text covers the icon or is too small."**
Use **Icon text size (%)**, **Dark background** and **Background opacity** under *8. Inventory*. You can also turn off **Armor class on icon** to make the lines shorter.

**"I want the panel on the right side of the screen."**
Turn on **Mirror layout (right side)** in the section of that panel (*3. Vests / Rigs*, *4. Helmets*, *5. Facecovers*, *6. Leg Armor*). Text becomes right-aligned, the icons go to the right of the text and the rows are indented to the left. In this mode that panel's **Position X** is measured from the **right edge** of the screen, so the panel moves to the right side by itself (2 = 2 % from the right edge). Each panel can be mirrored or not independently.

**"I do not want the inventory overlay."**
Turn off **Show durability on item icon** and/or **Show durability in tooltip**. The in-raid panel keeps working.

---

## Settings overview (F12)

| Section | What it controls |
|---------|------------------|
| **1. General** | When the panel appears (Auto Hide / Always On), the show key, how long it stays visible, damage trigger, low durability threshold, raid only, hide in menus. |
| **2. Style** | Shared look of all panels: icons, icon and text size, opacity, fade, glow, color by wear, damage flash. |
| **3. Vests / Rigs** | On / off, position, size, mirror layout, and what to list: plates, inserts of vests without plate slots, inserts of all vests. |
| **4. Helmets** | On / off, position, size, mirror layout, show helmet inserts. |
| **5. Facecovers** | On / off, position, size, mirror layout, show mask inserts (ballistic masks such as the CQCM). |
| **6. Leg Armor** | On / off, position, size, mirror layout, show leg armor inserts (Manimal's Leg Armor). |
| **7. Alerts** | Alert on break, alert type (notification / panel / both), alert duration, sound volume. |
| **8. Inventory** | Durability text on icons, tooltip, armor class badges, background, text size. |

---

## Compatibility and notes
- Built for **SPT 4.1.x** (E** 0.16.9). Client-side only.
- Works with vests and helmets from other mods as long as their plate slots are named like the vanilla ones (the slot name contains `plate`) and their inserts follow the vanilla helmet slots.
- It is light: it reads the equipment twice per second and does nothing outside a raid except update the inventory icons.
- Not tested with Fika. If you try it there, please report how it goes.
- Works with mods that let you wear an armored rig on top of a body armor: the plates of the second vest are tagged with its short name.
- Compatible with **Manimal's Leg Armor** (reads the leg armor from its hidden holder, no dependency: ShowMeThePlates works the same without it).

## Reporting problems
Include your `BepInEx/LogOutput.log` (or `Player.log`), the item you were using, and a screenshot of the panel.

## AI disclosure
I am not a programmer. This mod was written together with an AI assistant (Claude), following my directions: I decided what it should do, how it should look and behave, tested it in the game, and reported what was wrong, and the AI wrote the code and fixed it from that feedback.

That means the code may not look like what an experienced developer would write, and there may be bugs I did not find. If something breaks, please open an issue with your log and what you were doing, and I will do my best to fix it.

## Credits and license
Made by **thisfkngletters**.

The **code** is released under the [MIT License](LICENSE).

**Not covered by MIT:** `Broken_Plate_Sound.wav` is a modified sound from a video game and belongs to its original rights holder. It is included for non-commercial fan use only and will be removed on request. The mod works without it: delete the file and the plugin plays the game's own alert sound instead.

## Changelog

### 1.2.0
**New**
- **One panel per category.** F12 now has a separate section for each panel: *3. Vests / Rigs*, *4. Helmets*, *5. Facecovers* and *6. Leg Armor*. Each one has its own **Enabled**, **Position X / Y**, **Size** and **Mirror layout (right side)**, so you can place, resize and mirror every block on its own. The shared look (icons, text size, glow, opacity, damage flash...) is in *2. Style*.
- **Vests / Rigs: three display switches.** **Show plates**, **Show inserts (vests WITHOUT plate slots)** and **Show inserts (ALL vests)**, so every combination is possible: plates only, plates + soft inserts of soft-armor vests (default), plates + every insert, or inserts only. They replace *Always show vest soft inserts*.
- **Ballistic masks** (Facecover slot, e.g. the CQCM ballistic mask): the mask gets its own panel with its durability (plus each armored insert if the mask has any), real icon, damage flash and break alert, like vests and helmets. In the inventory, armored masks show their **armor class badge** on the icon (no durability, the game already shows it).
- **Manimal's Leg Armor support**: if you use [Manimal's Leg Armor](https://github.com/danauraborealis/Manimal-s-Leg-Armor), your leg armor gets its own panel with the total and each insert (**L. Thigh, R. Thigh, L. Calf, R. Calf**), damage flash and break alert. Leg armor icons in the inventory show `LT`, `RT`, `LC`, `RC`. No dependency: without that mod the panel just stays empty.
- Forearm inserts (e.g. FORT Gladiator Killa edition) are named **L. Forearm / R. Forearm** (`LF`, `RF` on the icon).

**Fixes**
- The icon PNG files (`icon_<id>_original.png` / `_mask.png`) were written next to the DLL whenever *Debug overlay* was on. They now need their own hidden option, *Save icon PNGs (debug)*, off by default. You can delete any `icon_*.png` / `vest_*_debug.png` files from the plugin folder.

**Updating from 1.1.0 or older:** replace the DLL. The settings were reorganized into new sections, so delete `BepInEx/config/com.showmetheplates.cfg` while the game is closed and set the position, size and mirror of each panel again in F12.

### 1.1.0
**New**
- **Real item icons** on plates, helmet inserts and helmet visors: the game's own icon of each piece, filled by durability, like the vest and helmet. Generated once per item type and cached. Option **Real item icons on plates / inserts** (F12 > 2. Panel) goes back to the drawn shapes.
- **Mirror layout (right side)** (F12 > 2. Panel, off by default): the panel reads right to left for players who place it on the right side of the screen. Text is right-aligned, icons go to the right of the text and the plate / insert rows are mirrored. Position X is then measured from the right edge, so the panel moves to the right side by itself.
- **Soft armor inserts in the panel**: vests without plates now list their soft inserts (Chest Soft, Back Soft, LS. Soft, RS. Soft, Neck Soft, Groin Soft, Groin R. Soft, L. Shoulder, R. Shoulder) with the game's own icons. New option **Always show vest soft inserts** (F12 > 2. Panel, off by default) lists them under the plates too. They flash on damage and trigger the break alert like plates.
- **Two armored vests** (armored rig + body armor, with mods that allow it): the plates and inserts of the second vest show its short name, e.g. `F. Plate - 53/53 (APC)`. Break alerts include the name too. Header priority: a tactical rig with plate slots (even if empty), then the body armor, then any rig.
- **Inventory: soft-only vests** (no plate slots, e.g. PACA) show their inserts on the icon (`Cs`, `Bs`, `LS`, `RS`, `Ns`, `Gs`, `GRs`, `LSh`, `RSh`) and in the tooltip, like helmets.
- **Inventory: empty plate carriers** show a grey **No Plates** line on the icon. Plate carriers only ever show their plates on the icon, so vests with many inserts (IOTV, 6B23-2...) stay readable at the flea market.
- New defaults: Icon detail 70 %, Icon size 100 %, Glow strength 0.5.

**Fixes**
- With a body armor (e.g. 6B43) and an unarmored rig / harness, the panel header and icon showed the rig. They now show the body armor.

**Updating from 1.0.0:** replace the DLL. New settings are added to your config automatically. To get the new default values, delete `BepInEx/config/com.showmetheplates.cfg` while the game is closed (this resets all your settings).

### 1.0.0
- First public release.
