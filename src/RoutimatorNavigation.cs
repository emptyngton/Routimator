// --- START OF FILE RoutimatorNavigation.cs ---

using UnityEngine;
using System.Collections.Generic;

namespace Routimator
{
    public class RoutimatorNavigation
    {
        private MVRScript owner;
        private RoutimatorState stateManager;
        private List<RoutimatorState.State> routingQueue = new List<RoutimatorState.State>();
        private bool routingActive = false;
        private RoutimatorState.State navigationTargetState;

        private float currentClock = 0.0f;
        private float currentDuration = 1.0f;
        private float currentValue = -1.0f;
        private int currentClockInt = -1;

        public RoutimatorNavigation(MVRScript owner, RoutimatorState stateManager)
        {
            this.owner = owner;
            this.stateManager = stateManager;
        }

        public void UpdateClock()
        {
            if (currentDuration > 0f && !float.IsPositiveInfinity(currentDuration))
            {
                currentClock = Mathf.Max(currentClock - Time.deltaTime, 0.0f);
            }
        }

        public void Transition(RoutimatorState.State currentState)
        {
            if (currentState == null)
            {
                ((Routimator)owner).SwitchState(null);
                return;
            }

            List<RoutimatorState.State> transitions = currentState.Transitions;
            if (transitions.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, transitions.Count);
                ((Routimator)owner).SwitchState(transitions[idx]);
            }
            else
            {
                ((Routimator)owner).SwitchState(null);
            }
        }

        public List<RoutimatorState.State> FindRoute(RoutimatorState.State start, RoutimatorState.State target)
        {
            Dictionary<RoutimatorState.State, RoutimatorState.State> prev = new Dictionary<RoutimatorState.State, RoutimatorState.State>();
            Queue<RoutimatorState.State> queue = new Queue<RoutimatorState.State>();
            HashSet<RoutimatorState.State> visited = new HashSet<RoutimatorState.State>();

            if (start == null || target == null) return null;

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                RoutimatorState.State current = queue.Dequeue();

                if (current.Equals(target))
                {
                    List<RoutimatorState.State> path = new List<RoutimatorState.State>();
                    RoutimatorState.State tmp = target;
                    while (tmp != null && !tmp.Equals(start))
                    {
                        path.Add(tmp);
                        if (!prev.ContainsKey(tmp))
                        {
                            SuperController.LogError("RoutimatorNavigation: Inconsistent path in FindRoute.");
                            return null;
                        }
                        tmp = prev[tmp];
                    }
                    if (target.Equals(start))
                    {
                        path.Add(start);
                    }
                    path.Reverse();
                    return path;
                }

                foreach (RoutimatorState.State neighbor in current.Transitions)
                {
                    if (neighbor != null && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        prev[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return null;
        }

        public List<RoutimatorState.State> FindRouteWithWalking(RoutimatorState.State start, RoutimatorState.State target)
        {
            if (start == null || target == null)
            {
                SuperController.LogError("RoutimatorNavigation: FindRouteWithWalking called with null start or target.");
                return null;
            }

            List<RoutimatorState.State> allStates = stateManager.GetStates();
            List<RoutimatorState.State> walkingStates = new List<RoutimatorState.State>();
            foreach (RoutimatorState.State s in allStates)
            {
                if (s.IsWalkingEnabled)
                {
                    walkingStates.Add(s);
                }
            }

            if (walkingStates.Count == 0)
            {
                Logger.Log("RoutimatorNavigation: FindRouteWithWalking - No states with IsWalkingEnabled found.");
                return null;
            }

            List<RoutimatorState.State> shortestPath = null;

            if (start.Equals(target))
            {
                if (start.IsWalkingEnabled)
                {
                    return new List<RoutimatorState.State>() { start };
                }
                else
                {
                    foreach (RoutimatorState.State W in walkingStates)
                    {
                        if (W.Equals(start)) continue;

                        List<RoutimatorState.State> segment1 = FindRoute(start, W);
                        List<RoutimatorState.State> segment2 = FindRoute(W, start);

                        if (segment1 != null && segment1.Count > 0 && segment2 != null && segment2.Count > 0)
                        {
                            List<RoutimatorState.State> currentLoopPath = new List<RoutimatorState.State>();
                            currentLoopPath.AddRange(segment1);

                            if (segment2[0].Equals(W) && currentLoopPath.Count > 0 && currentLoopPath[currentLoopPath.Count - 1].Equals(W))
                            {
                                for (int i = 1; i < segment2.Count; ++i) currentLoopPath.Add(segment2[i]);
                            }
                            else
                            {
                                currentLoopPath.AddRange(segment2);
                            }


                            if (shortestPath == null || currentLoopPath.Count < shortestPath.Count)
                            {
                                shortestPath = currentLoopPath;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (RoutimatorState.State W in walkingStates)
                {
                    List<RoutimatorState.State> segment1 = FindRoute(start, W);
                    List<RoutimatorState.State> segment2 = FindRoute(W, target);

                    if (segment1 == null || segment1.Count == 0 || segment2 == null || segment2.Count == 0)
                    {
                        continue;
                    }

                    List<RoutimatorState.State> currentFullPath = new List<RoutimatorState.State>();
                    currentFullPath.AddRange(segment1);

                    if (segment2[0].Equals(W) && currentFullPath.Count > 0 && currentFullPath[currentFullPath.Count - 1].Equals(W))
                    {
                        for (int i = 1; i < segment2.Count; ++i) currentFullPath.Add(segment2[i]);
                    }
                    else
                    {
                        currentFullPath.AddRange(segment2);
                    }

                    if (shortestPath == null || currentFullPath.Count < shortestPath.Count)
                    {
                        shortestPath = currentFullPath;
                    }
                }
            }
            return shortestPath;
        }

        public void SetRoutingQueue(List<RoutimatorState.State> queue) { routingQueue = new List<RoutimatorState.State>(queue); }
        public List<RoutimatorState.State> GetRoutingQueue() { return routingQueue; }
        public void ClearRoutingQueue() { routingQueue.Clear(); }
        public void RemoveFirstFromRoutingQueue() { if (routingQueue.Count > 0) routingQueue.RemoveAt(0); }
        public bool IsRoutingActive() { return routingActive; }
        public void SetRoutingActive(bool active) { routingActive = active; }
        public float GetCurrentClock() { return currentClock; }
        public void SetCurrentClock(float clock) { currentClock = clock; }
        public float GetCurrentDuration() { return currentDuration; }
        public void SetCurrentDuration(float duration) { currentDuration = duration; }
        public float GetCurrentValue() { return currentValue; }
        public void SetCurrentValue(float value) { currentValue = value; }
        public int GetCurrentClockInt() { return currentClockInt; }
        public void SetCurrentClockInt(int clockInt) { currentClockInt = clockInt; }
        public RoutimatorState.State GetNavigationTargetState() { return navigationTargetState; }
        public void SetNavigationTargetState(RoutimatorState.State state) { navigationTargetState = state; }
    }
}
// --- END OF FILE RoutimatorNavigation.cs ---