// --- START OF FILE RoutimatorUI.cs ---

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MacGruber;

namespace Routimator
{
    public class RoutimatorUI
    {
        private Routimator owner;
        private List<object> stateUI = new List<object>();
        private bool needUIRefresh = false;
        private bool needUIRebuild = false;
        private JSONStorableStringChooser animationChooser;

        public RoutimatorUI(Routimator owner)
        {
            this.owner = owner;
            animationChooser = new JSONStorableStringChooser("State From Anim", new List<string>(), "", "State From Anim");
            animationChooser.displayChoices = new List<string>();
        }

        private string GetColoredStateName(RoutimatorState.State state)
        {
            if (state.Name.StartsWith("VS"))
                return "<b><color=#0089b4>" + state.Name + "</color></b>";
            if (state.Name.StartsWith("VT"))
                return "<b><color=#c95803>" + state.Name + "</color></b>";
            if (state.Name.StartsWith("NAV"))
                return "<b><color=#4CAF50>" + state.Name + "</color></b>";
            return "<b>" + state.Name + "</b>";
        }

        public void UIRebuild()
        {
            needUIRebuild = false;
            var stateManager = owner.stateManager;
            var groupChooser = owner.GetGroupChooser();
            var stateChooser = owner.GetStateChooser();

            if (stateManager == null || groupChooser == null || stateChooser == null)
            {
                needUIRebuild = true;
                return;
            }

            List<string> options = new List<string>();
            List<string> displays = new List<string>();

            foreach (RoutimatorState.State s in stateManager.GetStates())
            {
                if (s.Group == groupChooser.val)
                {
                    options.Add(s.Name);
                    displays.Add(GetColoredStateName(s));
                }
            }

            stateChooser.choices = options;
            stateChooser.displayChoices = displays;

            if (options.Count > 0)
            {
                if (!options.Contains(stateChooser.val))
                    stateChooser.val = options[0];
                else
                    UISetState(stateChooser.val, groupChooser.val, stateManager);
            }
            else
            {
                stateChooser.val = "";
                UISetState("", groupChooser.val, stateManager);
            }
        }

