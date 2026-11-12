using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// Headless checks for the CDN update tooling:
//   [CDN_TEST_STATE=<an addressables_content_state.bin>] Unity -batchmode -nographics -projectPath <proj> -executeMethod CdnUpdateChecks.Run -logFile <log>
// The gather check needs a real state file (CDN_TEST_STATE); without one it is reported as SKIPPED.
public static class CdnUpdateChecks
{
	private static int failures;

	private static void Check(bool ok, string what)
	{
		Debug.Log((ok ? "CHECK OK: " : "CHECK FAILED: ") + what);
		if (!ok) failures++;
	}

	private static bool Throws(Action action)
	{
		try { action(); return false; } catch (Exception) { return true; }
	}

	public static void Run()
	{
		try
		{
			Archive();
			Gather();
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			failures++;
		}
		Debug.Log("CHECKS DONE failures=" + failures);
		EditorApplication.Exit(failures == 0 ? 0 : 1);
	}

	private static void Archive()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "cdn-archive-" + Guid.NewGuid().ToString("N"));
		string serverData = Path.Combine(tmp, "ServerData");
		Directory.CreateDirectory(serverData);
		string state = Path.Combine(tmp, "state.bin");
		File.WriteAllText(state, "STATE");
		File.WriteAllText(Path.Combine(serverData, "catalog_1.17.0.bin"), "CAT");
		File.WriteAllText(Path.Combine(serverData, "catalog_1.17.0.hash"), "0123456789abcdef0123456789abcdef");
		File.WriteAllText(Path.Combine(serverData, "other.bundle"), "B");
		string root = Path.Combine(tmp, "archive");
		string folder = ContentArchive.Archive(root, "1.17.0", "StandaloneLinux64", state, serverData);
		Check(folder == Path.Combine(root, "1.17.0", "StandaloneLinux64"), "archive folder is <root>/<client version>/<platform>");
		Check(File.ReadAllText(Path.Combine(folder, "addressables_content_state.bin")) == "STATE" && File.Exists(Path.Combine(folder, "catalog_1.17.0.bin")) && File.Exists(Path.Combine(folder, "catalog_1.17.0.hash")), "state file and catalog + hash are archived");
		Check(!File.Exists(Path.Combine(folder, "other.bundle")), "bundles are not archived");
		Check(ContentArchive.StatePathIn(root, "1.17.0", "StandaloneLinux64") == Path.Combine(folder, "addressables_content_state.bin"), "StatePathIn names the archived state file");
		Check(Throws(() => ContentArchive.FolderFor(root, "..", "Android")) && Throws(() => ContentArchive.FolderFor(root, "1.0", "a/b")) && Throws(() => ContentArchive.FolderFor(root, "", "Android")), "unsafe client version or platform is refused");
		Check(Throws(() => ContentArchive.Archive(root, "1.17.0", "Android", Path.Combine(tmp, "missing.bin"), serverData)), "a missing state file is refused");
		Check(Throws(() => ContentArchive.Archive(root, "1.17.0", "Android", state, Path.Combine(tmp, "empty"))), "a ServerData folder with no catalog is refused");
		Directory.Delete(tmp, true);
	}

	private static void Gather()
	{
		string state = Environment.GetEnvironmentVariable("CDN_TEST_STATE");
		if (string.IsNullOrEmpty(state) || !File.Exists(state))
		{
			Debug.Log("CHECK SKIPPED: gather with a missing group (set CDN_TEST_STATE to a content state file)");
			return;
		}
		AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
		GenerateAddressables.ConfigureRemoteCatalog(settings);
		settings.groups.Add(null);
		Check(Throws(() => ContentUpdateScript.GatherModifiedEntries(settings, state)), "stock GatherModifiedEntries throws when a group reference is missing (why the guard exists)");
		settings.groups.Add(null);
		var gathered = ModifiedEntryGatherer.Gather(settings, state);
		Check(gathered != null && settings.groups.TrueForAll(g => g != null), "ModifiedEntryGatherer.Gather returns a list and leaves no null group");
	}
}
