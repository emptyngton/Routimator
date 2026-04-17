// Routimator.Core.cs
// Namespace-level types (Logger, RoutimatorOperationStates), manager fields, Init + InitializeXxx
// helpers, Unity lifecycle (Update / OnDestroy / OnAtomRename), public getters.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using MacGruber;
using MVR.FileManagementSecure;
using System;

namespace Routimator
{
    public static class Logger
    {
        public static bool IsDebugLoggingEnabled = false;

        public static void Log(string message)
        {
            if (IsDebugLoggingEnabled)
            {
                SuperController.LogMessage("Routimator: " + message);
            }
        }
    }

    // String constants — let external plugins compare operation state by value.
    public static class RoutimatorOperationStates
    {
        public const string IDLE = "Idle";
        public const string NAVIGATING = "Navigating";
        public const string WAITING_FOR_WALK_FINISH = "WaitingForWalkFinish";
    }

    public partial class Routimator : MVRScript
    {
        // ====================================================================
        // COMPOSED SUBSYSTEMS
        // ====================================================================
        private RoutimatorUI ui;
        public RoutimatorState stateManager { get; private set; }
        public RoutimatorNavigation navigation { get; private set; }
        private RoutimatorGraph graphVisualizer;
        private RoutimatorTimeline timelineIntegration;
        private RoutimatorVoxta voxtaIntegration;
        private RoutimatorSerialization serialization;

        // ====================================================================
        // GROUPS
        // ====================================================================
        private JSONStorableStringChooser myGroupChooser;
        private List<string> myGroups = new List<string>();

        // ====================================================================
        // STATE CHOOSERS & SELECTED-STATE EDITOR
        // ====================================================================
        private JSONStorableStringChooser myStateChooser;
        private JSONStorableString myCurrentStateInfo;
        private JSONStorableString myPluginOperationStatusInfo;
        private JSONStorableString mySelectedStateInfo;
        private JSONStorableString myGroupName;
        private JSONStorableString myStateName;
        private JSONStorableFloat myStateDuration;
        private JSONStorableBool myStateIsWalkingEnabled;

        private JSONStorableString myStateSetFlags;
        private JSONStorableString mySetFlagsOnExitNavigation;

        private JSONStorableStringChooser myTransitionChooser;

        // ====================================================================
        // EXTERNAL TRIGGERS (JSONStorables callable by other plugins / Voxta)
        // ====================================================================
        private JSONStorableString mySwitchState;
        private JSONStorableString myRouteSwitchState;
        private JSONStorableString myInterruptNavigation;
        private JSONStorableString myContinueNavigation;
        private JSONStorableAction walkingFinishedTrigger;
        private JSONStorableBool enableDebugLogging;

        // ====================================================================
        // RUNTIME STATE
        // ====================================================================
        private RoutimatorState.State myCurrentState = null;
        private string currentOperationState = RoutimatorOperationStates.IDLE;
        private List<RoutimatorState.State> pendingRouteSegments = new List<RoutimatorState.State>();
        private RoutimatorState.State stateAwaitingWalkFinish = null;
        private RoutimatorState.State overallNavigationTarget = null;

        // ====================================================================
        // INIT — composition root
        // ====================================================================
        public override void Init()
        {
            ui = new RoutimatorUI(this);
            stateManager = new RoutimatorState(this);
            navigation = new RoutimatorNavigation(this, stateManager);
            timelineIntegration = new RoutimatorTimeline(this);
            voxtaIntegration = new RoutimatorVoxta(this);
            serialization = new RoutimatorSerialization(this, stateManager);

            Utils.OnInitUI(CreateUIElement);
            InitializeBasicUI();
            InitializeGroups();
            InitializeActionsAndTriggers();
            InitializeStateChooserUI();
            RebuildAnimationChooserUI();

            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
            SimpleTriggerHandler.LoadAssets();

            // FindTimelinePlugin asynchronously refreshes the animation list once Timeline is found.
            StartCoroutine(timelineIntegration.FindTimelinePlugin());
            StartCoroutine(voxtaIntegration.FindVoxtaPlugin());

            InitializeSaveLoadFunctionality();

            UpdatePluginOperationStatusInfo();

            enableDebugLogging = new JSONStorableBool("Enable Debug Logging", false, (bool val) => { Logger.IsDebugLoggingEnabled = val; });
            RegisterBool(enableDebugLogging);
            Logger.IsDebugLoggingEnabled = enableDebugLogging.val;
        }

        public void MarkAsModified()
        {
            needsStore = true;
        }

