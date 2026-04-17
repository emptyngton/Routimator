// --- START OF FILE RoutimatorSerialization.cs ---

using UnityEngine;
using System.Collections.Generic;
using SimpleJSON;
using MVR.FileManagementSecure;

namespace Routimator
{
    public class RoutimatorSerialization
    {
        private Routimator owner;
        private RoutimatorState stateManager;

        public RoutimatorSerialization(Routimator owner, RoutimatorState stateManager)
        {
            this.owner = owner;
            this.stateManager = stateManager;
        }

        public JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false, Dictionary<string, Vector2> nodePositionsToSave = null)
        {
            JSONClass pluginDataJson = new JSONClass();
            pluginDataJson["id"] = owner.storeId;

            if (forceStore || stateManager.GetStates().Count > 0 || (nodePositionsToSave != null && nodePositionsToSave.Count > 0))
            {
                JSONArray sclist = new JSONArray();

                foreach (RoutimatorState.State st in stateManager.GetStates())
                {
                    JSONClass sc = new JSONClass();
                    sc["Name"] = st.Name;
                    sc["Group"] = st.Group;
                    sc["SetFlags"] = st.SetFlags;
                    sc["IsWalkingEnabled"].AsBool = st.IsWalkingEnabled;

                    if (st.Transitions.Count > 0)
                    {
                        JSONArray tlist = new JSONArray();
                        foreach (RoutimatorState.State t in st.Transitions)
                        {
                            if (t != null && !string.IsNullOrEmpty(t.Name))
                                tlist.Add("", t.Name);
                        }
                        if (tlist.Count > 0)
                            sc["Transitions"] = tlist;
                    }

                    if (st.EnterTrigger != null && !string.IsNullOrEmpty(st.EnterTrigger.Name))
                        sc[st.EnterTrigger.Name] = st.EnterTrigger.GetJSON(owner.subScenePrefix);
                    if (st.ValueTrigger != null && !string.IsNullOrEmpty(st.ValueTrigger.Name))
                        sc[st.ValueTrigger.Name] = st.ValueTrigger.GetJSON(owner.subScenePrefix);
                    if (st.ExitTrigger != null && !string.IsNullOrEmpty(st.ExitTrigger.Name))
                        sc[st.ExitTrigger.Name] = st.ExitTrigger.GetJSON(owner.subScenePrefix);

                    sclist.Add("", sc);
                }
                if (sclist.Count > 0)
                    pluginDataJson["States"] = sclist;

                var currentState = owner.GetCurrentState();
                if (currentState != null)
                {
                    pluginDataJson["InitialState"] = currentState.Name;
                }

                if (nodePositionsToSave != null && nodePositionsToSave.Count > 0)
                {
                    JSONArray nodePositionsArray = new JSONArray();
                    foreach (var kvp in nodePositionsToSave)
                    {
                        JSONClass nodePosJson = new JSONClass();
                        nodePosJson["StateName"] = kvp.Key;
                        nodePosJson["PositionX"].AsFloat = kvp.Value.x;
                        nodePosJson["PositionY"].AsFloat = kvp.Value.y;
                        nodePositionsArray.Add(nodePosJson);
                    }
                    pluginDataJson["NodePositions"] = nodePositionsArray;
                }
            }
            return pluginDataJson;
        }

        public void SaveJSON(JSONClass jc, string filePath)
        {
            try
            {
                string directoryPath = filePath.Substring(0, filePath.LastIndexOfAny(new char[] { '/', '\\' }));
                FileManagerSecure.CreateDirectory(directoryPath);
                SuperController.singleton.SaveJSON(jc, filePath);
            }
            catch (System.Exception e)
            {
                SuperController.LogError("Failed to save Routimator preset: " + e.Message);
            }
        }

        public JSONNode LoadJSON(string url)
        {
            try
            {
                JSONNode node = SuperController.singleton.LoadJSON(url);
                return node;
            }
            catch (System.Exception e)
            {
                SuperController.LogError("Failed to load Routimator preset: " + e.Message);
                return null;
            }
        }