        public void UISetState(string stateName, string groupName, RoutimatorState stateManager)
        {
            Utils.RemoveUIElements(owner, stateUI);
            var groupNameParam = owner.GetGroupName();
            groupNameParam.valNoCallback = groupName;
            RoutimatorState.State state = stateManager.GetSelectedState(groupName, stateName);

            // Pobranie referencji do statycznych kontrolek UI
            var stateNameParam = owner.GetStateName();
            var stateSetFlagsParam = owner.GetStateSetFlags();
            var stateIsWalkingEnabledParam = owner.GetStateIsWalkingEnabled();

            if (state == null)
            {
                // Jeśli żaden stan nie jest wybrany, czyścimy pola
                if (stateNameParam != null)
                {
                    stateNameParam.valNoCallback = "";
                    if (stateNameParam.inputField != null)
                        stateNameParam.inputField.text = "";
                }
                if (stateSetFlagsParam != null)
                {
                    stateSetFlagsParam.valNoCallback = "";
                    if (stateSetFlagsParam.inputField != null)
                        stateSetFlagsParam.inputField.text = "";
                }
                if (stateIsWalkingEnabledParam != null)
                {
                    stateIsWalkingEnabledParam.valNoCallback = false;
                    if (stateIsWalkingEnabledParam.toggle != null)
                        stateIsWalkingEnabledParam.toggle.isOn = false;
                }
                return;
            }

            // Aktualizacja wartości w statycznych polach UI
            if (stateNameParam != null)
            {
                stateNameParam.valNoCallback = state.Name;
                if (stateNameParam.inputField != null)
                    stateNameParam.inputField.text = state.Name;
            }
            if (stateSetFlagsParam != null)
            {
                stateSetFlagsParam.valNoCallback = state.SetFlags;
                if (stateSetFlagsParam.inputField != null)
                    stateSetFlagsParam.inputField.text = state.SetFlags;
            }
            if (stateIsWalkingEnabledParam != null)
            {
                stateIsWalkingEnabledParam.valNoCallback = state.IsWalkingEnabled;
                if (stateIsWalkingEnabledParam.toggle != null)
                    stateIsWalkingEnabledParam.toggle.isOn = state.IsWalkingEnabled;
            }

            // Poniżej tworzenie FAKTYCZNIE dynamicznych elementów UI, które są dodawane do stateUI

            UIDynamicTextInfo triggerHeader = Utils.SetupInfoOneLine(owner, "<size=30><b>Triggers</b></size>", true);
            stateUI.Add(triggerHeader);

            UIDynamicButton enterButton = Utils.SetupButton(owner, "On Enter " + state.Name, state.EnterTrigger.OpenPanel, true);
            UIDynamicButton valueButton = Utils.SetupButton(owner, "On Value " + state.Name, state.ValueTrigger.OpenPanel, true);
            UIDynamicButton exitButton = Utils.SetupButton(owner, "On Exit " + state.Name, state.ExitTrigger.OpenPanel, true);
            stateUI.Add(enterButton);
            stateUI.Add(valueButton);
            stateUI.Add(exitButton);

            if (stateManager.GetStates().Count > 0)
            {
                stateUI.Add(Utils.SetupSpacer(owner, 10, true));
                UIDynamicTextInfo transitionsHeader = Utils.SetupInfoOneLine(owner, "<size=30><b>Transitions</b></size>", true);
                stateUI.Add(transitionsHeader);

                List<string> transitionNames = new List<string>();
                List<string> displayNames = new List<string>();

                bool selectedStartsWithVS = state.Name.StartsWith("VS");
                bool selectedStartsWithVT = state.Name.StartsWith("VT");

                for (int s_idx = 0; s_idx < stateManager.GetStates().Count; ++s_idx)
                {
                    var tState = stateManager.GetStates()[s_idx];
                    if (tState == state || state.Transitions.Contains(tState))
                        continue;

                    if ((selectedStartsWithVS && tState.Name.StartsWith("VS")) ||
                        (selectedStartsWithVT && tState.Name.StartsWith("VT")))
                        continue;

                    transitionNames.Add(tState.Name);
                    displayNames.Add(GetColoredStateName(tState));
                }

                var transitionChooser = owner.GetTransitionChooser();
                transitionChooser.choices = transitionNames;
                transitionChooser.displayChoices = displayNames;
                transitionChooser.val = transitionNames.Count == 0 ? "" : transitionNames[0];

                UIDynamicPopup transitionPopup = owner.CreateFilterablePopup(transitionChooser, true);
                stateUI.Add(transitionPopup);

                UIDynamicButton addTransitionBtn = Utils.SetupButton(owner, "Add Transition", () => UIAddTransition(state), true);
                stateUI.Add(addTransitionBtn);

                for (int t = 0; t < state.Transitions.Count; t++)
                {
                    RoutimatorState.State trans = state.Transitions[t];
                    string label = GetColoredStateName(trans);
                    UIDynamicLabelXButton btn = Utils.SetupLabelXButton(owner, label, () => UIRemoveTransition(state, trans), true);
                    stateUI.Add(btn);
                }
            }
        }

        private void UIAddTransition(RoutimatorState.State fromState)
        {
            var transitionChooser = owner.GetTransitionChooser();
            var stateManager = owner.stateManager;
            RoutimatorState.State toState = stateManager.GetStateGlobal(transitionChooser.val);
            if (toState == null) return;
            fromState.Transitions.Add(toState);
            fromState.Transitions.Sort(RoutimatorState.State.SortByNameAscending);
            UIRefresh();
            owner.TriggerGraphUpdate(false);
        }

        private void UIRemoveTransition(RoutimatorState.State fromState, RoutimatorState.State toRemove)
        {
            fromState.Transitions.Remove(toRemove);
            UIRefresh();
            owner.TriggerGraphUpdate(false);
        }

        public void UIRefresh()
        {
            needUIRefresh = false;
            var stateChooser = owner.GetStateChooser();
            var groupChooser = owner.GetGroupChooser();
            var stateManager = owner.stateManager;
            UISetState(stateChooser.val, groupChooser.val, stateManager);
        }

        public bool NeedsRebuild() { return needUIRebuild; }
        public void SetNeedsRebuild(bool value) { needUIRebuild = value; }
        public bool NeedsRefresh() { return needUIRefresh; }
        public void SetNeedsRefresh(bool value) { needUIRefresh = value; }
    }
}
// --- END OF FILE RoutimatorUI.cs ---