        // ====================================================================
        // UI INITIALIZATION HELPERS — called from Init()
        // ====================================================================
        private void InitializeBasicUI()
        {
            myCurrentStateInfo = new JSONStorableString("CurrentStateInfo", "Current State: ");
            Utils.SetupInfoOneLine(this, myCurrentStateInfo, false);

            myPluginOperationStatusInfo = new JSONStorableString("PluginOperationStatus", "Status: Idle");
            Utils.SetupInfoOneLine(this, myPluginOperationStatusInfo, false);

            if (!IsVR())
            {
                InitializeGraphVisualizer();
            }

            mySetFlagsOnExitNavigation = new JSONStorableString("SetFlagsOnExitNavigation", "", UISetFlagsOnExitNavigation);
            RegisterString(mySetFlagsOnExitNavigation);

            UIDynamicLabelInput exitFlagsInput = Utils.SetupTextInput(this, "Exit Flags", mySetFlagsOnExitNavigation, false);
        }

        private void InitializeStateChooserUI()
        {
            Utils.SetupSpacer(this, 10, false);
            Utils.SetupInfoOneLine(this, "<size=30><b>State</b></size>", false);

            myStateChooser = new JSONStorableStringChooser("Selected State", new List<string>(), string.Empty, "Selected State");
            myStateChooser.displayChoices = new List<string>();
            myStateChooser.setCallbackFunction += UISetState;
            CreateFilterablePopup(myStateChooser, false);

            myStateName = new JSONStorableString("Name", "", UIRenameState);
            RegisterString(myStateName);
            UIDynamicLabelInput stateNameInput = Utils.SetupTextInput(this, "State Name", myStateName, false);

            Utils.SetupTwinButton(this, "▲", UIMoveStateUp, "▼", UIMoveStateDown, false);

            // Animation-related UI is built in RebuildAnimationChooserUI.

            Utils.SetupTwinButton(this, "Add New State", UIAddState_ButtonAction, "Duplicate State", UIDuplicateState, false);

            Utils.SetupButton(this, "Remove State", UIRemoveState, false);

            myStateSetFlags = new JSONStorableString("SetFlags", "", UISetFlags);
            RegisterString(myStateSetFlags);
            UIDynamicLabelInput setFlagsInput = Utils.SetupTextInput(this, "Set Flags", myStateSetFlags, false);

            myStateIsWalkingEnabled = new JSONStorableBool("Walking enabled", false, UISetWalkingEnabled);
            RegisterBool(myStateIsWalkingEnabled);
            CreateToggle(myStateIsWalkingEnabled, false);

            Utils.SetupSpacer(this, 5, false);
            Utils.SetupButton(this, "<b>Activate Selected State</b>", UISwitchState, false);

            // UI button uses default routing (without walking).
            myRouteSwitchState = new JSONStorableString("RouteSwitchState", "", RouteSwitchStateAction);
            myRouteSwitchState.isStorable = myRouteSwitchState.isRestorable = false;
            RegisterString(myRouteSwitchState);
            Utils.SetupButton(this, "<b>Navigate to Selected State</b>", UIRouteSwitchState, false);
            Utils.SetupButton(this, "<b>Interrupt Navigation</b>", InterruptNavigation, false);
        }

        private void InitializeGroups()
        {
            Utils.SetupSpacer(this, 10, false);
            Utils.SetupInfoOneLine(this, "<size=30><b>Groups</b></size>", false);

            if (myGroups.Count == 0)
                myGroups.Add("Group_1");

            myGroupChooser = new JSONStorableStringChooser("Selected Group", myGroups, myGroups[0], "Selected Group");
            myGroupChooser.setCallbackFunction += UISetGroup;
            CreateFilterablePopup(myGroupChooser, false);

            myGroupName = new JSONStorableString("GroupName", myGroupChooser.val, UIGroupRename);
            RegisterString(myGroupName);
            UIDynamicLabelInput groupNameInput = Utils.SetupTextInput(this, "Name", myGroupName, false);

            Utils.SetupTwinButton(this, "Add New Group", UIAddGroup, "Delete Group", UIDeleteGroup, false);
        }

        private void InitializeActionsAndTriggers()
        {
            myTransitionChooser = new JSONStorableStringChooser("Transition", new List<string>(), "", "Transition to Add");
            myTransitionChooser.displayChoices = new List<string>();

            mySwitchState = new JSONStorableString("SwitchState", "", SwitchStateAction);
            mySwitchState.isStorable = mySwitchState.isRestorable = false;
            RegisterString(mySwitchState);

            myInterruptNavigation = new JSONStorableString("InterruptNavigation", "", InterruptNavigationAction);
            myInterruptNavigation.isStorable = myInterruptNavigation.isRestorable = false;
            RegisterString(myInterruptNavigation);

            myContinueNavigation = new JSONStorableString("ContinueNavigation", "", ContinueNavigationAction);
            myContinueNavigation.isStorable = myContinueNavigation.isRestorable = false;
            RegisterString(myContinueNavigation);

            walkingFinishedTrigger = new JSONStorableAction("WalkingFinishedTrigger", OnWalkingFinishedTrigger);
            RegisterAction(walkingFinishedTrigger);

            JSONStorableAction extInterruptNavigation = new JSONStorableAction("InterruptNavigationTrigger", InterruptNavigation);
            RegisterAction(extInterruptNavigation);

            JSONStorableAction extContinueNavigation = new JSONStorableAction("ContinueNavigationTrigger", () => ContinueNavigationAction("continue_navigation_direct_trigger"));
            RegisterAction(extContinueNavigation);
        }