        public void LateRestoreFromJSON(JSONClass jsonClass, Dictionary<string, Vector2> nodePositionsToRestore)
        {
            stateManager.ClearStates();
            nodePositionsToRestore.Clear();

            if (jsonClass == null)
            {
                Logger.Log("RoutimatorSerialization: No data to restore (jsonClass is null).");
                return;
            }

            if (jsonClass.HasKey("States"))
            {
                JSONArray sclist = jsonClass["States"].AsArray;
                for (int i = 0; i < sclist.Count; i++)
                {
                    JSONClass sc = sclist[i].AsObject;
                    if (sc == null || !sc.HasKey("Name") || string.IsNullOrEmpty(sc["Name"].Value)) continue;

                    RoutimatorState.State st = new RoutimatorState.State(owner, sc["Name"].Value);
                    // st.InfiniteDuration = sc.HasKey("InfiniteDuration") ? sc["InfiniteDuration"].AsBool : false; // USUNIĘTE
                    st.Group = sc.HasKey("Group") ? sc["Group"].Value : "Group_1";
                    st.SetFlags = sc.HasKey("SetFlags") ? sc["SetFlags"].Value : "";
                    st.IsWalkingEnabled = sc.HasKey("IsWalkingEnabled") ? sc["IsWalkingEnabled"].AsBool : false;

                    if (st.EnterTrigger != null && !string.IsNullOrEmpty(st.EnterTrigger.Name) && sc.HasKey(st.EnterTrigger.Name))
                        st.EnterTrigger.RestoreFromJSON(sc, owner.subScenePrefix, owner.mergeRestore, true);
                    if (st.ValueTrigger != null && !string.IsNullOrEmpty(st.ValueTrigger.Name) && sc.HasKey(st.ValueTrigger.Name))
                        st.ValueTrigger.RestoreFromJSON(sc, owner.subScenePrefix, owner.mergeRestore, true);
                    if (st.ExitTrigger != null && !string.IsNullOrEmpty(st.ExitTrigger.Name) && sc.HasKey(st.ExitTrigger.Name))
                        st.ExitTrigger.RestoreFromJSON(sc, owner.subScenePrefix, owner.mergeRestore, true);

                    stateManager.AddState(st);
                }
                stateManager.SortStates();

                for (int i = 0; i < sclist.Count; i++)
                {
                    JSONClass sc = sclist[i].AsObject;
                    if (sc == null || !sc.HasKey("Name")) continue;
                    RoutimatorState.State st = stateManager.GetStateGlobal(sc["Name"].Value);
                    if (st == null) continue;

                    if (sc.HasKey("Transitions"))
                    {
                        JSONArray tlist = sc["Transitions"].AsArray;
                        for (int t = 0; t < tlist.Count; t++)
                        {
                            if (string.IsNullOrEmpty(tlist[t].Value)) continue;
                            RoutimatorState.State trans = stateManager.GetStateGlobal(tlist[t].Value);
                            if (trans != null)
                                st.Transitions.Add(trans);
                        }
                        st.Transitions.Sort(RoutimatorState.State.SortByNameAscending);
                    }
                }
            }

            if (jsonClass.HasKey("NodePositions"))
            {
                JSONArray nodePositionsArray = jsonClass["NodePositions"].AsArray;
                for (int i = 0; i < nodePositionsArray.Count; i++)
                {
                    JSONClass nodePosJson = nodePositionsArray[i].AsObject;
                    if (nodePosJson == null || !nodePosJson.HasKey("StateName")) continue;
                    string stateName = nodePosJson["StateName"].Value;
                    float posX = nodePosJson.HasKey("PositionX") ? nodePosJson["PositionX"].AsFloat : 0f;
                    float posY = nodePosJson.HasKey("PositionY") ? nodePosJson["PositionY"].AsFloat : 0f;
                    if (!string.IsNullOrEmpty(stateName))
                    {
                        nodePositionsToRestore[stateName] = new Vector2(posX, posY);
                    }
                }
            }

            var groups = owner.GetGroups();
            groups.Clear();
            foreach (RoutimatorState.State s_state in stateManager.GetStates())
            {
                if (!string.IsNullOrEmpty(s_state.Group) && !groups.Contains(s_state.Group))
                {
                    groups.Add(s_state.Group);
                }
            }
            if (groups.Count == 0) groups.Add("Group_1");

            owner.GetGroupChooser().choices = new List<string>(groups);

            if (!owner.GetGroupChooser().choices.Contains(owner.GetGroupChooser().val))
            {
                owner.GetGroupChooser().valNoCallback = owner.GetGroupChooser().choices.Count > 0 ? owner.GetGroupChooser().choices[0] : "";
            }

            RoutimatorState.State initialState = null;
            if (jsonClass.HasKey("InitialState"))
            {
                initialState = stateManager.GetStateGlobal(jsonClass["InitialState"].Value);
            }
            ((Routimator)owner).ClearAndSetInitialPluginState(initialState); // Zmienione na nową metodę
        }
    }
}
// --- END OF FILE RoutimatorSerialization.cs ---