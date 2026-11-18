# ShowMeThePlates

## Preview

![ShowMeThePlates panel](ShowMeThePlates_panel.webp)


**See the condition of your armor plates and helmet inserts at a glance, in raid and in your inventory, without opening a single menu.**

ShowMeThePlates is a client-side BepInEx plugin for SPT 4.1. It adds a small, clean panel to your screen that shows the durability of every plate in your vest and every insert in your helmet. It also writes the same information on top of the item icons in your inventory, stash and loot, and warns you the moment a plate breaks.

> Requires SPT 4.1.x. Client-only: nothing to install on the server.

---

## Features

### 1. In-raid panel
- Shows your **vest and helmet** silhouettes, filled up according to how much durability they have left.
- Lists **each plate** (Front, Rear, Left side, Right side) and **each helmet insert** (Top, Nape, Eyes, Jaw, Ears) with its durability.
- **Auto Hide mode** (default): the panel stays out of your way and appears only when it matters:
  - when a plate or insert **loses durability** (you got hit),
  - when you **hold the show key** (R by default) for half a second,
  - while any component is below your **low durability threshold** (optional).
- Plates and inserts that just took damage **flash soft red** for a few seconds, so you can tell which one was hit.
- The panel **hides itself automatically in menus** and while the mouse cursor is free (inventory, pause, etc.).

### 2. Break alert
- When a plate or insert reaches **0 durability** you get a red Tarkov-style notification and a **sound**, so you know you are no longer protected there even in the middle of a fight.
- The alert can also be shown as blinking text under the panel (**Alert type**).

### 3. Inventory overlay
- Every vest or helmet icon in your inventory, stash and loot shows a **small list of its plates / inserts** with their durability, color coded, e.g. `F 56`, `R 60`, `LS 40`.
- The **armor class badge** (the game's own III, IV, V, VI icons) appears next to each line.
- Hover an item and the **tooltip lists every plate and insert** with its class and durability.
- Handy when you are comparing carriers at the flea market or deciding which plates to put in which vest.

### 4. Everything is configurable
Press **F12** and look for **ShowMeThePlates**. You can move the panel, resize it, change the text size, turn the glow and the damage flash on or off, pick when it appears, and decide what the inventory overlay shows. Every option has a description.

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
4. The vest has **no plates** (or only soft armor), so there is nothing to show.

**"My vest has soft armor and no plates, why is nothing shown?"**
The panel tracks plates and helmet inserts, not the built-in soft armor of the vest.

**"The helmet total is bigger than the sum of the inserts I see."**
The helmet total is the helmet itself **plus** its inserts **plus** the Gear piece (faceshield or visor). Each line is shown separately, and the overall number is the sum of all of them.

**"There is a `G` line on my helmet, what is it?"**
It is the armored extra piece mounted on the helmet, for example the Altyn faceshield. It has its own durability and armor class, so it is listed like any other insert.

**"A plate flashed red but did not warn me."**
The *flash* tells you which plate took damage. The *alert* (notification and sound) only appears when a plate reaches **0**.

**"I changed the defaults but nothing changed."**
BepInEx keeps your old values in `BepInEx/config/com.showmetheplates.cfg`. Delete that file while the game is closed and it will be created again with the new defaults.

**"The inventory text covers the icon or is too small."**
Use **Icon text size (%)**, **Dark background** and **Background opacity** under *6. Inventory*. You can also turn off **Armor class on icon** to make the lines shorter.

**"I do not want the inventory overlay."**
Turn off **Show durability on item icon** and/or **Show durability in tooltip**. The in-raid panel keeps working.

---

## Settings overview (F12)

| Section | What it controls |
|---------|------------------|
| **1. General** | When the panel appears (Auto Hide / Always On), the show key, how long it stays visible, damage trigger, low durability threshold, raid only, hide in menus. |
| **2. Panel** | Position, scale, icon and text size, opacity, fade, glow, color by wear, damage flash. |
| **3. Helmet** | Show the helmet total and inserts, and where to place the helmet block. |
| **4. Alerts** | Alert on break, alert type (notification / panel / both), alert duration, sound volume. |
| **6. Inventory** | Durability text on icons, tooltip, armor class badges, background, text size. |

---

## Compatibility and notes
- Built for **SPT 4.1.x** (EFT 0.16.9). Client-side only.
- Works with vests and helmets from other mods as long as their plate slots are named like the vanilla ones (the slot name contains `plate`) and their inserts follow the vanilla helmet slots.
- It is light: it reads the equipment twice per second and does nothing outside a raid except update the inventory icons.
- Not tested with Fika. If you try it there, please report how it goes.

## Reporting problems
Include your `BepInEx/LogOutput.log` (or `Player.log`), the item you were using, and a screenshot of the panel.

## AI disclosure
I am not a programmer. This mod was written together with an AI assistant (Claude, by Anthropic), following my directions: I decided what it should do, how it should look and behave, tested it in the game, and reported what was wrong, and the AI wrote the code and fixed it from that feedback.

That means the code may not look like what an experienced developer would write, and there may be bugs I did not find. If something breaks, please open an issue with your log and what you were doing, and I will do my best to fix it.

## Credits and license
Made by **thisfkngletters**.

The **code** is released under the [MIT License](LICENSE).

**Not covered by MIT:** `Broken_Plate_Sound.wav` is a modified sound from a video game and belongs to its original rights holder. It is included for non-commercial fan use only and will be removed on request. The mod works without it: delete the file and the plugin plays the game's own alert sound instead.

*Escape from Tarkov* and its assets belong to Battlestate Games. This is an unofficial fan-made modification, not affiliated with or endorsed by Battlestate Games.

## 1.0.0
- First public release.
