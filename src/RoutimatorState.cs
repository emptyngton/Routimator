// --- START OF FILE RoutimatorState.cs ---

using UnityEngine;
using System.Collections.Generic;
using SimpleJSON;
using MacGruber;

namespace Routimator
{
    public class RoutimatorState
    {
        private MVRScript owner;
        private List<State> states = new List<State>();

        public RoutimatorState(MVRScript owner)
        {
            this.owner = owner;
        }

        public class State
        {
            private string myName;
            public bool IsWalkingEnabled = false;

            public State(MVRScript script, string name)
            {
                myName = name;
                Group = "Group_1";
                EnterTrigger = new EventTrigger(script, "On Enter " + name);
                ValueTrigger = new FloatTrigger(script, "On Value " + name);
                ExitTrigger = new EventTrigger(script, "On Exit " + name);
                Transitions = new List<State>();
                SetFlags = "";
                IsWalkingEnabled = false;
            }

            public State(MVRScript script, string name, State source)
            {
                myName = name;
                Group = source.Group;
                EnterTrigger = new EventTrigger(source.EnterTrigger);
                ValueTrigger = new FloatTrigger(source.ValueTrigger);
                ExitTrigger = new EventTrigger(source.ExitTrigger);
                Transitions = new List<State>(source.Transitions);
                SetFlags = source.SetFlags;
                IsWalkingEnabled = source.IsWalkingEnabled;
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
                    EnterTrigger.Name = "On Enter " + value;
                    ValueTrigger.Name = "On Value " + value;
                    ExitTrigger.Name = "On Exit " + value;
                }
            }

            public string Group = "Group_1";
            public string SetFlags;
            public MacGruber.EventTrigger EnterTrigger;
            public FloatTrigger ValueTrigger;
            public EventTrigger ExitTrigger;
            public List<State> Transitions;

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

        public void UpdateTriggers()
        {
            for (int i = 0; i < states.Count; i++)
            {
                states[i].EnterTrigger.Update();
                states[i].ValueTrigger.Update();
                states[i].ExitTrigger.Update();
            }
        }

        public void CleanupStates()
        {
            for (int i = 0; i < states.Count; i++)
            {
                states[i].EnterTrigger.Remove();
                states[i].ValueTrigger.Remove();
                states[i].ExitTrigger.Remove();
            }
            states.Clear();
        }

        public State CreateState(string name, State source, string groupName)
        {
            State st;
            if (source == null)
                st = new State(owner, name);
            else
                st = new State(owner, name, source);

            st.Group = groupName;

            // Logika dla InfiniteDuration usunięta
            // if (name.StartsWith("VS_"))
            // st.InfiniteDuration = true;
            // else if (name.StartsWith("VT_"))
            // st.InfiniteDuration = false;

            states.Add(st);
            return st;
        }

        public void RemoveState(State st)
        {
            for (int s = 0; s < states.Count; s++)
                states[s].Transitions.Remove(st);

            st.EnterTrigger.Remove();
            st.ValueTrigger.Remove();
            st.ExitTrigger.Remove();
            states.Remove(st);
        }

        public void SortStates()
        {
            states.Sort(State.SortByNameAscending);
        }

        public void RemoveStatesByGroup(string groupName)
        {
            List<State> statesToRemove = new List<State>();
            foreach (State s in states)
            {
                if (s.Group == groupName)
                    statesToRemove.Add(s);
            }
            foreach (State s in statesToRemove)
            {
                RemoveState(s);
            }
        }

        public void RenameGroup(string oldName, string newName)
        {
            foreach (State s in states)
            {
                if (s.Group == oldName)
                    s.Group = newName;
            }
        }

        public bool StateExists(string groupName, string stateName)
        {
            return states.Exists(s => s.Group == groupName && s.Name == stateName);
        }

        public string GetNextName(string baseName)
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

        public State GetSelectedState(string groupName, string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return null;
            return states.Find(s => s.Group == groupName && s.Name == stateName);
        }

        public State GetStateGlobal(string name)
        {
            return states.Find(s => s.Name == name);
        }

        public List<State> GetStates()
        {
            return states;
        }

        public List<State> GetStatesInGroup(string groupName)
        {
            return states.FindAll(s => s.Group == groupName);
        }

        public bool MoveStateUp(string groupName, string stateName)
        {
            List<State> groupStates = GetStatesInGroup(groupName);
            int index = groupStates.FindIndex(s => s.Name == stateName);

            if (index > 0)
            {
                State temp = groupStates[index - 1];
                groupStates[index - 1] = groupStates[index];
                groupStates[index] = temp;
                int count = 0;
                for (int i = 0; i < states.Count; i++)
                    if (states[i].Group == groupName)
                        states[i] = groupStates[count++];
                return true;
            }
            return false;
        }

        public bool MoveStateDown(string groupName, string stateName)
        {
            List<State> groupStates = GetStatesInGroup(groupName);
            int index = groupStates.FindIndex(s => s.Name == stateName);

            if (index >= 0 && index < groupStates.Count - 1)
            {
                State temp = groupStates[index + 1];
                groupStates[index + 1] = groupStates[index];
                groupStates[index] = temp;
                int count = 0;
                for (int i = 0; i < states.Count; i++)
                    if (states[i].Group == groupName)
                        states[i] = groupStates[count++];
                return true;
            }
            return false;
        }

        public void SyncAtomNames()
        {
            for (int i = 0; i < states.Count; i++)
            {
                states[i].EnterTrigger.SyncAtomNames();
                states[i].ValueTrigger.SyncAtomNames();
                states[i].ExitTrigger.SyncAtomNames();
            }
        }

        public void ClearStates()
        {
            CleanupStates();
        }

        public void AddState(State state)
        {
            states.Add(state);
        }
    }
}
// --- END OF FILE RoutimatorState.cs ---