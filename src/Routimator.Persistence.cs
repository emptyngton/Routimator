// Routimator.Persistence.cs
// Scene-save JSON (GetJSON / LateRestoreFromJSON), .rm preset save/load UI and dialogs,
// and node-position caching for graph layout persistence.

using UnityEngine;
using System.Collections.Generic;
using SimpleJSON;
using MacGruber;
using MVR.FileManagementSecure;

namespace Routimator
{
    public partial class Routimator : MVRScript
    {
        // ====================================================================
        // FIELDS — Save/Load & graph node-position cache
        // ====================================================================
        private JSONStorableUrl loadURL;
        private const string BASE_DIRECTORY = "Saves/PluginData/Routimator";
        private Dictionary<string, Vector2> savedNodePositions = new Dictionary<string, Vector2>();

        // ====================================================================
        // SAVE / LOAD INITIALIZATION (called from Init())
        // ====================================================================
        private void InitializeSaveLoadFunctionality()
        {
            FileManagerSecure.CreateDirectory(BASE_DIRECTORY);
            loadURL = new JSONStorableUrl("loadURL", "", UILoadJSON, "rm", true);
            loadURL.hideExtension = true;
            loadURL.allowFullComputerBrowse = false;
            loadURL.allowBrowseAboveSuggestedPath = true;
            loadURL.SetFilePath(BASE_DIRECTORY + "/");
            if (!loadURL.val.EndsWith(".rm")) loadURL.val = "";
            CreateSaveLoadUI();
        }

        private void CreateSaveLoadUI()
        {
            UIDynamicTwinButton twin = Utils.SetupTwinButton(this, "Save", UISaveJSONDialog, "Load", delegate { }, false);
            loadURL.RegisterFileBrowseButton(twin.buttonRight);
        }

        // ====================================================================
        // SAVE / LOAD DIALOGS
        // ====================================================================
        private void UISaveJSONDialog()
        {
            SuperController sc = SuperController.singleton;
            sc.GetMediaPathDialog(UISaveJSON, "rm", BASE_DIRECTORY, false, true, false, null, true, null, false, false);
            sc.mediaFileBrowserUI.SetTextEntry(true);
            if (sc.mediaFileBrowserUI.fileEntryField != null)
            {
                string filename = System.DateTime.Now.ToString("yyyyMMdd_HH-mm-ss");
                sc.mediaFileBrowserUI.fileEntryField.text = filename;
                sc.mediaFileBrowserUI.ActivateFileNameField();
            }
        }

        private void UISaveJSON(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string filePath = path.Replace('\\', '/') + ".rm";
            JSONClass jc = serialization.GetJSON();
            serialization.SaveJSON(jc, filePath);
            Logger.Log("state saved to " + filePath);
        }

        private void UILoadJSON(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.EndsWith(".rm"))
            {
                Logger.Log("No valid preset file selected.");
                return;
            }
            JSONClass jc = serialization.LoadJSON(url).AsObject;
            if (jc != null)
            {
                // Deregister old navigation actions before replacing the state list.
                // Copy keys to avoid collection-modified-during-iteration errors.
                List<string> actionKeysToDeregister = new List<string>(stateNavigationActions.Keys);
                foreach (string actionKey in actionKeysToDeregister)
                {
                    JSONStorableAction actionToDeregister = stateNavigationActions[actionKey];
                    DeregisterAction(actionToDeregister);
                    stateNavigationActions.Remove(actionKey);
                }

                ClearAndSetInitialPluginState();

                serialization.LateRestoreFromJSON(jc, this.savedNodePositions);
                foreach (RoutimatorState.State loadedState in stateManager.GetStates())
                    RegisterNavigateToActions(loadedState);
                ui.UIRebuild();
                RebuildAnimationChooserUI();
                Logger.Log("state loaded from " + url);
            }
            else
            {
                SuperController.LogError("Routimator: File " + url + " not found or could not be loaded.");
            }
        }

        // ====================================================================
        // SCENE-LEVEL SERIALIZATION — persists alongside VaM scene saves
        // ====================================================================
        public override JSONClass GetJSON(bool MG_includePhysical = true, bool MG_includeAppearance = true, bool MG_forceStore = false)
        {
            JSONClass pluginDataJson = serialization.GetJSON(MG_includePhysical, MG_includeAppearance, MG_forceStore, this.savedNodePositions);
            if (pluginDataJson != null && pluginDataJson.Count > 0) this.needsStore = true;
            else if (MG_forceStore) { this.needsStore = true; if (pluginDataJson == null) pluginDataJson = new JSONClass(); }
            return pluginDataJson;
        }

        public override void LateRestoreFromJSON(JSONClass jsonClass, bool MG_restorePhysical = true, bool MG_restoreAppearance = true, bool MG_setMissingToDefault = true)
        {
            base.LateRestoreFromJSON(jsonClass, MG_restorePhysical, MG_restoreAppearance, MG_setMissingToDefault);

            // Deregister all existing (old) navigation actions before the state list is replaced.
            // Copy keys to avoid collection-modified-during-iteration errors.
            List<string> actionKeysToDeregister = new List<string>(stateNavigationActions.Keys);
            foreach (string actionKey in actionKeysToDeregister)
            {
                JSONStorableAction actionToDeregister = stateNavigationActions[actionKey];
                // DeregisterAction should be tolerant, but double-check VAM still has it.
                if (actionToDeregister != null && GetAction(actionToDeregister.name) != null)
                    DeregisterAction(actionToDeregister);
            }
            stateNavigationActions.Clear();

            ClearAndSetInitialPluginState(null);
            serialization.LateRestoreFromJSON(jsonClass, this.savedNodePositions);

            foreach (RoutimatorState.State loadedState in stateManager.GetStates())
                RegisterNavigateToActions(loadedState);

            ui.UIRebuild();
            RebuildAnimationChooserUI();
            if (graphVisualizer != null && graphVisualizer.IsVisible())
                graphVisualizer.UpdateGraph();
            MarkAsModified();
        }

        // ====================================================================
        // NODE-POSITION CACHE — feeds the graph visualizer's persistent layout
        // ====================================================================
        public void SaveNodePosition(string stateName, Vector2 position)
        {
            if (!string.IsNullOrEmpty(stateName)) savedNodePositions[stateName] = position;
        }

        public Vector2? GetSavedNodePosition(string stateName)
        {
            Vector2 pos;
            return savedNodePositions.TryGetValue(stateName, out pos) ? pos : (Vector2?)null;
        }

        public void ClearSavedNodePositions() { savedNodePositions.Clear(); }
    }
}
