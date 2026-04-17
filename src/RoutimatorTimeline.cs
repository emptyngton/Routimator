// --- START OF FILE RoutimatorTimeline.cs ---

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SimpleJSON;
using System;

namespace Routimator
{
    public class RoutimatorTimeline
    {
        private MVRScript owner;
        private JSONStorable timelineStorable;
        // timelineQueueCompleteAction is managed by Routimator.cs

        public RoutimatorTimeline(MVRScript owner)
        {
            this.owner = owner;
        }

        public IEnumerator FindTimelinePlugin()
        {
            while (true)
            {
                foreach (Atom atom in SuperController.singleton.GetAtoms())
                {
                    string timelineStorableID = atom.GetStorableIDs().FirstOrDefault(x => x.Contains("VamTimeline.AtomPlugin"));
                    if (!string.IsNullOrEmpty(timelineStorableID))
                    {
                        timelineStorable = atom.GetStorableByID(timelineStorableID);
                        if (timelineStorable != null)
                        {
                            Logger.Log("RoutimatorTimeline: Timeline plugin found on atom: " + atom.name);
                            RegisterForTimelineEvents();
                            ((Routimator)owner).RebuildAnimationChooserUI();
                            yield break;
                        }
                    }
                }
                yield return null;
            }
        }

        private void RegisterForTimelineEvents()
        {
            if (timelineStorable != null && owner != null)
            {
                timelineStorable.SendMessage("OnTimelineAnimationReady", owner, SendMessageOptions.DontRequireReceiver);
                Logger.Log("RoutimatorTimeline: Registered with Timeline for events via OnTimelineAnimationReady.");
            }
            else
            {
                SuperController.LogError("RoutimatorTimeline: Cannot register for Timeline events: timelineStorable or owner is null.");
            }
        }


        public void SendRouteToTimeline(List<RoutimatorState.State> route)
        {
            if (timelineStorable == null)
            {
                SuperController.LogError("RoutimatorTimeline: Cannot send route - Timeline plugin not found");
                return;
            }

            // Krok 1: Wyczyść istniejącą kolejkę, aby zapewnić start od zera.
            JSONStorableAction clearAction = timelineStorable.GetAction("Clear Queue");
            if (clearAction != null && clearAction.actionCallback != null)
            {
                clearAction.actionCallback.Invoke();
            }
            else
            {
                // To potencjalny problem, więc logujemy go jako błąd. Nowa trasa zostanie dołączona do istniejącej.
                SuperController.LogError("RoutimatorTimeline: Could not find 'Stop And Clear Animation Queue' action. The new route will be added to any existing items in the Timeline queue.");
            }

            // Krok 2: Znajdź chooser 'Add To Queue' używając poprawnej metody.
            JSONStorableStringChooser addToQueueChooser = timelineStorable.GetStringChooserJSONParam("Add To Queue");

            if (addToQueueChooser == null)
            {
                SuperController.LogError("RoutimatorTimeline: Timeline 'Add To Queue' storable parameter (JSONStorableStringChooser) not found.");
                return;
            }

            // Krok 3: Dodaj każdy stan z trasy do kolejki.
            for (int i = 0; i < route.Count; i++)
            {
                SuperController.LogMessage("Scheduling " + route[i].Name);
                // Ustawienie wartości choosera wywoła callback w Timeline, który doda element do kolejki.
                addToQueueChooser.val = route[i].Name;
            }

            // Krok 4: Rozpocznij odtwarzanie nowo zbudowanej kolejki.
            JSONStorableAction playQueueAction = timelineStorable.GetAction("Play Queue");
            if (playQueueAction != null && playQueueAction.actionCallback != null)
            {
                playQueueAction.actionCallback.Invoke();
            }
            else
            {
                SuperController.LogError("RoutimatorTimeline: Timeline 'Play Queue' action not found.");
            }
        }

        public void TryPlayTimelineAnimation(string animationName)
        {
            if (timelineStorable == null)
            {
                return;
            }

            JSONStorableAction playAction = timelineStorable.GetAction("Play " + animationName);
            if (playAction != null && playAction.actionCallback != null)
            {
                playAction.actionCallback.Invoke();
                return;
            }

            playAction = timelineStorable.GetAction("Play " + animationName + "/*"); // Fallback for names with suffixes
            if (playAction != null && playAction.actionCallback != null)
            {
                playAction.actionCallback.Invoke();
                return;
            }
        }


        public void NotifyTimelineNavigationInterrupted()
        {
            if (timelineStorable == null)
                return;

            // Updated action name based on common VamTimeline plugin naming
            JSONStorableAction interruptAction = timelineStorable.GetAction("Stop And Clear Animation Queue");
            if (interruptAction != null && interruptAction.actionCallback != null)
            {
                interruptAction.actionCallback.Invoke();
                Logger.Log("RoutimatorTimeline: Notified Timeline of navigation interruption (called 'Stop And Clear Animation Queue').");
            }
            else
            {
                // Attempt a fallback, common alternative
                interruptAction = timelineStorable.GetAction("InterruptQueue");
                if (interruptAction != null && interruptAction.actionCallback != null)
                {
                    interruptAction.actionCallback.Invoke();
                    Logger.Log("RoutimatorTimeline: Notified Timeline of navigation interruption (called 'InterruptQueue' as fallback).");
                }
                else
                {
                    Logger.Log("RoutimatorTimeline: WARNING: Timeline action 'Stop And Clear Animation Queue' (and fallback 'InterruptQueue') not found.");
                }
            }
        }

        public JSONStorable GetTimelineStorable()
        {
            return timelineStorable;
        }

        public List<string> GetAllAnimations(string additionalPattern = ".*")
        {
            List<string> animations = new List<string>();
            if (timelineStorable == null)
                return animations;

            var baseRegex = new Regex(@"^Play (?!.*(If Not Playing|Segment)).*", RegexOptions.Compiled);
            // It's good practice to ensure userPattern is valid regex or handle potential exceptions.
            Regex userRegex = null;
            try { userRegex = new Regex(additionalPattern, RegexOptions.Compiled); }
            catch (ArgumentException)
            {
                SuperController.LogError($"RoutimatorTimeline: Invalid regex pattern provided: {additionalPattern}. Using default '.*'.");
                userRegex = new Regex(".*", RegexOptions.Compiled);
            }


            foreach (var name in timelineStorable.GetActionNames())
            {
                if (baseRegex.IsMatch(name) && userRegex.IsMatch(name))
                {
                    animations.Add(name.Substring(5));
                }
            }
            return animations;
        }

        public bool IsAnimationExists(string animationName)
        {
            if (timelineStorable == null) return false;
            return timelineStorable.GetActionNames().Contains("Play " + animationName) ||
                   timelineStorable.GetActionNames().Contains("Play " + animationName + "/*"); // Check fallback too
        }
    }
}
// --- END OF FILE RoutimatorTimeline.cs ---