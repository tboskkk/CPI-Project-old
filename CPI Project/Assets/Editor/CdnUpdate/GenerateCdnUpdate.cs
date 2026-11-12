using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// Builds a CDN content update (replaced and added assets) against the state a shipped client was built with, and
// zips it in the layout the server imports: <Platform>/catalog_<version>.bin, its .hash, and the changed .bundle files.
//
// Run it in a checkout separate from the one the client shipped from: the update build rewrites donor bundles under
// StreamingAssets. Menu: Project/AssetBundles/Generate CDN Update. Headless:
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod GenerateCdnUpdate.RunHeadless
// (CDN_UPDATE_STATE overrides the archived state file, CDN_UPDATE_ZIP the output path.)
public static class GenerateCdnUpdate
{
	// Returns the zip's path, or null when no asset changed since the state file was saved.
	public static string BuildUpdate(string clientVersion, BuildTarget target, string statePath, string outZipPath)
	{
		if (PlayerSettings.bundleVersion != clientVersion)
		{
			throw new InvalidOperationException("this project is version " + PlayerSettings.bundleVersion + " but the update is for client " + clientVersion + "; check out the shipped client's version first");
		}
		if (EditorUserBuildSettings.activeBuildTarget != target)
		{
			throw new InvalidOperationException("active build target is " + EditorUserBuildSettings.activeBuildTarget + ", not " + target + "; switch platform first");
		}
		if (!File.Exists(statePath))
		{
			throw new FileNotFoundException("content state file not found", statePath);
		}

		AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
		GenerateAddressables.ConfigureRemoteCatalog(settings);
		for (int i = 0; i < settings.DataBuilders.Count; i++)
		{
			if (settings.DataBuilders[i] is AddressableBundleNameBuilder)
			{
				settings.ActivePlayerDataBuilderIndex = i;
				break;
			}
		}

		List<AddressableAssetEntry> modified = ModifiedEntryGatherer.Gather(settings, statePath);
		List<string> newAssetPaths = ManifestDelta.NewAssetPaths(settings, statePath);
		if (modified.Count == 0 && newAssetPaths.Count == 0)
		{
			Debug.Log("GenerateCdnUpdate: no asset changed since the content state was saved; nothing to build");
			return null;
		}
		string groupName = "Content Update " + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
		List<string> modifiedGuids = modified.Select(entry => entry.guid).ToList();
		AddressableAssetGroup updateGroup;
		if (modified.Count > 0)
		{
			ContentUpdateScript.CreateContentUpdateGroup(settings, modified, groupName);
			updateGroup = settings.FindGroup(groupName);
		}
		else
		{
			updateGroup = settings.CreateGroup(groupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
			BundledAssetGroupSchema bundled = updateGroup.GetSchema<BundledAssetGroupSchema>();
			bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
			bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
			bundled.UseAssetBundleCrc = false;
			bundled.UseAssetBundleCrcForCachedBundles = false;
			updateGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
		}
		// The project's name builder names bundles from the legacy ".sa.unity3d.txt" label, which would give the update
		// bundle its donor group's name and collide with it. The moved entries drop that label.
		foreach (string guid in modifiedGuids)
		{
			AddressableAssetEntry moved = settings.FindAssetEntry(guid);
			foreach (string label in new List<string>(moved.labels))
			{
				moved.SetLabel(label, false, false);
			}
		}
		if (newAssetPaths.Count > 0)
		{
			// Assets added since the client shipped sit in static groups, which a content update doesn't build:
			// they move to the update group, and their manifest entries go in as a delta the client merges.
			foreach (string path in newAssetPaths)
			{
				AddressableAssetEntry added = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), updateGroup, false, false);
				added.address = path.ToLowerInvariant();
				foreach (string label in new List<string>(added.labels))
				{
					added.SetLabel(label, false, false);
				}
			}
			ManifestDelta.AddToGroup(settings, updateGroup, ManifestDelta.BuildText(newAssetPaths, target));
			Debug.Log("GenerateCdnUpdate: " + newAssetPaths.Count + " new asset(s) added with a manifest delta");
		}

		string outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ServerData", target.ToString()));
		if (Directory.Exists(outputFolder))
		{
			Directory.Delete(outputFolder, true);
		}
		AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
		if (!string.IsNullOrEmpty(result.Error))
		{
			throw new InvalidOperationException("content update build failed: " + result.Error);
		}
		return ZipUpdate(outputFolder, outZipPath);
	}

	// Zips <outputFolder> as <folder name>/<file>. Checks the folder has exactly one catalog with its hash.
	public static string ZipUpdate(string outputFolder, string outZipPath)
	{
		string[] catalogs = Directory.GetFiles(outputFolder, "catalog_*.bin").Concat(Directory.GetFiles(outputFolder, "catalog_*.json")).ToArray();
		if (catalogs.Length != 1 || !File.Exists(Path.ChangeExtension(catalogs[0], ".hash")))
		{
			throw new InvalidOperationException("expected exactly one catalog and its .hash in " + outputFolder + ", found " + catalogs.Length + " catalog(s)");
		}
		Directory.CreateDirectory(Path.GetDirectoryName(outZipPath));
		if (File.Exists(outZipPath))
		{
			File.Delete(outZipPath);
		}
		string platform = new DirectoryInfo(outputFolder).Name;
		using (ZipArchive zip = ZipFile.Open(outZipPath, ZipArchiveMode.Create))
		{
			foreach (string file in Directory.GetFiles(outputFolder).OrderBy(f => f, StringComparer.Ordinal))
			{
				zip.CreateEntryFromFile(file, platform + "/" + Path.GetFileName(file));
			}
		}
		return outZipPath;
	}

	private static string DefaultZipPath(string clientVersion, BuildTarget target)
	{
		return Path.Combine(ContentArchive.DefaultRoot(), clientVersion, "updates", target + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");
	}

	[MenuItem("Project/AssetBundles/Generate CDN Update")]
	private static void RunFromMenu()
	{
		string version = PlayerSettings.bundleVersion;
		BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
		try
		{
			string state = ContentArchive.StatePathIn(ContentArchive.DefaultRoot(), version, target.ToString());
			string zip = BuildUpdate(version, target, state, DefaultZipPath(version, target));
			EditorUtility.DisplayDialog("Generate CDN Update", zip == null ? "No asset changed since client " + version + " shipped. Nothing to build." : "Update written to\n" + zip + "\n\nUpload it on the admin panel's CDN page, then point the client's row at it.", "OK");
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			EditorUtility.DisplayDialog("Generate CDN Update failed", e.Message, "OK");
		}
	}

	public static void RunHeadless()
	{
		int exitCode = 1;
		try
		{
			string version = PlayerSettings.bundleVersion;
			BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
			string state = Environment.GetEnvironmentVariable("CDN_UPDATE_STATE");
			if (string.IsNullOrEmpty(state))
			{
				state = ContentArchive.StatePathIn(ContentArchive.DefaultRoot(), version, target.ToString());
			}
			string outZip = Environment.GetEnvironmentVariable("CDN_UPDATE_ZIP");
			if (string.IsNullOrEmpty(outZip))
			{
				outZip = DefaultZipPath(version, target);
			}
			string zip = BuildUpdate(version, target, state, outZip);
			Debug.Log(zip == null ? "GenerateCdnUpdate: nothing to build" : "GenerateCdnUpdate: wrote " + zip);
			exitCode = 0;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
		}
		EditorApplication.Exit(exitCode);
	}
}
