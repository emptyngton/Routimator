// Routimator.Routing.cs
// Route planning and navigation orchestration:
//   - RouteSwitchStateAction / Internal — resolve a path, then ProcessRoute it.
//   - ProcessRoute — split a route around walking states, feed the first segment to Timeline.
//   - Interrupt / Continue navigation (from UI and external string commands).
//   - OnWalkingFinishedTrigger — resumes navigation after an external walk completes.
//   - OnTimelineEvent — receives Timeline playback events and advances the routing queue
//     in lockstep. (Lives here rather than in a "timeline bridge" file because ~90% of its
//     body is routing logic; only the outer event-dispatch is Timeline-protocol.)
//   - RegisterNavigateToActions / Deregister — per-state "Navigate to <State>" and
//     "Walk and Navigate to <State>" JSONStorableActions callable by external plugins.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Routimator
{
    public partial class Routimator : MVRScript
    {
        // Cache of per-state navigation actions we've registered with VAM; keyed by action name
        // ("Navigate to <StateName>" / "Walk and Navigate to <StateName>").
        private Dictionary<string, JSONStorableAction> stateNavigationActions = new Dictionary<string, JSONStorableAction>();

        // ====================================================================
        // ROUTE ENTRY POINTS
        // ====================================================================
        // Public wrapper used by the JSONStorableString (e.g., from external plugin / UI string).
        public void RouteSwitchStateAction(string targetName)
        {
            // Default: navigate without forcing a walking-state detour.
            RouteSwitchStateActionInternal(targetName, false);
        }

        private void RouteSwitchStateActionInternal(string targetName, bool useWalkingRoute)
        {
            myRouteSwitchState.valNoCallback = string.Empty;
            if (myCurrentState == null)
            {
                SuperController.LogError("Routimator: No current state to route from. Activate a state first.");
                SetOperationState(RoutimatorOperationStates.IDLE);
                return;
            }

            RoutimatorState.State targetState = stateManager.GetStateGlobal(targetName);
            if (targetState == null)
            {
                SuperController.LogError("Routimator: Target state '" + targetName + "' not found.");
                SetOperationState(RoutimatorOperationStates.IDLE);
                return;
            }

            // Already at target? If walking was requested and current state isn't a walking
            // state, we fall through and look for a loop via a walking state. Otherwise no-op.
            if (myCurrentState.Equals(targetState))
            {
                if (useWalkingRoute && !myCurrentState.IsWalkingEnabled)
                {
                    Logger.Log("Already in target state, but it's not a walking state and walking was requested. Finding loop.");
                }
                else
                {
                    Logger.Log("Already in target state and conditions met (or walking not requested). No navigation needed.");
                    return;
                }
            }

            if (currentOperationState != RoutimatorOperationStates.IDLE)
            {
                Logger.Log("New navigation requested, interrupting current operation: " + currentOperationState);
                InterruptNavigationLogic();
                timelineIntegration.NotifyTimelineNavigationInterrupted();
                voxtaIntegration.TrySetVoxtaFlags("!nav");
            }

            List<RoutimatorState.State> routeForProcessing;
            if (useWalkingRoute)
            {
                Logger.Log("Finding route WITH walking to " + targetName);
                routeForProcessing = navigation.FindRouteWithWalking(myCurrentState, targetState);
            }
            else
            {
                Logger.Log("Finding route WITHOUT walking to " + targetName);
                routeForProcessing = navigation.FindRoute(myCurrentState, targetState);
            }

            if (routeForProcessing == null || routeForProcessing.Count == 0)
            {
                string routeType = useWalkingRoute ? "walking" : "standard";
                SuperController.LogError("Routimator: No " + routeType + " route found from " + myCurrentState.Name + " to " + targetName + ".");
                SetOperationState(RoutimatorOperationStates.IDLE);
                return;
            }

            // `routeForProcessing` contains the steps to execute (from the step after myCurrentState
            // up to targetState, inclusive). Special case: [myCurrentState] alone means start==target
            // and conditions are met — no steps to execute, ProcessRoute will handle it with an
            // empty list and transition to IDLE.
            if (routeForProcessing.Count == 1 && routeForProcessing[0].Equals(myCurrentState) && myCurrentState.Equals(targetState))
            {
                Logger.Log("Route is just the current state, which is also the target. No steps to process.");
                overallNavigationTarget = targetState;
                navigation.SetNavigationTargetState(targetState);
                ProcessRoute(new List<RoutimatorState.State>());
                return;
            }

            if (routeForProcessing.Count == 0 && !myCurrentState.Equals(targetState))
            {
                SuperController.LogError("Routimator: Route became empty after attempting to remove current state, but target is different. This should not happen.");
                SetOperationState(RoutimatorOperationStates.IDLE);
                return;
            }

            overallNavigationTarget = targetState;
            navigation.SetNavigationTargetState(targetState);
            voxtaIntegration.TrySetVoxtaFlags("nav");

            ProcessRoute(routeForProcessing);
        }

        // ====================================================================
        // PROCESS ROUTE
        //
        // Splits the route around the first walking state: everything up to and including the
        // walking state is sent as a segment to Timeline; anything after is queued as
        // `pendingRouteSegments` and resumed later when `OnWalkingFinishedTrigger` fires.
        //
        // Called with an empty list to wrap up navigation (check whether we've reached the
        // overall target, fire `!nav`, transition to IDLE).
        // ====================================================================
        private void ProcessRoute(List<RoutimatorState.State> routeSegment)
        {
            if (routeSegment == null || routeSegment.Count == 0)
            {
                Logger.Log("ProcessRoute called with empty segment or no more segments to process.");
                if (myCurrentState != null && overallNavigationTarget != null && myCurrentState.Equals(overallNavigationTarget))
                {
                    Logger.Log("Reached overall navigation target: " + overallNavigationTarget.Name + " after processing all route segments.");
                    string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                    string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                    voxtaIntegration.TrySetVoxtaFlags(finalFlags);
                    SetOperationState(RoutimatorOperationStates.IDLE);
                }
                else if (currentOperationState != RoutimatorOperationStates.IDLE)
                {
                    Logger.Log("Route processing ended, but overall target not reached or not set. Setting to IDLE.");
                    SetOperationState(RoutimatorOperationStates.IDLE);
                }
                pendingRouteSegments.Clear();
                stateAwaitingWalkFinish = null;
                navigation.SetRoutingActive(false);
                return;
            }

            List<RoutimatorState.State> segmentToSendToTimeline = new List<RoutimatorState.State>();
            pendingRouteSegments.Clear();
            stateAwaitingWalkFinish = null;

            int walkingStateIndex = -1;
            for (int i = 0; i < routeSegment.Count; i++)
            {
                if (routeSegment[i].IsWalkingEnabled)
                {
                    walkingStateIndex = i;
                    break;
                }
            }

            if (walkingStateIndex != -1)
            {
                for (int i = 0; i <= walkingStateIndex; i++)
                    segmentToSendToTimeline.Add(routeSegment[i]);

                if (walkingStateIndex < routeSegment.Count - 1)
                {
                    for (int i = walkingStateIndex + 1; i < routeSegment.Count; i++)
                        pendingRouteSegments.Add(routeSegment[i]);
                }
                stateAwaitingWalkFinish = routeSegment[walkingStateIndex];
                Logger.Log("Segment includes walking state: '" + stateAwaitingWalkFinish.Name + "'. Sending up to this state. Pending segments after walk: " + pendingRouteSegments.Count);
            }
            else
            {
                segmentToSendToTimeline.AddRange(routeSegment);
                Logger.Log("No walking states in current segment. Sending entire segment. Pending segments after this: " + pendingRouteSegments.Count);
            }

            if (segmentToSendToTimeline.Count > 0)
            {
                SetOperationState(RoutimatorOperationStates.NAVIGATING);
                navigation.SetRoutingQueue(new List<RoutimatorState.State>(segmentToSendToTimeline));
                navigation.SetRoutingActive(true);
                timelineIntegration.SendRouteToTimeline(segmentToSendToTimeline);
            }
            else
            {
                SuperController.LogError("Routimator: ProcessRoute - segmentToSendToTimeline is empty. This should not happen if routeSegment was not empty.");
                SetOperationState(RoutimatorOperationStates.IDLE);
                navigation.SetRoutingActive(false);
            }
        }

        // ====================================================================
        // INTERRUPT & CONTINUE
        // ====================================================================
        private void InterruptNavigation()
        {
            Logger.Log("Interrupt Navigation button pressed.");
            InterruptNavigationLogic();
            string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
            string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
            voxtaIntegration.TrySetVoxtaFlags(finalFlags);
            timelineIntegration.NotifyTimelineNavigationInterrupted();
        }

        private void InterruptNavigationLogic()
        {
            if (currentOperationState != RoutimatorOperationStates.IDLE)
                Logger.Log("Interrupting current operation: " + currentOperationState);
            else
                Logger.Log("No active operation to interrupt.");
            navigation.ClearRoutingQueue();
            navigation.SetRoutingActive(false);
            pendingRouteSegments.Clear();
            stateAwaitingWalkFinish = null;
            overallNavigationTarget = null;
            SetOperationState(RoutimatorOperationStates.IDLE);
            navigation.SetNavigationTargetState(null);
            if (graphVisualizer != null && graphVisualizer.IsVisible())
                graphVisualizer.UpdateHighlights();
        }

        private void InterruptNavigationAction(string val)
        {
            if (!string.IsNullOrEmpty(val) && val.Equals("interrupt_navigation", System.StringComparison.OrdinalIgnoreCase))
                InterruptNavigation();
            myInterruptNavigation.val = "";
        }

        private void ContinueNavigationAction(string val)
        {
            if (!string.IsNullOrEmpty(val) &&
                (val.Equals("continue_navigation", System.StringComparison.OrdinalIgnoreCase) ||
                 val.Equals("continue_navigation_direct_trigger", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (currentOperationState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
                {
                    Logger.Log("'ContinueNavigation' (legacy) called while WAITING_FOR_WALK_FINISH. Triggering 'WalkingFinishedTrigger' instead.");
                    OnWalkingFinishedTrigger();
                }
                else if (myCurrentState != null && myCurrentState.Name.StartsWith("NAV_state", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (navigation.IsRoutingActive() && navigation.GetRoutingQueue().Count > 0)
                        timelineIntegration.SendRouteToTimeline(navigation.GetRoutingQueue());
                    else if (!navigation.IsRoutingActive() && myCurrentState.Transitions.Count > 0)
                        navigation.Transition(myCurrentState);
                }
                else
                {
                    Logger.Log("Continue navigation command received but not applicable in current state: " + currentOperationState);
                }
            }
            myContinueNavigation.val = "";
        }

        // ====================================================================
        // WALKING-FINISHED HANDSHAKE
        //
        // When a segment's last state is a walking state, Routimator transitions to
        // WAITING_FOR_WALK_FINISH and broadcasts to listener plugins. Those plugins drive the
        // character to the destination, then fire this action to resume the next segment.
        // ====================================================================
        private void OnWalkingFinishedTrigger()
        {
            Logger.Log("WalkingFinishedTrigger received. Current OpState: " + currentOperationState);
            if (currentOperationState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
            {
                Logger.Log("Was waiting for walk. Resuming navigation with " + pendingRouteSegments.Count + " pending segments.");
                stateAwaitingWalkFinish = null;
                ProcessRoute(new List<RoutimatorState.State>(pendingRouteSegments));
            }
            else
            {
                Logger.Log("Received WalkingFinishedTrigger but was not in WAITING_FOR_WALK_FINISH state. Current OpState: " + currentOperationState + ". Ignoring.");
            }
        }

        // ====================================================================
        // TIMELINE EVENT HANDLER — receives `SendClipPlaybackStarted` / `SendAnimationQueueFinished`
        // via SendMessage, and advances the routing queue in lockstep with Timeline playback.
        // ====================================================================
        public void OnTimelineEvent(object[] e)
        {
            if (e == null || e.Length == 0) return;
            string eventName = e[0] as string;
            if (string.IsNullOrEmpty(eventName)) return;

            string logClipName = "N/A";
            if (e.Length > 1 && e[1] is string) logClipName = (string)e[1];
            Logger.Log($"Timeline event: {eventName}, Arg1: {logClipName}, OpState: {currentOperationState}, RoutingActive: {navigation.IsRoutingActive()}");

            if (eventName == "SendClipPlaybackStarted")
            {
                string clipNameFromTimeline = null;
                if (e.Length > 1 && e[1] is string)
                    clipNameFromTimeline = (string)e[1];

                if (clipNameFromTimeline != null)
                {
                    if (navigation.IsRoutingActive() && navigation.GetRoutingQueue().Count > 0)
                    {
                        RoutimatorState.State expectedNextStateInQueue = navigation.GetRoutingQueue()[0];

                        if (clipNameFromTimeline == expectedNextStateInQueue.Name)
                        {
                            Logger.Log($"Timeline playing expected state: {clipNameFromTimeline}");
                            SwitchState(expectedNextStateInQueue);

                            if (navigation.GetRoutingQueue().Count > 0 && navigation.GetRoutingQueue()[0].Name == expectedNextStateInQueue.Name)
                                navigation.RemoveFirstFromRoutingQueue();

                            // Detect the last clip of the current segment.
                            // (Note: the new Timeline behavior fires SendAnimationQueueFinished at the
                            // START of the queue rather than at the end, so this segment-completion
                            // logic — moved here from HandleTimelineQueueComplete — triggers when
                            // Timeline begins playing the final clip.)
                            if (navigation.GetRoutingQueue().Count == 0 && navigation.IsRoutingActive())
                            {
                                Logger.Log("Last clip of the current navigation segment ('" + clipNameFromTimeline + "') has started. Processing completion logic.");
                                if (myCurrentState != null && stateAwaitingWalkFinish != null && myCurrentState.Equals(stateAwaitingWalkFinish))
                                {
                                    Logger.Log("Timeline finished segment ending with the designated walking state: '" + stateAwaitingWalkFinish.Name + "'. Transitioning to WAITING_FOR_WALK_FINISH.");
                                    SetOperationState(RoutimatorOperationStates.WAITING_FOR_WALK_FINISH);
                                    navigation.SetRoutingActive(false);
                                    BroadcastWaitingForWalkFinish(stateAwaitingWalkFinish);
                                    if (graphVisualizer != null && graphVisualizer.IsVisible()) graphVisualizer.SetContinuousBlinking(true);
                                }
                                else if (currentOperationState == RoutimatorOperationStates.NAVIGATING)
                                {
                                    Logger.Log("Timeline finished a NAVIGATING segment. Checking for pending segments.");
                                    if (pendingRouteSegments.Count > 0)
                                    {
                                        Logger.Log("Processing next pending segment.");
                                        navigation.SetRoutingActive(false);
                                        ProcessRoute(new List<RoutimatorState.State>(pendingRouteSegments));
                                    }
                                    else
                                    {
                                        Logger.Log("Timeline finished final NAVIGATING segment. Current state: " + (myCurrentState?.Name ?? "None") + ", Overall target: " + (overallNavigationTarget?.Name ?? "None"));
                                        navigation.SetRoutingActive(false);
                                        if (myCurrentState != null && overallNavigationTarget != null && myCurrentState.Equals(overallNavigationTarget))
                                        {
                                            string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                                            string finalFlagsToSet = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                                            voxtaIntegration.TrySetVoxtaFlags(finalFlagsToSet);
                                            SetOperationState(RoutimatorOperationStates.IDLE);
                                        }
                                        else
                                        {
                                            Logger.Log("Final segment processed. Setting IDLE.");
                                            SetOperationState(RoutimatorOperationStates.IDLE);
                                        }
                                        overallNavigationTarget = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (currentOperationState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
                            {
                                Logger.Log($"Timeline sent clip '{clipNameFromTimeline}' while Routimator is WAITING_FOR_WALK_FINISH (logically for state: '{stateAwaitingWalkFinish?.Name}', Timeline queue expected: '{expectedNextStateInQueue.Name}'). Allowing external animation control. Routimator remains in WAITING state.");
                            }
                            else
                            {
                                SuperController.LogError($"Routimator: Mismatch! Timeline sent clip '{clipNameFromTimeline}', Routimator expected '{expectedNextStateInQueue.Name}'. Current OpState: {currentOperationState}. Interrupting navigation.");
                                InterruptNavigationLogic();
                                voxtaIntegration.TrySetVoxtaFlags("!nav");
                                timelineIntegration.NotifyTimelineNavigationInterrupted();
                            }
                        }
                    }
                    else
                    {
                        if (myCurrentState != null && myCurrentState.Name == clipNameFromTimeline)
                        {
                            // Already in sync.
                        }
                        else
                        {
                            RoutimatorState.State timelineState = stateManager.GetStateGlobal(clipNameFromTimeline);
                            if (timelineState != null)
                            {
                                Logger.Log($"Timeline started '{clipNameFromTimeline}' while no active route. Synchronizing Routimator state.");
                                SwitchState(timelineState);
                            }
                            else
                            {
                                Logger.Log($"Timeline started UNKNOWN clip '{clipNameFromTimeline}' while no active route. Routimator state remains: {myCurrentState?.Name ?? "None"}.");
                            }
                        }
                    }
                }
                else
                {
                    SuperController.LogError("Routimator: SendClipPlaybackStarted event received without valid clip name (arg1 was not a string or was null).");
                }
            }
            else if (eventName == "SendAnimationQueueFinished")
            {
                HandleTimelineQueueComplete();
            }
        }

        public void HandleTimelineQueueComplete()
        {
            // The new Timeline version fires this event at the START of the queue, not the end.
            // Segment-completion logic has moved into OnTimelineEvent (triggered by the last
            // SendClipPlaybackStarted of a segment), so this handler is intentionally a no-op.
            Logger.Log("HandleTimelineQueueComplete received and ignored due to new Timeline behavior.");
        }

        // ====================================================================
        // PER-STATE NAVIGATION ACTIONS
        //
        // For every state we register two JSONStorableActions so external plugins / Voxta can
        // navigate by name without building a string command:
        //   "Navigate to <StateName>"          → RouteSwitchStateActionInternal(name, false)
        //   "Walk and Navigate to <StateName>" → RouteSwitchStateActionInternal(name, true)
        //
        // The cache (`stateNavigationActions`) lets us deregister on rename/remove without
        // leaking actions in VAM's global registry.
        // ====================================================================
        private void RegisterNavigateToActions(RoutimatorState.State state)
        {
            if (state == null || string.IsNullOrEmpty(state.Name)) return;

            // Standard (non-walking) action.
            string navigateActionName = "Navigate to " + state.Name;
            JSONStorableAction existingNavigateAction = GetAction(navigateActionName);
            bool navigateActionExistsInCache = stateNavigationActions.ContainsKey(navigateActionName);

            if (existingNavigateAction != null)
            {
                if (!navigateActionExistsInCache)
                    stateNavigationActions[navigateActionName] = existingNavigateAction;
                // else: already registered and cached, nothing to do.
            }
            else
            {
                JSONStorableAction newNavigateAction = new JSONStorableAction(navigateActionName, () => RouteSwitchStateActionInternal(state.Name, false));
                RegisterAction(newNavigateAction);
                stateNavigationActions[navigateActionName] = newNavigateAction;
            }

            // Walking variant.
            string walkAndNavigateActionName = "Walk and Navigate to " + state.Name;
            JSONStorableAction existingWalkNavigateAction = GetAction(walkAndNavigateActionName);
            bool walkNavigateActionExistsInCache = stateNavigationActions.ContainsKey(walkAndNavigateActionName);

            if (existingWalkNavigateAction != null)
            {
                if (!walkNavigateActionExistsInCache)
                    stateNavigationActions[walkAndNavigateActionName] = existingWalkNavigateAction;
            }
            else
            {
                JSONStorableAction newWalkAndNavigateAction = new JSONStorableAction(walkAndNavigateActionName, () => RouteSwitchStateActionInternal(state.Name, true));
                RegisterAction(newWalkAndNavigateAction);
                stateNavigationActions[walkAndNavigateActionName] = newWalkAndNavigateAction;
            }
        }

        private void DeregisterNavigateToActionsByName(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;

            // Standard action.
            string navigateActionName = "Navigate to " + stateName;
            if (stateNavigationActions.ContainsKey(navigateActionName))
            {
                JSONStorableAction actionToDeregister = stateNavigationActions[navigateActionName];
                DeregisterAction(actionToDeregister);
                stateNavigationActions.Remove(navigateActionName);
            }
            else
            {
                // Not in cache: clean up any stray VAM-side registration.
                JSONStorableAction existingAction = GetAction(navigateActionName);
                if (existingAction != null) DeregisterAction(existingAction);
            }

            // Walking variant.
            string walkAndNavigateActionName = "Walk and Navigate to " + stateName;
            if (stateNavigationActions.ContainsKey(walkAndNavigateActionName))
            {
                JSONStorableAction actionToDeregister = stateNavigationActions[walkAndNavigateActionName];
                DeregisterAction(actionToDeregister);
                stateNavigationActions.Remove(walkAndNavigateActionName);
            }
            else
            {
                JSONStorableAction existingAction = GetAction(walkAndNavigateActionName);
                if (existingAction != null) DeregisterAction(existingAction);
            }
        }
    }
}
