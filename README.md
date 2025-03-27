Routimator is an advanced state machine plugin designed for Voxta and Timeline. It builds on the MacGruber state machine (heavily modified) to serve as a powerful navigation layer between Timeline animations and Voxta actions. Using a breadth-first search (BFS) algorithm, Routimator finds the shortest valid path from a starting state (or animation) to a target state, automatically triggering the necessary transitions along the way.

Check out our Routimator Diagram Visualizer to better understand how it works: https://doc.voxta.ai/diagram.html
​
Key Features​
Advanced State Navigation:
Routimator uses a BFS algorithm to determine the shortest path between states. For instance, transitioning from a bedroom sitting animation to a bathroom shower animation may involve several intermediate states and transitions that the plugin will execute in sequence.
Seamless Integration:
If a state’s name matches a timeline animation name, Routimator automatically plays that animation—eliminating the need for additional triggers.
Flexible Transition Logic:
Define custom transition paths by setting which states can lead into others. This gives you the power to create very complex logic chains across numerous timeline animations.
Voxta Flag Management:
When navigation is active, the plugin automatically sets a special Voxta flag (nav). This flag can be used to block conflicting actions until navigation is complete. Once the destination state (a looping state) is reached, the plugin unsets the flag (!nav), allowing your character to select other actions.
Grouping & Organization:
Group states based on context (e.g., different rooms or poses). For example, you might have a “sitting” group and a “standing” group, each with its own transition logic.
Save and Load Presets:
Easily save your state and transition configurations as presets, and load them back later. This is particularly useful when working with complex scenes or when sharing setups with others.
Interrupt Navigation:
If a navigation route becomes too long or if you need to abort a transition mid-route, an “Interrupt Navigation” command allows you to stop the routing process. The plugin will halt routing once it reaches the first looping state (prefixed with VS_).
How to Use Routimator​
Setup Your States and Transitions:
Add New States:
Use the UI to add new states. The default naming scheme is VS_new_state for looping states and VT_new_state for transition states. Duplicate states or rename them as needed.
Define Transitions:
Select a state and add transitions by choosing from available states. This creates the links that the BFS algorithm will follow.
Grouping:
Organize your states by groups (e.g., different rooms or poses). This allows you to maintain separate transition logic for different parts of your scene.
Navigating Between States:
Manual Switch:
You can manually trigger a state switch.
Route Navigation:
Select your target state and click "Navigate to Selected State". The BFS algorithm will calculate and execute the shortest route.
Interrupting Navigation:
If necessary, use the "Interrupt Navigation" command to halt routing mid-way.
Timeline & Voxta Integration:
Automatic Animation Play:
When a state name matches a timeline animation, Routimator will trigger that animation automatically.
Voxta Flag Handling:
The plugin sets the nav flag during active navigation to prevent other actions from interfering. Once navigation completes, the !nav flag is applied to allow normal operations.
Saving and Loading Presets:
Use the provided save/load UI to store your configuration. This makes it easy to reuse or share complex setups.

You can find more information on the Voxta documentation website.
