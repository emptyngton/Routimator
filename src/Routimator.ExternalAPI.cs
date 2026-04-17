// Routimator.ExternalAPI.cs
// Surfaces intended for other plugins and the graph visualizer:
//   - Listener registration + the WAITING_FOR_WALK_FINISH broadcast.
//   - Graph-side transition add/remove bridges (called from RoutimatorGraph).
//   - TriggerGraphUpdate convenience for code that mutates state list / transitions.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Routimator
{
    public partial class Routimator : MVRScript
    {
        // Other plugins register themselves here to receive OnRoutimatorWaitingForWalkFinish.
        private List<MVRScript> registeredListeners = new List<MVRScript>();

        // ====================================================================
        // LISTENER REGISTRATION — called by external plugins (e.g., walking controller)
        // ====================================================================
        public void RegisterRoutimatorListener(MVRScript listener)
        {
            if (listener != null && !registeredListeners.Contains(listener))
            {
                registeredListeners.Add(listener);
                Logger.Log("Listener " + listener.storeId + " on atom " + listener.containingAtom.uid + " registered.");
            }
        }

        public void UnregisterRoutimatorListener(MVRScript listener)
        {
            if (listener != null && registeredListeners.Contains(listener))
            {
                registeredListeners.Remove(listener);
                Logger.Log("Listener " + listener.storeId + " on atom " + listener.containingAtom.uid + " unregistered.");
            }
        }

        // ====================================================================
        // BROADCAST — fired when routing pauses at a walking state.
        //
        // Listeners receive an OnRoutimatorWaitingForWalkFinish SendMessage with
        // (this, walkingState) as args. They drive the character to its destination and
        // fire WalkingFinishedTrigger to resume navigation.
        // ====================================================================
        private void BroadcastWaitingForWalkFinish(RoutimatorState.State walkingState)
        {
            Logger.Log("BroadcastWaitingForWalkFinish");
            object[] args = new object[] { this, walkingState };
            List<MVRScript> listenersCopy = new List<MVRScript>(registeredListeners);

            foreach (MVRScript listenerScript in listenersCopy)
            {
                try
                {
                    if (listenerScript == null || listenerScript.containingAtom == null || !listenerScript.enabled)
                    {
                        registeredListeners.Remove(listenerScript);
                        Logger.Log("Removed inactive/destroyed listener during broadcast: " + (listenerScript != null ? listenerScript.storeId : "Unknown"));
                        continue;
                    }
                    listenerScript.SendMessage("OnRoutimatorWaitingForWalkFinish", args, SendMessageOptions.DontRequireReceiver);
                }
                catch (Exception e)
                {
                    SuperController.LogError("Routimator: Error preparing SendMessage for OnRoutimatorWaitingForWalkFinish to listener " + listenerScript.storeId + ": " + e);
                }
            }
        }

        // ====================================================================
        // GRAPH BRIDGES — called when the user adds/removes a transition by dragging in the graph
        // ====================================================================
        public void AddTransitionFromGraph(RoutimatorState.State sourceState, RoutimatorState.State targetState)
        {
            if (sourceState == null || targetState == null || stateManager == null)
            {
                SuperController.LogError("Routimator: Invalid state(s) for transition addition from graph.");
                return;
            }
            if (sourceState.Transitions.Contains(targetState)) return;
            sourceState.Transitions.Add(targetState);
            sourceState.Transitions.Sort(RoutimatorState.State.SortByNameAscending);
            needsStore = true;
            RefreshUIIfNeeded(sourceState);
        }

        public void RemoveTransitionFromGraph(RoutimatorState.State sourceState, RoutimatorState.State targetState)
        {
            if (sourceState == null || targetState == null || stateManager == null)
            {
                SuperController.LogError("Routimator: Invalid state(s) for transition removal from graph.");
                return;
            }
            if (!sourceState.Transitions.Contains(targetState)) return;
            sourceState.Transitions.Remove(targetState);
            needsStore = true;
            RefreshUIIfNeeded(sourceState);
        }

        private void RefreshUIIfNeeded(RoutimatorState.State sourceState)
        {
            if (ui != null)
            {
                if (myStateChooser.val == sourceState.Name && myGroupChooser.val == sourceState.Group)
                    ui.UIRefresh();
            }
        }

        // ====================================================================
        // GRAPH UPDATE CONVENIENCE
        // ====================================================================
        public void TriggerGraphUpdate(bool fitView = false)
        {
            if (graphVisualizer != null && graphVisualizer.IsVisible())
            {
                graphVisualizer.UpdateGraph();
                if (fitView)
                    graphVisualizer.FitGraphToView();
            }
        }
    }
}
