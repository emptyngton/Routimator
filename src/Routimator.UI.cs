// Routimator.UI.cs
// All UI* button/field callbacks, animation chooser rebuild, and display-info updaters.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using MacGruber;

namespace Routimator
{
    public partial class Routimator : MVRScript
    {
        // ====================================================================
        // ANIMATION CHOOSER FIELDS
        // ====================================================================
        private UIDynamicPopup myAnimationPopup;
        private UIDynamicButton createFromAnimButton;
        private UIDynamicButton createForAllAnimsButton;
        private JSONStorableStringChooser myAnimationChooser;
        private List<string> availableAnimations = new List<string>();

        // ====================================================================
        // GROUP CALLBACKS
        // ====================================================================
        private void UISetGroup(string groupName) { ui.UIRebuild(); }

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
                    ui.UIRebuild();
                    return;
                }
            }
            SuperController.LogError("Routimator: Too many groups!");
        }

        private void UIDeleteGroup()
        {
            string currentGroup = myGroupChooser.val;
            List<RoutimatorState.State> statesInGroup = new List<RoutimatorState.State>(stateManager.GetStatesInGroup(currentGroup));
            foreach (RoutimatorState.State stateInGroup in statesInGroup)
                DeregisterNavigateToActionsByName(stateInGroup.Name);
            stateManager.RemoveStatesByGroup(currentGroup);
            myGroups.Remove(currentGroup);
            myGroupChooser.choices = new List<string>(myGroups);
            myGroupChooser.valNoCallback = myGroups.Count > 0 ? myGroups[0] : "";
            ui.UIRebuild();
            RebuildAnimationChooserUI();
        }

        private void UIGroupRename(string newName)
        {
            string oldName = myGroupChooser.val;
            if (string.IsNullOrEmpty(oldName)) return;
            int index = myGroups.IndexOf(oldName);
            if (index < 0) return;
            myGroups[index] = newName;
            myGroupChooser.choices = new List<string>(myGroups);
            myGroupChooser.valNoCallback = newName;
            stateManager.RenameGroup(oldName, newName);
        }

        // ====================================================================
        // STATE EDITOR CALLBACKS
        // ====================================================================
        private void UISetState(string v) { ui.UISetState(v, myGroupChooser.val, stateManager); }

        private void UISetFlags(string newFlags)
        {
            RoutimatorState.State state = stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val);
            if (state == null) return;
            state.SetFlags = newFlags;
        }

        private void UISetWalkingEnabled(bool enabled)
        {
            RoutimatorState.State state = stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val);
            if (state == null) return;
            if (state.IsWalkingEnabled != enabled)
            {
                state.IsWalkingEnabled = enabled;
                MarkAsModified();
                if (graphVisualizer != null && graphVisualizer.IsVisible()) graphVisualizer.UpdateGraph();
            }
        }

        private void UIRenameState(string newName)
        {
            RoutimatorState.State state = stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val);
            if (state == null) return;
            string oldName = state.Name;
            DeregisterNavigateToActionsByName(oldName);
            state.Name = newName;
            RegisterNavigateToActions(state);
            stateManager.SortStates();
            ui.UIRebuild();
            myStateChooser.val = newName;
            RebuildAnimationChooserUI();
        }

        private void UISetFlagsOnExitNavigation(string newFlags) { }

        // ====================================================================
        // STATE LIST MANAGEMENT
        // ====================================================================
        private void UIAddState_ButtonAction() { UIAddState(null); }

        private void UIAddState(RoutimatorState.State source)
        {
            if (myGroups.Count == 0)
            {
                myGroups.Add("Group_1");
                myGroupChooser.choices = new List<string>(myGroups);
                myGroupChooser.valNoCallback = "Group_1";
            }
            for (int i = 1; i < 1000; i++)
            {
                string name = "VS_new_state_" + i;
                bool exists = stateManager.StateExists(myGroupChooser.val, name);
                if (!exists)
                {
                    RoutimatorState.State st = stateManager.CreateState(name, source, myGroupChooser.val);
                    RegisterNavigateToActions(st);
                    ui.UIRebuild();
                    myStateChooser.val = name;
                    TriggerGraphUpdate(true);
                    return;
                }
            }
            SuperController.LogError("Routimator: Too many states!");
        }

        private void UIDuplicateState()
        {
            int idx = myStateChooser.choices.IndexOf(myStateChooser.val);
            if (idx < 0) return;
            RoutimatorState.State orig = stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val);
            string originalName = orig.Name;
            string newName = stateManager.GetNextName(originalName);
            int tries = 0;
            while (stateManager.StateExists(myGroupChooser.val, newName) && tries < 999)
            {
                newName = stateManager.GetNextName(newName);
                tries++;
            }
            RoutimatorState.State duplicate = stateManager.CreateState(newName, orig, myGroupChooser.val);
            RegisterNavigateToActions(duplicate);
            ui.UIRebuild();
            myStateChooser.val = newName;
            TriggerGraphUpdate(true);
        }

        private void UIRemoveState()
        {
            int idx = myStateChooser.choices.IndexOf(myStateChooser.val);
            if (idx < 0) return;
            RoutimatorState.State st = stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val);
            if (myCurrentState == st) SwitchState(null);
            DeregisterNavigateToActionsByName(st.Name);
            stateManager.RemoveState(st);
            ui.UIRebuild();
            TriggerGraphUpdate(false);
            if (myStateChooser.choices.Count > 0) myStateChooser.val = myStateChooser.choices[0];
            else myStateChooser.val = "";
            RebuildAnimationChooserUI();
        }

        private void UIMoveStateUp() { stateManager.MoveStateUp(myGroupChooser.val, myStateChooser.val); ui.UIRebuild(); }
        private void UIMoveStateDown() { stateManager.MoveStateDown(myGroupChooser.val, myStateChooser.val); ui.UIRebuild(); }
        private void UISwitchState() { SwitchState(stateManager.GetSelectedState(myGroupChooser.val, myStateChooser.val)); }

        private void UIRouteSwitchState()
        {
            string targetName = string.IsNullOrEmpty(myRouteSwitchState.val) ? myStateChooser.val : myRouteSwitchState.val;
            // UI button always navigates without the walking route.
            RouteSwitchStateActionInternal(targetName, false);
        }

        // ====================================================================
        // ANIMATION CHOOSER — rebuilt whenever state list changes
        // ====================================================================
        public void RebuildAnimationChooserUI()
        {
            // Step 1: tear down the previous animation UI elements.
            if (myAnimationPopup != null)
            {
                RemovePopup(myAnimationPopup);
                myAnimationPopup = null;
            }
            if (myAnimationChooser != null)
            {
                DeregisterStringChooser(myAnimationChooser);
                myAnimationChooser = null;
            }
            if (createFromAnimButton != null)
            {
                RemoveButton(createFromAnimButton);
                createFromAnimButton = null;
            }
            if (createForAllAnimsButton != null)
            {
                RemoveButton(createForAllAnimsButton);
                createForAllAnimsButton = null;
            }

            // Step 2: collect animations from Timeline that don't already have a matching state.
            List<string> tempAvailable = new List<string>();
            if (timelineIntegration != null)
            {
                List<string> allAnimations = timelineIntegration.GetAllAnimations();
                foreach (string animation in allAnimations)
                {
                    string cleanName = animation;
                    int slashIndex = cleanName.IndexOf("/*");
                    if (slashIndex > 0) cleanName = cleanName.Substring(0, slashIndex);
                    bool stateExists = stateManager.GetStates().Any(s => s.Name == cleanName);
                    if (!stateExists && !tempAvailable.Contains(cleanName))
                        tempAvailable.Add(cleanName);
                }
                tempAvailable.Sort();
            }

            // Step 3: build fresh UI.
            myAnimationChooser = new JSONStorableStringChooser("State From Anim", tempAvailable, tempAvailable.Count > 0 ? tempAvailable[0] : "", "State From Anim");
            myAnimationChooser.displayChoices = tempAvailable;
            myAnimationPopup = CreateFilterablePopup(myAnimationChooser, false);
            RegisterStringChooser(myAnimationChooser);

            createFromAnimButton = Utils.SetupButton(this, "Create From Anim", UICreateStateFromAnimation, false);
            createForAllAnimsButton = Utils.SetupButton(this, "Create For All Anims", UICreateStatesForAllAnimations, false);
        }

        public void UpdateAnimationList()
        {
            // Step 1: compute the new available-animation list.
            availableAnimations.Clear();
            List<string> allAnimations = timelineIntegration.GetAllAnimations();
            foreach (string animation in allAnimations)
            {
                string cleanName = animation;
                int slashIndex = cleanName.IndexOf("/*");
                if (slashIndex > 0) cleanName = cleanName.Substring(0, slashIndex);
                bool stateExists = stateManager.GetStates().Any(s => s.Name == cleanName);
                if (!stateExists && !availableAnimations.Contains(cleanName)) availableAnimations.Add(cleanName);
            }
            availableAnimations.Sort();

            // Step 2: update both `choices` and `displayChoices` in-place — the popup UI holds
            // direct references to these list objects, so replacing them would break the binding.
            myAnimationChooser.choices.Clear();
            myAnimationChooser.choices.AddRange(availableAnimations);
            myAnimationChooser.displayChoices.Clear();
            myAnimationChooser.displayChoices.AddRange(availableAnimations);

            // Step 3: if the current selection is gone, pick a new default; otherwise reassign to
            // force the popup text to refresh (e.g., when it was previously empty).
            if (!myAnimationChooser.choices.Contains(myAnimationChooser.val))
                myAnimationChooser.val = (myAnimationChooser.choices.Count > 0) ? myAnimationChooser.choices[0] : "";
            else
                myAnimationChooser.val = myAnimationChooser.val;
        }

        public void UICreateStateFromAnimation()
        {
            string animationName = myAnimationChooser.val;
            if (string.IsNullOrEmpty(animationName))
            {
                Logger.Log("No animation selected!");
                return;
            }
            string newStateName = animationName;
            if (stateManager.StateExists(myGroupChooser.val, newStateName))
            {
                for (int i = 1; i < 100; i++)
                {
                    string testName = newStateName + "_" + i;
                    if (!stateManager.StateExists(myGroupChooser.val, testName))
                    {
                        newStateName = testName;
                        break;
                    }
                }
            }
            RoutimatorState.State newState = stateManager.CreateState(newStateName, null, myGroupChooser.val);
            RegisterNavigateToActions(newState);
            RebuildAnimationChooserUI();
            ui.UIRebuild();
            myStateChooser.val = newStateName;
            TriggerGraphUpdate(true);
        }

        public void UICreateStatesForAllAnimations()
        {
            Logger.Log("UICreateStatesForAllAnimations called");
            if (availableAnimations == null || availableAnimations.Count == 0)
            {
                Logger.Log("No animations available!");
                return;
            }
            int createdCount = 0;
            string currentGroup = myGroupChooser.val;
            foreach (string animationName in availableAnimations)
            {
                if (string.IsNullOrEmpty(animationName)) continue;
                if (stateManager.StateExists(currentGroup, animationName))
                {
                    Logger.Log($"State '{animationName}' already exists, skipping");
                    continue;
                }
                RoutimatorState.State newState = stateManager.CreateState(animationName, null, currentGroup);
                RegisterNavigateToActions(newState);
                createdCount++;
            }
            if (createdCount > 0)
            {
                ui.UIRebuild();
                TriggerGraphUpdate(true);
                Logger.Log($"Created {createdCount} new states from animations");
            }
            else
            {
                Logger.Log("No new states created (all animations already have states)");
            }
            RebuildAnimationChooserUI();
        }

        // ====================================================================
        // DISPLAY-INFO UPDATERS — called from Update() / state transitions
        // ====================================================================
        private void UpdateCurrentStateInfo()
        {
            if (myCurrentState != null) myCurrentStateInfo.val = "Current State: " + myCurrentState.Name;
            else myCurrentStateInfo.val = "Current State: ";
        }

        private void UpdateCurrentStateValue()
        {
            if (myCurrentState != null)
            {
                float v = 0.0f;
                if (navigation.GetCurrentDuration() > 0 && !float.IsPositiveInfinity(navigation.GetCurrentDuration()))
                {
                    float fraction = navigation.GetCurrentClock() / navigation.GetCurrentDuration();
                    fraction = Mathf.Clamp01(fraction);
                    v = 1.0f - fraction;
                }
                if (!Mathf.Approximately(v, navigation.GetCurrentValue()))
                {
                    myCurrentState.ValueTrigger.Trigger(v);
                    navigation.SetCurrentValue(v);
                }
            }
        }

        private void UpdatePluginOperationStatusInfo()
        {
            if (myPluginOperationStatusInfo == null) return;
            string statusText = "Status: ";
            if (currentOperationState == RoutimatorOperationStates.IDLE)
            {
                statusText += "Idle";
            }
            else if (currentOperationState == RoutimatorOperationStates.NAVIGATING)
            {
                string navTargetName = overallNavigationTarget?.Name ?? navigation.GetNavigationTargetState()?.Name ?? "Unknown";
                statusText += "Navigating to " + navTargetName;
            }
            else if (currentOperationState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
            {
                string walkStateName = stateAwaitingWalkFinish?.Name ?? "Unknown";
                string overallTargetName = overallNavigationTarget?.Name ?? "final target";
                statusText += "Waiting for walk (at " + walkStateName + ", towards " + overallTargetName + ")";
            }
            else
            {
                statusText += "Unknown (" + currentOperationState + ")";
            }
            myPluginOperationStatusInfo.val = statusText;
        }
    }
}
