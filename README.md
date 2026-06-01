# SUBCRAFTICA (v1.0)

You wake up in the middle of the ocean, your fabricator is hungry, your lockers are chaos, and every recipe asks for three other recipes first.

So... I built **SubCraftica** to make crafting feel less like wrestling a Reaper and more like actually surviving with style. 🐟

🌊 **WHAT IS SUBCRAFTICA?**

SubCraftica is a quality-of-life crafting mod for Subnautica that helps you:

- Auto-craft sub-recipes
- Use resources from inventory + storages
- Craft multiple units in queue
- Scale craft time and energy by amount
- Understand recipe status with clearer tooltips

It keeps a **vanilla-first behavior** in core crafting flow, so things feel natural and stable.

🧰 **MAIN FEATURES**

### 1) Auto-crafting sub-recipes
If a recipe needs components that can also be crafted, SubCraftica can chain that process for you.

### 2) Inventory + storage resource usage
No more moving everything to your pockets first.
SubCraftica can pull ingredients from:
- your inventory
- nearby/allowed containers (depending on config)

### 3) Craft amount and queue
You can choose how many units you want and let the queue run.
Supported crafting modes:
- **Per-item** (vanilla-first chaining)
- **Batch** (single animation)
- **Instant** (no animation)

### 4) Time and energy scaling
Crafting can scale by quantity, including optional sub-recipe energy cost.

### 5) Container toggles in UI
Each storage can be marked with extra behavior:
- **Exclude from extraction**
- **Prefer for surplus return**

🔍 **TOOLTIP GUIDE (what the numbers and colors mean)**

### Amounts / counts
- You may see ingredient counts in formats like `owned / required`.
- In quantity crafting, total output indicators help you understand final produced amount.

### Color logic (how it actually works)
- Colors are **configurable** in mod options (presets).
- The "owned" number color communicates **source and craftability**, not only "have / missing".
- If the requirement is covered by your inventory, it uses your configured **inventory-available** color.
- If it needs storage support (inventory + storages), it uses your configured **storage-supported** color.
- If the component count is `0` but the item is craftable via sub-recipes, it is still shown as craftable (using inventory or storage-supported color depending on source).
- Missing color (your configured missing preset) is used only when the requirement is truly not craftable with current resources/planning.

### Why this matters
The tooltip is there to answer one question quickly:
> “Can I craft this now, and if not, what exactly is missing?”

⚙️ **CONFIG OPTIONS (simple map)**

- **Crafting Mode**: Per-item / Batch / Instant
- **Max Units Per Request**: queue cap
- **Storage Mode**: Disabled / Nearby / All loaded
- **Storage Range**: how far storages are scanned
- **Return Surplus To Storage**: puts leftovers back when possible
- **Energy Multiplier**: scales energy cost
- **Include Subrecipe Energy**: include nested craft energy or not
- **Tooltip Mode**: Disabled / Basic / Advanced
- **Ingredient Color Presets**: visual style preferences

🌍 **LOCALIZATION**

SubCraftica ships with 27 language files in `lang/`.

📦 **REQUIREMENTS**

- Subnautica
- BepInEx
- Nautilus

Optional compatibility mods:
- Mades Redo Inventory Stacking
- Inventory Resource Stacks
- Visible Locker Interior (Fix for visual issues in storage interactions)

🧩 **COMPATIBILITY NOTES**

Designed to work with common stacking mods and vanilla-first crafting flow.

Optional compatibility mods:
- Mades Redo Inventory Stacking
- Inventory Resource Stacks
- Visible Locker Interior (Fix for visual issues in storage interactions)

**Final note:** SubCraftica may be incompatible with any other mod that modifies the same crafting queue, tooltip, storage extraction, or autocrafting flow internals.

🛠️ **INSTALLATION**

1. Install **BepInEx** and **Nautilus**.
2. Copy `SubCraftica.dll` to:
   - `BepInEx/plugins/SubCraftica/SubCraftica.dll`
3. Copy the `lang` folder to:
   - `BepInEx/plugins/SubCraftica/lang/`
4. Launch the game and configure SubCraftica in the mods/options menu.

💙 **THANKS / INSPIRATION**

Huge thanks to the modding ecosystem and authors whose work inspired this project and pushed me to keep improving:

- Nautilus and BepInEx communities
- Stacking mod inspirations and compatibility references:
  - **Mades Redo Inventory Stacking**
  - **Inventory Resource Stacks**
- Auto-crafting / queue inspiration mods:
  - **EasyCraft** (major inspiration)
- **VisibleLockerInterior** and other QoL mod creators
- Players who report bugs with patience and useful logs 💙

---

🫧 **Final personal note (from the pirate of this tiny lifepod)**

I’m not a professional programmer — just someone stubborn enough to keep debugging until 3 AM because “one more test and I sleep” (spoiler: I don’t).
This mod took a lot of time, effort, and love. I’ll keep trying to help, fix, and improve it as much as possible.

If something breaks, tell me. If something works, also tell me (my morale is powered by small victories and caffeine).

And yes, real life economy is currently doing a perfect impression of a damaged Seamoth hull 😅
So if you want to support the project, I’ll leave my link here:

**[Put your support link here]**

No pressure at all — enjoying the mod already means a lot.

Now go craft responsibly... and please don’t feed titanium to Leviathans.
