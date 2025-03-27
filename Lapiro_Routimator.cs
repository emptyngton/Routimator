using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Needed for LINQ methods such as FirstOrDefault
using SimpleJSON;
using MacGruber;
using MVR.FileManagementSecure;

namespace Lapiro
{
    public class Routimator : MVRScript
    {
        // ***** GROUPS FIELDS *****
        private JSONStorableStringChooser myGroupChooser;
        private List<string> myGroups = new List<string>();

        private JSONStorableStringChooser myStateChooser;
        private JSONStorableString myCurrentStateInfo;
        private JSONStorableString myCurrentClockInfo;
        private JSONStorableString mySelectedStateInfo;
        private JSONStorableString myGroupName;

        // External triggers to switch states or do BFS route switching
        private JSONStorableString mySwitchState;
        private JSONStorableString myRouteSwitchState;  

        // NEW: External trigger for Interrupt Navigation via external command
        private JSONStorableString myInterruptNavigation;

        // NEW: External trigger for continue navigation via external command.
        // When the plugin receives "continue_navigation", it will break out of a NAV state.
        private JSONStorableString myContinueNavigation;

        private JSONStorableString myStateName;
        private JSONStorableBool myStateInfiniteDuration;
        private JSONStorableFloat myStateDuration;
        
        // NEW: JSONStorable for Set Flags field (state-specific)
        private JSONStorableString myStateSetFlags;

        // NEW: Global field for exit flags (to clear nav plus additional custom flags)
        private JSONStorableString mySetFlagsOnExitNavigation;

        // For adding transitions from the selected state
        private JSONStorableStringChooser myTransitionChooser;

        // Internal UI elements list
        private List<object> myStateUI = new List<object>();

        // Internal references
        private State myCurrentState = null;
        private float myClock = 0.0f;
        private float myDuration = 1.0f;
        private float myValue = -1.0f;
        private int myClockInt = -1;

        private bool myNeedUIRefresh = false;
        private bool myNeedUIRebuild = false;

        // For BFS route switching
        private List<State> routingQueue = new List<State>();
        private bool routingActive = false;
        // NEW: Field to mark the designated VT target state so we can delay clearing nav flag.
        private State navigationTargetState;

        // ***** NEW: Timeline plugin fields *****
        private string timelineTargetAtomName = "Person"; // Atom containing Timeline
        private string timelineTargetPluginID = "plugin#1_VamTimeline.AtomPlugin"; // Exact plugin name
        private JSONStorable timelineStorable; // Reference to Timeline plugin

        // ***** NEW: Voxta plugin fields *****
        private string voxtaTargetAtomName = "Person"; // Atom containing Voxta plugin
        private string voxtaTargetPluginID = "plugin#3_Voxta"; // This can vary
        private JSONStorable voxtaStorable; // Reference to Voxta plugin

        // ***** NEW: SAVE/LOAD UI fields *****
        private JSONStorableUrl loadURL;
        private const string BASE_DIRECTORY = "Saves/PluginData/Lapiro/Routimator";

        /// <summary>
        /// Helper to color the state name.
        /// </summary>
        private string GetColoredStateName(State state)
        {
            if (state.Name.StartsWith("VS"))
                return "<b><color=#0089b4>" + state.Name + "</color></b>";
            if (state.Name.StartsWith("VT"))
                return "<b><color=#c95803>" + state.Name + "</color></b>";
            return "<b>" + state.Name + "</b>";
        }

        // ==========================================================
        // STATE CLASS (with new Group and SetFlags fields)
        // ==========================================================
        private class State
        {
            private string myName;
            public State(MVRScript script, string name)
            {
                myName = name;
                Group = "Group_1";
                // Initialize trigger names with a space between "On" and the type.
                EnterTrigger = new EventTrigger(script, "On Enter " + name);
                ValueTrigger = new FloatTrigger(script, "On Value " + name);
                ExitTrigger = new EventTrigger(script, "On Exit " + name);
                Transitions = new List<State>();
                SetFlags = "";
            }

            public State(MVRScript script, string name, State source)
            {
                myName = name;
                Group = source.Group;
                EnterTrigger = new EventTrigger(source.EnterTrigger);
                ValueTrigger = new FloatTrigger(source.ValueTrigger);
                ExitTrigger = new EventTrigger(source.ExitTrigger);
                InfiniteDuration = source.InfiniteDuration;
                Duration = source.Duration;
                Transitions = new List<State>(source.Transitions);
                SetFlags = source.SetFlags;
            }

            public static int SortByNameAscending(State a, State b)
            {
                return a.Name.CompareTo(b.Name);
            }

            public string Name
            {
                get { return myName; }
                set
                {
                    myName = value;
                    // Always use "On Enter", etc. with a space.
                    EnterTrigger.Name = "On Enter " + value;
                    ValueTrigger.Name = "On Value " + value;
                    ExitTrigger.Name = "On Exit " + value;
                }
            }

            public string Group = "Group_1";
            public string SetFlags;
            public EventTrigger EnterTrigger;
            public FloatTrigger ValueTrigger;
            public EventTrigger ExitTrigger;
            public bool InfiniteDuration = false;
            public float Duration = 3.0f;
            public List<State> Transitions;

