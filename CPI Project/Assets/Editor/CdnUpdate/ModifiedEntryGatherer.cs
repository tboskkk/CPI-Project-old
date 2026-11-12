using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// ContentUpdateScript.GatherModifiedEntries throws a NullReferenceException when the settings list a group whose
// asset is missing. This project's settings reference three such groups (their GUIDs resolve to no asset), so the
// null entries are dropped from the in-memory list first. The settings asset on disk is not rewritten by this.
public static class ModifiedEntryGatherer
{
	public static List<AddressableAssetEntry> Gather(AddressableAssetSettings settings, string statePath)
	{
		int dropped = settings.groups.RemoveAll(group => group == null);
		if (dropped > 0)
		{
			Debug.LogWarning("ModifiedEntryGatherer: ignored " + dropped + " group reference(s) whose asset is missing from AddressableAssetSettings");
		}
		return ContentUpdateScript.GatherModifiedEntries(settings, statePath);
	}
}
