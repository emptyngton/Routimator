# Routimator

Routimator is an advanced state machine plugin designed for Voxta and Timeline. It builds on the MacGruber state machine (heavily modified) to serve as a powerful navigation layer between Timeline animations and Voxta actions. Using a breadth-first search (BFS) algorithm, Routimator finds the shortest valid path from a starting state (or animation) to a target state, automatically triggering the necessary transitions along the way.

🌐 **Available on [VaM Hub](https://hub.virtamate.com/resources/routimator.55321/)** — recommended install for auto-updates.
📚 **[Full documentation](https://doc.voxta.ai/vam/routimator/)**

👉 Check out our Routimator Diagram Visualizer to better understand how it works.

## 🔑 Key Features

### ✅ Advanced State Navigation
Routimator uses a BFS algorithm to determine the shortest path between states.
Example: transitioning from a bedroom sitting animation to a bathroom shower animation may involve several intermediate states that the plugin will execute in sequence.

### ✅ Seamless Integration
If a state's name matches a Timeline animation name, Routimator automatically plays that animation — no additional triggers required.

### ✅ Flexible Transition Logic
Define custom transition paths by setting which states can lead into others. Build complex logic chains across numerous Timeline animations.

### ✅ Voxta Flag Management
During navigation, Routimator sets a special Voxta flag: `nav`

- This flag can block conflicting actions until navigation is complete
- When a looping state is reached, Routimator sets `!nav`, allowing normal interaction again

### ✅ Grouping & Organization
Group states by context (e.g. rooms or poses).
Example: a "sitting" group and a "standing" group, each with its own logic.

### ✅ Save and Load Presets
Save your state and transition configurations as presets and load them later. Ideal for complex scenes or sharing setups.

### ✅ Interrupt Navigation
Abort routing mid-path using the Interrupt Navigation command. The plugin will halt once it reaches the first looping state (prefixed with `VS_`).

## 🚀 How to Use Routimator

### 1. Setup Your States and Transitions

**➕ Add New States**
Use the UI to add new states.
- Looping states: `VS_new_state`
- Transition states: `VT_new_state`

You can rename or duplicate states as needed.

**🔁 Define Transitions**
Select a state, then add transitions by choosing from available states. This builds the routing map that BFS will use.

**🗂 Grouping**
Organize states by groups (e.g. "Bedroom", "Bathroom", "Standing") to keep logic manageable and separated by context.

### 2. Navigating Between States

**🎛 Manual Switch** — trigger a manual state switch.

**🧭 Route Navigation** — select your target state and click "Navigate to Selected State". Routimator will calculate and execute the shortest route.

**🛑 Interrupting Navigation** — use the "Interrupt Navigation" command if needed.

### 3. Timeline & Voxta Integration

**🎞 Automatic Animation Play** — if a state name matches a Timeline animation, Routimator triggers it automatically.

**🏁 Voxta Flag Handling**
- `nav` is set while routing is active
- `!nav` is set once destination is reached

### 💾 Saving and Loading Presets

Use the UI to save/load configurations. Reuse and share complex setups easily.

📚 More info available at the Voxta Documentation Website.

---

## Install

**End users:** install from [VaM Hub](https://hub.virtamate.com/resources/routimator.55321/), or grab the latest `.var` from the [GitHub Releases](https://github.com/emptyngton/Routimator/releases) page.

**From source (for development):** copy this repo into `<VaM>/Custom/Scripts/Lapiro/Project_Routimator/` and point VaM at `Custom/Scripts/Lapiro/Routimator/Routimator.cslist`.

Either way, load `Routimator.cslist` as a **Person atom plugin** (from the Person atom's Plugins tab → Add Plugin). Routing is character-specific, so each Person that should be routable needs its own Routimator instance.

**Requirements:**
- [Virt-A-Mate](https://virtamate.com/)
- [VamTimeline](https://github.com/acidbubbles/vam-timeline) — as an Atom plugin on the Person atom
- Voxta VaM plugin — optional, for flag-driven AI integration
- `MacGruber_Utils.cs` — bundled in `src/lib/`

## Project layout

```
Routimator/
├── Routimator.cslist                    # VaM plugin manifest
├── src/
│   ├── Routimator.Core.cs               # fields, Init, Update, OnDestroy
│   ├── Routimator.UI.cs                 # UI callbacks, status info
│   ├── Routimator.StateMachine.cs       # state transitions
│   ├── Routimator.Routing.cs            # navigation + Timeline event handling
│   ├── Routimator.Persistence.cs        # JSON save/load
│   ├── Routimator.ExternalAPI.cs        # listener broadcast, graph bridges
│   ├── RoutimatorState.cs               # state manager
│   ├── RoutimatorNavigation.cs          # BFS + walking-route search
│   ├── RoutimatorTimeline.cs            # Timeline plugin dispatcher
│   ├── RoutimatorVoxta.cs               # Voxta plugin dispatcher
│   ├── RoutimatorUI.cs                  # dynamic UI builder
│   ├── RoutimatorSerialization.cs       # JSON (de)serialization
│   ├── RoutimatorGraph.cs               # interactive graph visualizer
│   └── lib/
│       └── MacGruber_Utils.cs           # bundled dependency
├── LICENSE
└── README.md
```

The six `Routimator.*.cs` files are all `partial class Routimator` — they compile as a single class. The split is purely organizational; behavior is identical to the pre-split monolith.

## Development

Open the project folder in JetBrains Rider (or any C# IDE). Note: VaM plugins are sandboxed — `System.Reflection` is prohibited, and only C# 6.0 language features are supported.

## Credits

- Original **Routimator** by [Lapiro](https://virtamate.com/).
- Bundled `MacGruber_Utils.cs` by **MacGruber**.

## License

[Apache License 2.0](LICENSE).