            // --- New overrides for equality based on Name ---
            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is State))
                    return false;
                State other = (State)obj;
                return this.Name.Equals(other.Name);
            }

            public override int GetHashCode()
            {
                return this.Name.GetHashCode();
            }
        }

        private List<State> myStates = new List<State>();

        public override void Init()
        {
            // Initialize main UI for state editing
            Utils.OnInitUI(CreateUIElement);

            myCurrentStateInfo = new JSONStorableString("CurrentState", "Current State: ");
            Utils.SetupInfoOneLine(this, myCurrentStateInfo, false);

            mySelectedStateInfo = new JSONStorableString("SelectedStateInfo", "Selected/Target State: ");
            Utils.SetupInfoOneLine(this, mySelectedStateInfo, false);

            myCurrentClockInfo = new JSONStorableString("CurrentClock", "Current Clock: ");
            Utils.SetupInfoOneLine(this, myCurrentClockInfo, false);

            // NEW: Initialize and register global exit flags field.
            mySetFlagsOnExitNavigation = new JSONStorableString("SetFlagsOnExitNavigation", "", UISetFlagsOnExitNavigation);
            RegisterString(mySetFlagsOnExitNavigation);
            // NEW: Add global exit flags UI right after Current Clock.
            UIDynamicLabelInput exitFlagsInput = Utils.SetupTextInput(this, "Exit Flags", mySetFlagsOnExitNavigation, false);

            // Ensure a default group exists.
            if (myGroups.Count == 0)
                myGroups.Add("Group_1");

            myGroupChooser = new JSONStorableStringChooser("Selected Group", myGroups, myGroups[0], "Selected Group");
            myGroupChooser.setCallbackFunction += UISetGroup;
            CreateFilterablePopup(myGroupChooser, false);
            Utils.SetupTwinButton(this, "Add New Group", UIAddGroup, "Delete Group", UIDeleteGroup, false);

            myStateChooser = new JSONStorableStringChooser("Selected State", new List<string>(), string.Empty, "Selected State");
            myStateChooser.displayChoices = new List<string>();
            myStateChooser.setCallbackFunction += UISetState;
            CreateFilterablePopup(myStateChooser, false);

            Utils.SetupTwinButton(this, "▲", UIMoveStateUp, "▼", UIMoveStateDown, false);
            Utils.SetupTwinButton(this, "Add New State", UIAddState, "Duplicate State", UIDuplicateState, false);
            Utils.SetupButton(this, "Remove State", UIRemoveState, false);
            Utils.SetupSpacer(this, 5, false);
            Utils.SetupButton(this, "<b>Activate Selected State</b>", UISwitchState, false);

            mySwitchState = new JSONStorableString("SwitchState", "", SwitchStateAction);
            mySwitchState.isStorable = mySwitchState.isRestorable = false;
            RegisterString(mySwitchState);

            myRouteSwitchState = new JSONStorableString("RouteSwitchState", "", RouteSwitchStateAction);
            myRouteSwitchState.isStorable = myRouteSwitchState.isRestorable = false;
            RegisterString(myRouteSwitchState);
            Utils.SetupButton(this, "<b>Navigate to Selected State</b>", UIRouteSwitchState, false);
            Utils.SetupButton(this, "<b>Interrupt Navigation</b>", InterruptNavigation, false);

            // NEW: Register external trigger for interrupting navigation.
            myInterruptNavigation = new JSONStorableString("InterruptNavigation", "", InterruptNavigationAction);
            myInterruptNavigation.isStorable = myInterruptNavigation.isRestorable = false;
            RegisterString(myInterruptNavigation);

            // NEW: Register external trigger for continue navigation.
            myContinueNavigation = new JSONStorableString("ContinueNavigation", "", ContinueNavigationAction);
            myContinueNavigation.isStorable = myContinueNavigation.isRestorable = false;
            RegisterString(myContinueNavigation);
			
			
						// NEW: Register external action triggers (for direct use in VAM UI)
			JSONStorableAction extInterruptNavigation = new JSONStorableAction("InterruptNavigationTrigger", () =>
			{
				// Directly call the interrupt function
				InterruptNavigation();
			});
			RegisterAction(extInterruptNavigation);

			JSONStorableAction extContinueNavigation = new JSONStorableAction("ContinueNavigationTrigger", () =>
			{
				// Directly trigger "continue" without needing to type "continue_navigation"
				if (myCurrentState != null && myCurrentState.Name.StartsWith("NAV_state", System.StringComparison.OrdinalIgnoreCase))
				{
					if (routingActive && routingQueue.Count > 0)
					{
						SwitchState(routingQueue[0]);
						routingQueue.RemoveAt(0);
					}
					else
					{
						Transition();
					}
				}
				else
				{
					SuperController.LogMessage("Continue navigation command received but current state is not a NAV_state.");
				}
			});
			RegisterAction(extContinueNavigation);
			
            myStateName = new JSONStorableString("Name", "", UIRenameState);
            myStateInfiniteDuration = new JSONStorableBool("Loop", false);
            myStateInfiniteDuration.setCallbackFunction += UISetInfiniteDuration;

            myStateDuration = new JSONStorableFloat("Duration (sec)", 3, 0, 60, true, true);
            myStateDuration.constrained = false;
            myStateDuration.setCallbackFunction += UISetDuration;

            myStateSetFlags = new JSONStorableString("SetFlags", "", UISetFlags);
            RegisterString(myStateSetFlags);

            myTransitionChooser = new JSONStorableStringChooser("Transition", new List<string>(), "", "Transition to Add");
            myTransitionChooser.displayChoices = new List<string>();

            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
            SimpleTriggerHandler.LoadAssets();
            
            myGroupName = new JSONStorableString("GroupName", myGroupChooser.val, UIGroupRename);
            RegisterString(myGroupName);

            // Start the coroutines to find the Timeline and Voxta plugins.
            StartCoroutine(FindTimelinePlugin());
            StartCoroutine(FindVoxtaPlugin());

            // ***** SETUP SAVE/LOAD FUNCTIONALITY *****
            FileManagerSecure.CreateDirectory(BASE_DIRECTORY);
            loadURL = new JSONStorableUrl("loadURL", "", UILoadJSON, "rm", true);
            loadURL.hideExtension = true;
            loadURL.allowFullComputerBrowse = false;
            loadURL.allowBrowseAboveSuggestedPath = true;
            loadURL.SetFilePath(BASE_DIRECTORY + "/");
            // Clear stored value if it isn’t a valid preset file
            if (!loadURL.val.EndsWith(".rm"))
            {
                loadURL.val = "";
            }
            
            // Create Save/Load UI buttons as a twin button so they appear next to each other.
            CreateSaveLoadUI();
        }

        // Callback for the external interrupt navigation trigger.
        private void InterruptNavigationAction(string val)
        {
            // Check for the specific command (case-insensitive)
            if (!string.IsNullOrEmpty(val) && val.Equals("interrupt_navigation", System.StringComparison.OrdinalIgnoreCase))
            {
                InterruptNavigation();
            }
            // Clear the storable to allow new triggers
            myInterruptNavigation.val = "";
        }

        // Callback for the external continue navigation trigger.
        private void ContinueNavigationAction(string val)
        {
            if (!string.IsNullOrEmpty(val) && val.Equals("continue_navigation", System.StringComparison.OrdinalIgnoreCase))
            {
                // Check if current state starts with "NAV_state"
                if (myCurrentState != null && myCurrentState.Name.StartsWith("NAV_state", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (routingActive && routingQueue.Count > 0)
                    {
                        SwitchState(routingQueue[0]);
                        routingQueue.RemoveAt(0);
                    }
                    else
                    {
                        Transition();
                    }
                }
                else
                {
                    SuperController.LogMessage("Continue navigation command received but current state is not a NAV_state.");
                }
            }
            // Clear the storable to allow new triggers
            myContinueNavigation.val = "";
        }

        // Global callback for the exit flags field.
        private void UISetFlagsOnExitNavigation(string newFlags)
        {
            // This global field holds custom flags for exit navigation.
            // No additional processing is needed.
        }

        // Main UI for state editing (called via Utils.OnInitUI)
        private void CreateUIElement() 
        {
            UIRebuild();
            // (The state–editing UI is built here.)
        }

        // New method to create Save/Load UI buttons side-by-side.
        private void CreateSaveLoadUI()
        {
            // Use the twin button helper to create Save and Load buttons in one row.
            UIDynamicTwinButton twin = Utils.SetupTwinButton(this, "Save", UISaveJSONDialog, "Load", delegate { }, false);
            // Register the file browse functionality on the right button.
            loadURL.RegisterFileBrowseButton(twin.buttonRight);
        }

        // Save/Load UI Methods

        private void UISaveJSONDialog() 
        {
            SuperController sc = SuperController.singleton;
            sc.GetMediaPathDialog(UISaveJSON, "rm", BASE_DIRECTORY, false, true, false, null, true, null, false, false);
            sc.mediaFileBrowserUI.SetTextEntry(true);
            if (sc.mediaFileBrowserUI.fileEntryField != null)
            {
                string filename = System.DateTime.Now.ToString("yyyyMMdd_HH-mm-ss");
                sc.mediaFileBrowserUI.fileEntryField.text = filename;
                sc.mediaFileBrowserUI.ActivateFileNameField();
            }
        }
        
        private void UISaveJSON(string path) 
        {
            if (string.IsNullOrEmpty(path))
                return;
            string filePath = path.Replace('\\', '/') + ".rm";
            JSONClass jc = GetJSON();
            SaveJSON(jc, filePath);
            SuperController.LogMessage("Routimator state saved to " + filePath);
        }
        
        private void UILoadJSON(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.EndsWith(".rm"))
            {
                SuperController.LogMessage("Routimator: No valid preset file selected.");
                return;
            }
            JSONClass jc = LoadJSON(url).AsObject;
            if (jc != null) 
            {
                LateRestoreFromJSON(jc);
                UIRebuild();
                SuperController.LogMessage("Routimator state loaded from " + url);
            }
            else
            {
                SuperController.LogError("Routimator: File " + url + " not found or could not be loaded.");
            }
        }

        // -------------------- [Remaining methods remain unchanged] --------------------
        private void UISetGroup(string groupName)
        {
            UIRebuild();
        }

        private void UIAddGroup()
        {
            for (int i = 1; i < 1000; i++)
            {
                string groupName = "Group_" + i;
                if (!myGroups.Contains(groupName))
                {
                    myGroups.Add(groupName);
                    myGroupChooser.choices = new List<string>(myGroups);
                    myGroupChooser.valNoCallback = groupName;
                    UIRebuild();
                    return;
                }
            }
            SuperController.LogError("Too many groups!");
        }

        // Delete the group and remove all states in that group.
        private void UIDeleteGroup()
        {
            string currentGroup = myGroupChooser.val;
            myStates.RemoveAll(s => s.Group == currentGroup);
            myGroups.Remove(currentGroup);
            myGroupChooser.choices = new List<string>(myGroups);
            myGroupChooser.valNoCallback = myGroups.Count > 0 ? myGroups[0] : "";
            UIRebuild();
        }

        private void UIRebuild()
        {
            List<string> options = new List<string>();
            List<string> displays = new List<string>();
            foreach (State s in myStates)
            {
                if (s.Group == myGroupChooser.val)
                {
                    options.Add(s.Name);
                    displays.Add(GetColoredStateName(s));
                }
            }
            myStateChooser.choices = options;
            myStateChooser.displayChoices = displays;
            if (options.Count > 0)
            {
                if (!options.Contains(myStateChooser.val))
                    myStateChooser.val = options[0];
                else
                    UISetState(myStateChooser.val);
            }
            else
            {
                myStateChooser.val = "";
                UISetState("");
            }
        }

        private void UISetState(string v)
        {
            mySelectedStateInfo.val = "Target State: " + v;
            Utils.RemoveUIElements(this, myStateUI);
            myGroupName.valNoCallback = myGroupChooser.val;
            UIDynamicLabelInput groupNameInput = Utils.SetupTextInput(this, "Group Name", myGroupName, false);
            myStateUI.Add(groupNameInput);
            State state = GetSelectedState();
            if (state == null)
                return;
            myStateName.valNoCallback = state.Name;
            UIDynamicLabelInput stateNameInput = Utils.SetupTextInput(this, "State Name", myStateName, false);
            myStateUI.Add(stateNameInput);
            myStateSetFlags.valNoCallback = state.SetFlags;
            UIDynamicLabelInput setFlagsInput = Utils.SetupTextInput(this, "Set Flags", myStateSetFlags, false);
            myStateUI.Add(setFlagsInput);

            // (Note: The global exit flags field is now part of the main UI and not repeated here.)

            myStateInfiniteDuration.valNoCallback = state.InfiniteDuration;
            UIDynamicToggle loopToggle = CreateToggle(myStateInfiniteDuration, false);
            myStateUI.Add(loopToggle);
            if (!state.InfiniteDuration)
            {
                myStateDuration.valNoCallback = state.Duration;
                UIDynamicSlider durSlider = CreateSlider(myStateDuration, false);
                durSlider.rangeAdjustEnabled = true;
                myStateUI.Add(myStateDuration);
            }
            UIDynamicTextInfo triggerHeader = Utils.SetupInfoOneLine(this, "<size=30><b>Triggers</b></size>", true);
            myStateUI.Add(triggerHeader);

            UIDynamicButton enterButton = Utils.SetupButton(this, "On Enter " + state.Name, state.EnterTrigger.OpenPanel, true);
            UIDynamicButton valueButton = Utils.SetupButton(this, "On Value " + state.Name, state.ValueTrigger.OpenPanel, true);
            UIDynamicButton exitButton = Utils.SetupButton(this, "On Exit " + state.Name, state.ExitTrigger.OpenPanel, true);
            myStateUI.Add(enterButton);
            myStateUI.Add(valueButton);
            myStateUI.Add(exitButton);

            if (myStates.Count > 0)
            {
                UIDynamicTextInfo transitionsHeader = Utils.SetupInfoOneLine(this, "<size=30><b>Transitions</b></size>", true);
                myStateUI.Add(transitionsHeader);
                List<string> transitionNames = new List<string>();
                List<string> displayNames = new List<string>();
                bool selectedStartsWithVS = state.Name.StartsWith("VS");
                bool selectedStartsWithVT = state.Name.StartsWith("VT");
                for (int s = 0; s < myStates.Count; ++s)
                {
                    var tState = myStates[s];
                    if (tState == state || state.Transitions.Contains(tState))
                        continue;
                    if ((selectedStartsWithVS && tState.Name.StartsWith("VS")) ||
                        (selectedStartsWithVT && tState.Name.StartsWith("VT")))
                        continue;
                    transitionNames.Add(tState.Name);
                    displayNames.Add(GetColoredStateName(tState));
                }
                myTransitionChooser.choices = transitionNames;
                myTransitionChooser.displayChoices = displayNames;
                myTransitionChooser.val = transitionNames.Count == 0 ? "" : transitionNames[0];
                UIDynamicPopup transitionPopup = CreateFilterablePopup(myTransitionChooser, true);
                myStateUI.Add(transitionPopup);
                UIDynamicButton addTransitionBtn = Utils.SetupButton(this, "Add Transition", () => UIAddTransition(state), true);
                myStateUI.Add(addTransitionBtn);
                for (int t = 0; t < state.Transitions.Count; t++)
                {
                    State trans = state.Transitions[t];
                    string label = GetColoredStateName(trans);
                    UIDynamicLabelXButton btn = Utils.SetupLabelXButton(this, label, () => UIRemoveTransition(state, trans), true);
                    myStateUI.Add(btn);
                }
            }
        }

        private void UISetInfiniteDuration(bool v)
        {
            State state = GetSelectedState();
            if (state == null)
                return;
            state.InfiniteDuration = v;
            UIRefresh();
            myClockInt = -1;
            myValue = -1.0f;
            UpdateCurrentStateInfo();
            UpdateCurrentStateValue();
        }

        private void UISetDuration(float v)
        {
            State state = GetSelectedState();
            if (state == null)
                return;
            state.Duration = v;
        }

        private void UISetFlags(string newFlags)
        {
            State state = GetSelectedState();
            if (state == null)
                return;
            state.SetFlags = newFlags;
        }

        private void UIGroupRename(string newName)
        {
            string oldName = myGroupChooser.val;
            if (string.IsNullOrEmpty(oldName))
                return;
            int index = myGroups.IndexOf(oldName);
            if (index < 0)
                return;
            myGroups[index] = newName;
            myGroupChooser.choices = new List<string>(myGroups);
            myGroupChooser.valNoCallback = newName;
            foreach (State s in myStates)
            {
                if (s.Group == oldName)
                    s.Group = newName;
            }
        }

        private void UIRenameState(string newName)
        {
            State state = GetSelectedState();
            if (state == null)
                return;
            state.Name = newName;
            // Force infinite duration for any state that starts with "NAV_state".
            if(newName.StartsWith("NAV_state", System.StringComparison.OrdinalIgnoreCase))
            {
                state.InfiniteDuration = true;
            }
            else if (newName.StartsWith("VS_"))
                state.InfiniteDuration = true;
            else if (newName.StartsWith("VT_"))
                state.InfiniteDuration = false;
            myStates.Sort(State.SortByNameAscending);
            UIRebuild();
            myStateChooser.val = newName;
        }

        private void UIAddState() 
        { 
            if (myGroups.Count == 0)
            {
                myGroups.Add("Group_1");
                myGroupChooser.choices = new List<string>(myGroups);
                myGroupChooser.valNoCallback = "Group_1";
            }
            UIAddState(null); 
        }

        private void UIAddState(State source)
        {
            for (int i = 1; i < 1000; i++)
            {
                string name = "VS_new_state_" + i;
                bool exists = myStates.Exists(s => s.Group == myGroupChooser.val && s.Name == name);
                if (!exists)
                {
                    State st = (source == null) ? new State(this, name) : new State(this, name, source);
                    st.Group = myGroupChooser.val;
                    if (name.StartsWith("VS_"))
                        st.InfiniteDuration = true;
                    else if (name.StartsWith("VT_"))
                        st.InfiniteDuration = false;
                    myStates.Add(st);
                    UIRebuild();
                    myStateChooser.val = name;
                    return;
                }
            }
            SuperController.LogError("Too many states!");
        }

        private void UIDuplicateState()
        {
            int idx = myStateChooser.choices.IndexOf(myStateChooser.val);
            if (idx < 0)
                return;
            State orig = GetSelectedState();
            string originalName = orig.Name;
            string newName = GetNextName(originalName);
            int tries = 0;
            while (myStates.Exists(s => s.Group == myGroupChooser.val && s.Name == newName) && tries < 999)
            {
                newName = GetNextName(newName);
                tries++;
            }
            State duplicate = new State(this, newName, orig);
            duplicate.Group = myGroupChooser.val;
            if (newName.StartsWith("VS_"))
                duplicate.InfiniteDuration = true;
            else if (newName.StartsWith("VT_"))
                duplicate.InfiniteDuration = false;
            myStates.Add(duplicate);
            UIRebuild();
            myStateChooser.val = newName;
        }

        private string GetNextName(string baseName)
        {
            int underscoreIndex = baseName.LastIndexOf('_');
            if (underscoreIndex != -1 && underscoreIndex < baseName.Length - 1)
            {
                string suffix = baseName.Substring(underscoreIndex + 1);
                int num;
                if (int.TryParse(suffix, out num))
                {
                    string prefix = baseName.Substring(0, underscoreIndex);
                    int next = num + 1;
                    return prefix + "_" + next;
                }
            }
            return baseName + "_1";
        }

        private void UIRemoveState()
        {
            int idx = myStateChooser.choices.IndexOf(myStateChooser.val);
            if (idx < 0)
                return;
            State st = GetSelectedState();
            if (myCurrentState == st)
                SwitchState(null);
            for (int s = 0; s < myStates.Count; s++)
                myStates[s].Transitions.Remove(st);
            st.EnterTrigger.Remove();
            st.ValueTrigger.Remove();
            st.ExitTrigger.Remove();
            myStates.Remove(st);
            UIRebuild();
            if (myStateChooser.choices.Count > 0)
                myStateChooser.val = myStateChooser.choices[0];
            else
                myStateChooser.val = "";
        }

        private void UIMoveStateUp()
        {
            List<State> groupStates = myStates.FindAll(s => s.Group == myGroupChooser.val);
            int index = groupStates.FindIndex(s => s.Name == myStateChooser.val);
            if (index > 0)
            {
                State temp = groupStates[index - 1];
                groupStates[index - 1] = groupStates[index];
                groupStates[index] = temp;
                int count = 0;
                for (int i = 0; i < myStates.Count; i++)
                    if (myStates[i].Group == myGroupChooser.val)
                        myStates[i] = groupStates[count++];
                UIRebuild();
                myStateChooser.val = groupStates[index - 1].Name;
            }
        }

        private void UIMoveStateDown()
        {
            List<State> groupStates = myStates.FindAll(s => s.Group == myGroupChooser.val);
            int index = groupStates.FindIndex(s => s.Name == myStateChooser.val);
            if (index >= 0 && index < groupStates.Count - 1)
            {
                State temp = groupStates[index + 1];
                groupStates[index + 1] = groupStates[index];
                groupStates[index] = temp;
                int count = 0;
                for (int i = 0; i < myStates.Count; i++)
                    if (myStates[i].Group == myGroupChooser.val)
                        myStates[i] = groupStates[count++];
                UIRebuild();
                myStateChooser.val = groupStates[index + 1].Name;
            }
        }

        private void UISwitchState() { SwitchState(GetSelectedState()); }

        private void UIAddTransition(State fromState)
        {
            State toState = GetStateGlobal(myTransitionChooser.val);
            if (toState == null)
                return;
            fromState.Transitions.Add(toState);
            fromState.Transitions.Sort(State.SortByNameAscending);
            UIRefresh();
        }

        private void UIRemoveTransition(State fromState, State toRemove)
        {
            fromState.Transitions.Remove(toRemove);
            UIRefresh();
        }

        private void SwitchStateAction(string newState)
        {
            mySwitchState.valNoCallback = string.Empty;
            State st = GetStateGlobal(newState);
            if (st != null)
                SwitchState(st);
            else
                SuperController.LogError("Can't switch to unknown state '" + newState + "'.");
        }

        private void SwitchState(State newState)
        {
            // Store the current (old) state.
            State previousState = myCurrentState;
            
            if (previousState != null)
                previousState.ExitTrigger.Trigger();
            
            myCurrentState = newState;
            
            if (myCurrentState != null)
            {
                myCurrentState.EnterTrigger.Trigger();
                TryPlayTimelineAnimation(myCurrentState.Name);
                TrySetVoxtaFlags(myCurrentState.SetFlags);
                if (routingActive && myCurrentState.InfiniteDuration)
                    myClock = myDuration = 1.0f;
                else if (routingActive)
                    myClock = myDuration = myCurrentState.Duration;
                else if (myCurrentState.InfiniteDuration)
                    myClock = myDuration = 1.0f;
                else
                    myClock = myDuration = myCurrentState.Duration;
                myClockInt = -1;
                myValue = -1.0f;
            }
            
            // If we are not routing and the previous state was the designated VT target, unset nav flag now.
            if (!routingActive && navigationTargetState != null && previousState != null && previousState.Equals(navigationTargetState))
            {
                string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                TrySetVoxtaFlags(finalFlags);
                navigationTargetState = null;
            }
            
            UpdateCurrentStateInfo();
            UpdateCurrentStateValue();
        }

        private void UIRouteSwitchState()
        {
            string targetName = string.IsNullOrEmpty(myRouteSwitchState.val) ? myStateChooser.val : myRouteSwitchState.val;
            RouteSwitchStateAction(targetName);
        }

        private void RouteSwitchStateAction(string targetName)
        {
            myRouteSwitchState.valNoCallback = string.Empty;
            if (myCurrentState == null)
            {
                SuperController.LogError("No current state to route from.");
                return;
            }
            State target = GetStateGlobal(targetName);
            if (target == null)
            {
                SuperController.LogError("Target state '" + targetName + "' not found.");
                return;
            }
            if (myCurrentState.Equals(target))
            {
                SuperController.LogMessage("Already in target state.");
                return;
            }
            List<State> route = FindRoute(myCurrentState, target);
            if (route == null)
            {
                SuperController.LogError("No route found from " + myCurrentState.Name + " to " + targetName + ".");
                return;
            }
            // If the target state is a VT state, record it so that we delay unsetting nav until it’s finished.
            if (target.Name.StartsWith("VT_"))
                navigationTargetState = target;
            else
                navigationTargetState = null;
            
            TrySetVoxtaFlags("nav");
            routingQueue = route;
            routingActive = true;
            if (routingQueue.Count > 0)
            {
                SwitchState(routingQueue[0]);
                routingQueue.RemoveAt(0);
            }
        }

        private List<State> FindRoute(State start, State target)
        {
            Dictionary<State, State> prev = new Dictionary<State, State>();
            Queue<State> queue = new Queue<State>();
            HashSet<State> visited = new HashSet<State>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                State current = queue.Dequeue();
                if (current.Equals(target))
                {
                    List<State> path = new List<State>();
                    State tmp = target;
                    while (!tmp.Equals(start))
                    {
                        path.Add(tmp);
                        tmp = prev[tmp];
                    }
                    path.Reverse();
                    return path;
                }
                foreach (State neighbor in current.Transitions)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        prev[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return null;
        }

        private void InterruptNavigation()
        {
            if (routingActive)
            {
                routingQueue.Clear();
                routingActive = false;
                navigationTargetState = null; // Reset the target state.
                string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                TrySetVoxtaFlags(finalFlags);
                SuperController.LogMessage("Navigation interrupted.");
            }
            else
            {
                SuperController.LogMessage("No active navigation to interrupt.");
            }
        }

        private void UpdateCurrentStateInfo()
        {
            if (myCurrentState != null)
            {
                myCurrentStateInfo.val = "Current State: " + myCurrentState.Name;
                if (myCurrentState.InfiniteDuration)
                    myCurrentClockInfo.val = "Current Clock: Infinite";
                else
                {
                    int clockInt = Mathf.CeilToInt(myClock * 10f);
                    myCurrentClockInfo.val = "Current Clock: " + myClock.ToString("F1") + " sec";
                    myClockInt = clockInt;
                }
            }
            else
            {
                myCurrentStateInfo.val = "Current State: ";
                myCurrentClockInfo.val = "Current Clock: ";
            }
        }

        private void UpdateCurrentStateValue()
        {
            if (myCurrentState != null)
            {
                float v = 0.0f;
                if (!myCurrentState.InfiniteDuration)
                {
                    float fraction = myClock / myDuration;
                    fraction = Mathf.Clamp01(fraction);
                    v = 1.0f - fraction;
                }
                if (!Mathf.Approximately(v, myValue))
                {
                    myCurrentState.ValueTrigger.Trigger(v);
                    myValue = v;
                }
            }
        }

        public void Update()
        {
            for (int i = 0; i < myStates.Count; i++)
            {
                myStates[i].EnterTrigger.Update();
                myStates[i].ValueTrigger.Update();
                myStates[i].ExitTrigger.Update();
            }
            bool uiActive = UITransform.gameObject.activeInHierarchy;
            if (uiActive)
            {
                if (myNeedUIRebuild)
                    UIRebuild();
                if (myNeedUIRefresh)
                    UIRefresh();
            }
            if (myCurrentState == null)
                return;
            // Special handling for any NAV_state: hold indefinitely until "continue_navigation" is triggered.
            if (myCurrentState.Name.StartsWith("NAV_state", System.StringComparison.OrdinalIgnoreCase))
            {
                UpdateCurrentStateInfo();
                UpdateCurrentStateValue();
                return;
            }
            if (routingActive)
            {
                myClock = Mathf.Max(myClock - Time.deltaTime, 0.0f);
                if (myClock <= 0.0f)
                {
                    if (routingQueue.Count > 0)
                    {
                        SwitchState(routingQueue[0]);
                        routingQueue.RemoveAt(0);
                    }
                    else
                    {
                        routingActive = false;
                        // Only clear nav flag immediately if there is no designated VT target.
                        if (navigationTargetState == null)
                        {
                            string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                            string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                            TrySetVoxtaFlags(finalFlags);
                        }
                    }
                }
                UpdateCurrentStateInfo();
                UpdateCurrentStateValue();
                return;
            }
            myClock = Mathf.Max(myClock - Time.deltaTime, 0.0f);
            if (!myCurrentState.InfiniteDuration && myClock <= 0.0f)
                Transition();
            else
            {
                UpdateCurrentStateInfo();
                UpdateCurrentStateValue();
            }
        }

        private void Transition()
        {
            if (myCurrentState == null)
            {
                SwitchState(null);
                return;
            }
            List<State> transitions = myCurrentState.Transitions;
            if (transitions.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, transitions.Count);
                SwitchState(transitions[idx]);
            }
            else
            {
                SwitchState(null);
            }
        }

        // ==========================================================
        // JSON Serialization
        // ==========================================================
        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            JSONClass jc = base.GetJSON(includePhysical, includeAppearance, forceStore);
            if (includePhysical || forceStore)
            {
                needsStore = true;
                JSONArray sclist = new JSONArray();
                for (int i = 0; i < myStates.Count; i++)
                {
                    State st = myStates[i];
                    JSONClass sc = new JSONClass();
                    sc["Name"] = st.Name;
                    sc["InfiniteDuration"].AsBool = st.InfiniteDuration;
                    sc["Duration"].AsFloat = st.Duration;
                    sc["Group"] = st.Group;
                    sc["SetFlags"] = st.SetFlags;
                    if (st.Transitions.Count > 0)
                    {
                        JSONArray tlist = new JSONArray();
                        for (int t = 0; t < st.Transitions.Count; t++)
                            tlist.Add("", st.Transitions[t].Name);
                        sc["Transitions"] = tlist;
                    }
                    sc[st.EnterTrigger.Name] = st.EnterTrigger.GetJSON(subScenePrefix);
                    sc[st.ValueTrigger.Name] = st.ValueTrigger.GetJSON(subScenePrefix);
                    sc[st.ExitTrigger.Name] = st.ExitTrigger.GetJSON(subScenePrefix);
                    sclist.Add("", sc);
                }
                jc["States"] = sclist;
                if (myCurrentState != null)
                {
                    jc["InitialState"] = myCurrentState.Name;
                    jc["Clock"].AsFloat = myClock;
                    jc["Duration"].AsFloat = myDuration;
                }
                // NEW: Save the global exit flags.
                jc["ExitFlags"] = mySetFlagsOnExitNavigation.val;
				jc["SelectedGroup"] = myGroupChooser.val;
            }
            return jc;
        }

        public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
        {
            base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
            if (!physicalLocked && restorePhysical && !IsCustomPhysicalParamLocked("trigger"))
            {
                for (int i = 0; i < myStates.Count; i++)
                {
                    myStates[i].EnterTrigger.Remove();
                    myStates[i].ValueTrigger.Remove();
                    myStates[i].ExitTrigger.Remove();
                }
                myStates.Clear();
                JSONArray sclist = jc["States"].AsArray;
                for (int i = 0; i < sclist.Count; i++)
                {
                    JSONClass sc = sclist[i].AsObject;
                    State st = new State(this, sc["Name"]);
                    st.InfiniteDuration = sc["InfiniteDuration"].AsBool;
                    st.Duration = sc.HasKey("Duration") ? sc["Duration"].AsFloat : 3.0f;
                    st.Group = sc.HasKey("Group") ? sc["Group"].Value : "Group_1";
                    st.SetFlags = sc.HasKey("SetFlags") ? sc["SetFlags"].Value : "";
                    st.EnterTrigger.RestoreFromJSON(sc, subScenePrefix, mergeRestore, setMissingToDefault);
                    st.ValueTrigger.RestoreFromJSON(sc, subScenePrefix, mergeRestore, setMissingToDefault);
                    st.ExitTrigger.RestoreFromJSON(sc, subScenePrefix, mergeRestore, setMissingToDefault);
                    myStates.Add(st);
                }
                myStates.Sort(State.SortByNameAscending);
                UIRebuild();
                for (int i = 0; i < sclist.Count; i++)
                {
                    JSONClass sc = sclist[i].AsObject;
                    State st = GetStateGlobal(sc["Name"]);
                    if (st == null)
                        continue;
                    if (sc.HasKey("Transitions"))
                    {
                        JSONArray tlist = sc["Transitions"].AsArray;
                        for (int t = 0; t < tlist.Count; t++)
                        {
                            State trans = GetStateGlobal(tlist[t]);
                            if (trans != null)
                                st.Transitions.Add(trans);
                        }
                    }
                }
                myCurrentState = jc.HasKey("InitialState") ? GetStateGlobal(jc["InitialState"]) : null;
                myClock = 0.0f;
                myDuration = 1.0f;
                myClockInt = -1;
                myValue = -1.0f;
                if (myCurrentState != null)
                {
                    if (myCurrentState.InfiniteDuration)
                        myClock = myDuration = 1.0f;
                    else
                        myClock = myDuration = myCurrentState.Duration;
                    if (jc.HasKey("Duration"))
                        myDuration = jc["Duration"].AsFloat;
                    if (jc.HasKey("Clock"))
                        myClock = Mathf.Clamp(jc["Clock"].AsFloat, 0f, myDuration);
                }
                UpdateCurrentStateInfo();
                UpdateCurrentStateValue();
                var opts = myStateChooser.choices;
                if (opts.Count == 0)
                    myStateChooser.valNoCallback = "";
                else if (myCurrentState != null)
                    myStateChooser.valNoCallback = myCurrentState.Name;
                else if (!opts.Contains(myStateChooser.val))
                    myStateChooser.valNoCallback = opts[0];
				myGroups.Clear();
				foreach (State s in myStates)
				{
					if (!string.IsNullOrEmpty(s.Group) && !myGroups.Contains(s.Group))
						myGroups.Add(s.Group);
				}
				myGroupChooser.choices = new List<string>(myGroups);
				if (jc.HasKey("SelectedGroup") && myGroups.Contains(jc["SelectedGroup"].Value))
					myGroupChooser.valNoCallback = jc["SelectedGroup"].Value;
				else
					myGroupChooser.valNoCallback = myGroups.Count > 0 ? myGroups[0] : "";
				myNeedUIRefresh = true;
            }
            // NEW: Restore global exit flags if present.
            if (jc.HasKey("ExitFlags"))
            {
                mySetFlagsOnExitNavigation.val = jc["ExitFlags"].Value;
            }
        }

        private State GetSelectedState()
        {
            if (string.IsNullOrEmpty(myStateChooser.val))
                return null;
            return myStates.Find(s => s.Group == myGroupChooser.val && s.Name == myStateChooser.val);
        }

        private State GetState(string name)
        {
            return myStates.Find(s => s.Group == myGroupChooser.val && s.Name == name);
        }

        private State GetStateGlobal(string name)
        {
            return myStates.Find(s => s.Name == name);
        }

        private void OnAtomRename(string oldid, string newid)
        {
            for (int i = 0; i < myStates.Count; i++)
            {
                myStates[i].EnterTrigger.SyncAtomNames();
                myStates[i].ValueTrigger.SyncAtomNames();
                myStates[i].ExitTrigger.SyncAtomNames();
            }
        }

        // Modified to loop until the Timeline plugin is found instead of waiting a fixed time.
		private IEnumerator FindTimelinePlugin()
		{
			while (true)
			{
				foreach (Atom atom in SuperController.singleton.GetAtoms())
				{
					// Check if this atom has the Timeline plugin storable by looking for its identifier.
					string timelineStorableID = atom.GetStorableIDs().FirstOrDefault(x => x.Contains("VamTimeline.AtomPlugin"));
					if (!string.IsNullOrEmpty(timelineStorableID))
					{
						timelineStorable = atom.GetStorableByID(timelineStorableID);
						if (timelineStorable != null)
						{
							SuperController.LogMessage("Timeline plugin found on atom: " + atom.name);
							break;
						}
					}
				}
				if (timelineStorable != null)
					break;
				yield return null; // Wait a frame before checking again
			}
		}

        // Modified to loop until the Voxta plugin is found.
		private IEnumerator FindVoxtaPlugin()
		{
			while (true)
			{
				foreach (Atom atom in SuperController.singleton.GetAtoms())
				{
					// Check for the Voxta plugin storable by its identifier.
					string voxtaStorableID = atom.GetStorableIDs().FirstOrDefault(x => x.Contains("Voxta"));
					if (!string.IsNullOrEmpty(voxtaStorableID))
					{
						voxtaStorable = atom.GetStorableByID(voxtaStorableID);
						if (voxtaStorable != null)
						{
							SuperController.LogMessage("Voxta plugin found on atom: " + atom.name);
							break;
						}
					}
				}
				if (voxtaStorable != null)
					break;
				yield return null; // Wait a frame before trying again
			}
		}

        // Modified to use VaM API to set the flags via the JSONStorable.
        private void TrySetVoxtaFlags(string flags)
        {
            if (string.IsNullOrEmpty(flags))
                return;
            if (voxtaStorable == null)
                return;
            
            JSONStorableString flagsStorable = voxtaStorable.GetStringJSONParam("SetFlags");
            if (flagsStorable != null)
            {
                flagsStorable.val = flags;
                SuperController.LogMessage("Voxta flags set: " + flags);
            }
            else
            {
                SuperController.LogError("Voxta JSONStorable 'SetFlags' not found.");
            }
        }

        private void TryPlayTimelineAnimation(string animationName)
        {
            if (timelineStorable == null)
                return;
            JSONStorableAction playAction = timelineStorable.GetAction("Play " + animationName);
            if (playAction != null && playAction.actionCallback != null)
            {
                playAction.actionCallback.Invoke();
                SuperController.LogMessage($"Playing animation: {animationName}");
                return;
            }
            playAction = timelineStorable.GetAction("Play " + animationName + "/*");
            if (playAction != null && playAction.actionCallback != null)
            {
                playAction.actionCallback.Invoke();
                SuperController.LogMessage($"Playing animation (fallback): {animationName}");
                return;
            }
        }

        private void OnDestroy()
        {
            SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
            for (int i = 0; i < myStates.Count; i++)
            {
                myStates[i].EnterTrigger.Remove();
                myStates[i].ValueTrigger.Remove();
                myStates[i].ExitTrigger.Remove();
            }
            myStates.Clear();
            Utils.OnDestroyUI();
        }

        private void UIRefresh()
        {
            myNeedUIRefresh = false;
            UISetState(myStateChooser.val);
        }
    }
}