        private void InitializeGraphVisualizer()
        {
            graphVisualizer = new RoutimatorGraph(this);
            UIDynamicButton graphButton = Utils.SetupButton(this, "<b>Show State Graph</b>", ToggleStateGraph, false);
        }

        private void ToggleStateGraph() { graphVisualizer.ToggleGraphVisibility(); }

        // ====================================================================
        // UNITY LIFECYCLE — Update (per-frame tick)
        // ====================================================================
        public void Update()
        {
            stateManager.UpdateTriggers();
            bool uiActive = UITransform.gameObject.activeInHierarchy;
            if (uiActive)
            {
                if (ui.NeedsRebuild()) ui.UIRebuild();
                if (ui.NeedsRefresh()) ui.UIRefresh();
            }

            if (myCurrentState != null)
            {
                if (!float.IsPositiveInfinity(navigation.GetCurrentDuration()))
                {
                    navigation.UpdateClock();
                }
            }
            UpdateCurrentStateInfo();
            UpdateCurrentStateValue();

            if (graphVisualizer != null && graphVisualizer.IsVisible())
            {
                GameObject graphCanvas = graphVisualizer.GetCanvas();
                if (graphCanvas != null)
                {
                    bool screenshotActive = (SuperController.singleton.screenshotCamera != null && SuperController.singleton.screenshotCamera.enabled) ||
                                            (SuperController.singleton.hiResScreenshotCamera != null && SuperController.singleton.hiResScreenshotCamera.enabled);

                    if (SuperController.singleton.HubOpen || SuperController.singleton.worldUIActivated || screenshotActive)
                        graphCanvas.SetActive(false);
                    else
                        graphCanvas.SetActive(true);
                }
            }
        }

        private void OnAtomRename(string oldid, string newid) { stateManager.SyncAtomNames(); }

        // ====================================================================
        // UNITY LIFECYCLE — OnDestroy (NOTE: no `override` — MVRScript doesn't mark it virtual)
        // ====================================================================
        private void OnDestroy()
        {
            SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;

            // Deregister all navigation actions when destroying the plugin.
            // Copy keys to avoid collection-modified-during-iteration errors.
            List<string> actionKeysToDeregister = new List<string>(stateNavigationActions.Keys);
            foreach (string actionKey in actionKeysToDeregister)
            {
                JSONStorableAction actionToDeregister = stateNavigationActions[actionKey];
                if (actionToDeregister != null && GetAction(actionToDeregister.name) != null)
                    DeregisterAction(actionToDeregister);
            }
            stateNavigationActions.Clear();

            if (stateManager != null) stateManager.CleanupStates();
            if (graphVisualizer != null && graphVisualizer.IsVisible()) graphVisualizer.HideGraph();
            Utils.OnDestroyUI();

            registeredListeners.Clear();
        }

        public static bool IsVR()
        {
            return SuperController.singleton.isOpenVR || SuperController.singleton.isOVR;
        }

        // ====================================================================
        // PUBLIC GETTERS — used by manager classes to read plugin state
        // ====================================================================
        public JSONStorableStringChooser GetGroupChooser() { return myGroupChooser; }
        public JSONStorableStringChooser GetStateChooser() { return myStateChooser; }
        public JSONStorableStringChooser GetTransitionChooser() { return myTransitionChooser; }
        public JSONStorableString GetStateName() { return myStateName; }
        public JSONStorableFloat GetStateDuration() { return myStateDuration; }
        public JSONStorableString GetStateSetFlags() { return myStateSetFlags; }
        public JSONStorableString GetSelectedStateInfo() { return mySelectedStateInfo; }
        public List<string> GetGroups() { return myGroups; }
        public JSONStorableString GetGroupName() { return myGroupName; }
        public RoutimatorState.State GetCurrentState() { return myCurrentState; }
        public JSONStorableString GetSetFlagsOnExitNavigation() { return mySetFlagsOnExitNavigation; }
        public JSONStorableBool GetStateIsWalkingEnabled() { return myStateIsWalkingEnabled; }
        public string GetCurrentOperationState() { return currentOperationState; }
    }
}
