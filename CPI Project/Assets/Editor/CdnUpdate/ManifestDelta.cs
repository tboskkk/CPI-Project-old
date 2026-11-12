using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// New content keys (assets added since a client shipped) reach the update's catalog by themselves, but the client's
// content manifest has no entry mapping each key to its Addressables address. This writes those entries as a
// TextAsset (RemoteCatalogRedirect.ManifestDeltaAddress) that goes into the update's catalog; the client loads and merges it.
public static class ManifestDelta
{
	public const string AssetFolder = "Assets/Generated/CdnUpdate";

	public static string BundleKeyFor(BuildTarget target)
	{
		return "assetbundles/generated/" + target.ToString().ToLowerInvariant() + "/cdn/delta.sa.unity3d";
	}

	// Manifest lines for the given asset paths ("Assets/..."), in the generator's own format, plus the bundle line.
	public static string BuildText(IEnumerable<string> assetPaths, BuildTarget target)
	{
		string bundleKey = BundleKeyFor(target);
		StringBuilder text = new StringBuilder();
		HashSet<string> written = new HashSet<string>();
		foreach (string path in assetPaths.OrderBy(p => p, StringComparer.Ordinal))
		{
			string line = GenerateManifest.BuildBundleAssetLine(path, bundleKey);
			if (written.Add(line))
			{
				text.Append(line).Append('\n');
			}
		}
		if (written.Count > 0)
		{
			text.Append("bundle:").Append(bundleKey).Append("?d=&p=0\n");
		}
		return text.ToString();
	}

	// Addressable assets whose guid wasn't in the state the shipped client was built from.
	public static List<string> NewAssetPaths(AddressableAssetSettings settings, string statePath)
	{
		AddressablesContentState state = ContentUpdateScript.LoadContentState(statePath);
		if (state == null)
		{
			throw new InvalidOperationException("could not read the content state file " + statePath);
		}
		HashSet<string> shipped = new HashSet<string>(state.cachedInfos.Select(info => info.asset.guid.ToString()));
		List<string> found = new List<string>();
		foreach (AddressableAssetGroup group in settings.groups)
		{
			if (group == null)
			{
				continue;
			}
			foreach (AddressableAssetEntry entry in group.entries)
			{
				List<AddressableAssetEntry> assets = new List<AddressableAssetEntry>();
				entry.GatherAllAssets(assets, true, true, false);
				foreach (AddressableAssetEntry asset in assets)
				{
					string path = asset.AssetPath;
					if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path) || shipped.Contains(asset.guid) || path.StartsWith(AssetFolder + "/", StringComparison.Ordinal))
					{
						continue;
					}
					found.Add(path);
				}
			}
		}
		return found.Distinct().ToList();
	}

	// Writes the delta TextAsset, makes it an addressable entry in the given group and returns that entry.
	public static AddressableAssetEntry AddToGroup(AddressableAssetSettings settings, AddressableAssetGroup group, string text)
	{
		Directory.CreateDirectory(AssetFolder);
		string path = AssetFolder + "/ContentManifestDelta.txt";
		File.WriteAllText(path, text);
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
		AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
		entry.address = Disney.Kelowna.Common.RemoteCatalogRedirect.ManifestDeltaAddress;
		foreach (string label in new List<string>(entry.labels))
		{
			entry.SetLabel(label, false, false);
		}
		return entry;
	}
}
