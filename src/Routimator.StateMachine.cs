// Routimator.StateMachine.cs
// State transitions: SwitchState (the core transition), operation-state control,
// and the plugin-state reset used during load.

using UnityEngine;
using System.Collections.Generic;

namespace Routimator
{
    public partial class Routimator : MVRScript
    {
        // ====================================================================
        // EXTERNAL TRIGGER — SwitchState (string name → direct jump)
        // ====================================================================
        private void SwitchStateAction(string newStateName)
        {
            mySwitchState.valNoCallback = string.Empty;
            RoutimatorState.State st = stateManager.GetStateGlobal(newStateName);
            if (st != null) SwitchState(st);
            else SuperController.LogError("Routimator: Can't switch to unknown state '" + newStateName + "'.");
        }

        // ====================================================================
        // CLEAR & RESET — used by load, to replace current plugin state with a fresh one.
        // ====================================================================
        public void ClearAndSetInitialPluginState(RoutimatorState.State initialState = null)
        {
            if (currentOperationState != RoutimatorOperationStates.IDLE)
            {
                InterruptNavigationLogic();
            }

            RoutimatorState.State previousPluginState = myCurrentState;
            myCurrentState = initialState;

            pendingRouteSegments.Clear();
            stateAwaitingWalkFinish = null;
            overallNavigationTarget = null;
            SetOperationState(RoutimatorOperationStates.IDLE);

            navigation.SetNavigationTargetState(null);
            navigation.ClearRoutingQueue();

            if (myCurrentState != null)
            {
                myCurrentState.EnterTrigger.Trigger();
                voxtaIntegration.TrySetVoxtaFlags(myCurrentState.SetFlags);
            }

            UpdateCurrentStateInfo();
            UpdateCurrentStateValue();

            if (graphVisualizer != null && graphVisualizer.IsVisible())
            {
                graphVisualizer.UpdateHighlights();
                if (previousPluginState != myCurrentState || myCurrentState != null)
                    graphVisualizer.NotifyStateChanged();
            }
        }

        // ====================================================================
        // CORE — SwitchState: fires Exit on previous, Enter on new, sends to Timeline/Voxta.
        //
        // This also handles the "new switch interrupts an in-flight operation" case: if we're
        // NAVIGATING or WAITING_FOR_WALK_FINISH and the target isn't part of that operation,
        // the operation is cancelled (Timeline queue cleared, Voxta `!nav` set) before the jump.
        // ====================================================================
        public void SwitchState(RoutimatorState.State newState)
        {
            bool partOfCurrentRouting = false;
            if (navigation.IsRoutingActive() && navigation.GetRoutingQueue().Count > 0 && navigation.GetRoutingQueue()[0].Equals(newState))
                partOfCurrentRouting = true;
            if (stateAwaitingWalkFinish != null && stateAwaitingWalkFinish.Equals(newState))
                partOfCurrentRouting = true;

            RoutimatorState.State previousState = myCurrentState;

            if (!partOfCurrentRouting && currentOperationState != RoutimatorOperationStates.IDLE)
            {
                Logger.Log("SwitchState to '" + (newState?.Name ?? "null") + "' called, interrupting current operation: " + currentOperationState);
                InterruptNavigationLogic();
                voxtaIntegration.TrySetVoxtaFlags("!nav");
                timelineIntegration.NotifyTimelineNavigationInterrupted();
            }

            if (previousState != null && previousState.Equals(newState))
            {
                UpdateCurrentStateInfo();
                UpdateCurrentStateValue();
                if (graphVisualizer != null && graphVisualizer.IsVisible())
                    graphVisualizer.UpdateHighlights();
                return;
            }

            if (previousState != null) previousState.ExitTrigger.Trigger();
            myCurrentState = newState;

            if (myCurrentState != null)
            {
                myCurrentState.EnterTrigger.Trigger();
                if (!partOfCurrentRouting || (previousState != null && !previousState.SetFlags.Equals(myCurrentState.SetFlags)))
                {
                    voxtaIntegration.TrySetVoxtaFlags(myCurrentState.SetFlags);
                }
                if (!navigation.IsRoutingActive() && currentOperationState != RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
                {
                    timelineIntegration.TryPlayTimelineAnimation(myCurrentState.Name);
                }
            }
            else
            {
                if (currentOperationState == RoutimatorOperationStates.NAVIGATING || currentOperationState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
                {
                    Logger.Log("Switched to null state during an operation. Ending operation.");
                    InterruptNavigationLogic();
                    voxtaIntegration.TrySetVoxtaFlags("!nav");
                }
                else
                {
                    SetOperationState(RoutimatorOperationStates.IDLE);
                }
            }

            if (stateAwaitingWalkFinish != null && previousState != null && previousState.Equals(stateAwaitingWalkFinish))
                stateAwaitingWalkFinish = null;

            if (currentOperationState != RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
            {
                if ((overallNavigationTarget != null && myCurrentState != null && myCurrentState.Equals(overallNavigationTarget)) ||
                    (overallNavigationTarget == null && currentOperationState != RoutimatorOperationStates.IDLE && myCurrentState == null) ||
                    (overallNavigationTarget == null && currentOperationState != RoutimatorOperationStates.IDLE && myCurrentState != null && !navigation.IsRoutingActive() && pendingRouteSegments.Count == 0)
                   )
                {
                    if (currentOperationState != RoutimatorOperationStates.IDLE && overallNavigationTarget != null)
                    {
                        string customFlags = mySetFlagsOnExitNavigation != null ? mySetFlagsOnExitNavigation.val : "";
                        string finalFlags = "!nav" + (string.IsNullOrEmpty(customFlags) ? "" : "," + customFlags);
                        voxtaIntegration.TrySetVoxtaFlags(finalFlags);
                    }
                    SetOperationState(RoutimatorOperationStates.IDLE);
                    overallNavigationTarget = null;
                }
            }

            UpdateCurrentStateInfo();
            UpdateCurrentStateValue();

            if (graphVisualizer != null && graphVisualizer.IsVisible())
            {
                graphVisualizer.UpdateHighlights();
                if (previousState != myCurrentState)
                    graphVisualizer.NotifyStateChanged();
            }
        }

        // ====================================================================
        // OPERATION-STATE TRANSITION — drives UI status + graph blink indicator
        // ====================================================================
        private void SetOperationState(string newState)
        {
            string previousOpState = currentOperationState;
            if (currentOperationState != newState)
                currentOperationState = newState;
            UpdatePluginOperationStatusInfo();

            if (graphVisualizer != null && graphVisualizer.IsVisible())
            {
                bool shouldBeContinuousBlinking = (newState == RoutimatorOperationStates.NAVIGATING || newState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH);

                if (shouldBeContinuousBlinking)
                {
                    graphVisualizer.SetContinuousBlinking(true);
                }
                else if ((previousOpState == RoutimatorOperationStates.NAVIGATING || previousOpState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH)
                         && !shouldBeContinuousBlinking)
                {
                    graphVisualizer.SetContinuousBlinking(false);
                }
            }
        }
    }
}
