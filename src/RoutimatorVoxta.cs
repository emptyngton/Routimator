using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

namespace Routimator
{
    public class RoutimatorVoxta
    {
        private MVRScript owner;

        // Voxta plugin fields
        private JSONStorable voxtaStorable; // Reference to Voxta plugin

        public RoutimatorVoxta(MVRScript owner)
        {
            this.owner = owner;
        }

        // Modified to loop until the Voxta plugin is found
        public IEnumerator FindVoxtaPlugin()
        {
            while (true)
            {
                foreach (Atom atom in SuperController.singleton.GetAtoms())
                {
                    // Check for the Voxta plugin storable by its identifier
                    string voxtaStorableID = atom.GetStorableIDs().FirstOrDefault(x => x.Contains("Voxta"));
                    if (!string.IsNullOrEmpty(voxtaStorableID))
                    {
                        voxtaStorable = atom.GetStorableByID(voxtaStorableID);
                        if (voxtaStorable != null)
                        {
                            Logger.Log("Voxta plugin found on atom: " + atom.name);
                            break;
                        }
                    }
                }

                if (voxtaStorable != null)
                    break;

                yield return null; // Wait a frame before trying again
            }
        }

        // Set flags in Voxta plugin
        public void TrySetVoxtaFlags(string flags)
        {
            if (string.IsNullOrEmpty(flags))
                return;

            if (voxtaStorable == null)
                return;

            JSONStorableString flagsStorable = voxtaStorable.GetStringJSONParam("SetFlags");
            if (flagsStorable != null)
            {
                flagsStorable.val = flags;
                Logger.Log("Voxta flags set: " + flags);
            }
            else
            {
                SuperController.LogError("Voxta JSONStorable 'SetFlags' not found.");
            }
        }

        // Get the Voxta plugin reference
        public JSONStorable GetVoxtaStorable()
        {
            return voxtaStorable;
        }

        // Parse flag string into a list of flags
        public List<string> ParseFlagString(string flagString)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrEmpty(flagString))
                return result;

            string[] flagArray = flagString.Split(',');
            foreach (string flag in flagArray)
            {
                string trimmedFlag = flag.Trim();
                if (!string.IsNullOrEmpty(trimmedFlag))
                    result.Add(trimmedFlag);
            }

            return result;
        }

        // Combine flag lists into a flag string
        public string CombineFlagLists(List<string> flagsToSet, List<string> flagsToUnset)
        {
            List<string> allFlags = new List<string>();

            if (flagsToSet != null)
            {
                foreach (string flag in flagsToSet)
                {
                    allFlags.Add(flag);
                }
            }

            if (flagsToUnset != null)
            {
                foreach (string flag in flagsToUnset)
                {
                    allFlags.Add("!" + flag);
                }
            }

            return string.Join(",", allFlags.ToArray());
        }
    }
}