using System;
using System.IO;
using System.Linq;
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
			Update();
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

	private static void Update()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "cdn-update-" + Guid.NewGuid().ToString("N"));
		string folder = Path.Combine(tmp, "Android");
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, "catalog_1.17.0.bin"), "CAT");
		File.WriteAllText(Path.Combine(folder, "catalog_1.17.0.hash"), "0123456789abcdef0123456789abcdef");
		File.WriteAllText(Path.Combine(folder, "a.bundle"), "B");
		string zipPath = GenerateCdnUpdate.ZipUpdate(folder, Path.Combine(tmp, "out", "u.zip"));
		string[] names;
		using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath)) names = zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
		Check(string.Join(",", names) == "Android/a.bundle,Android/catalog_1.17.0.bin,Android/catalog_1.17.0.hash", "zip holds <Platform>/<file> entries with forward slashes: " + string.Join(",", names));
		File.WriteAllText(Path.Combine(folder, "catalog_1.18.0.bin"), "CAT2");
		Check(Throws(() => GenerateCdnUpdate.ZipUpdate(folder, Path.Combine(tmp, "two.zip"))), "two catalogs in the output are refused");
		File.Delete(Path.Combine(folder, "catalog_1.18.0.bin"));
		File.Delete(Path.Combine(folder, "catalog_1.17.0.hash"));
		Check(Throws(() => GenerateCdnUpdate.ZipUpdate(folder, Path.Combine(tmp, "nohash.zip"))), "a catalog without its hash is refused");
		Directory.Delete(tmp, true);

		string state = Environment.GetEnvironmentVariable("CDN_TEST_STATE");
		string realState = !string.IsNullOrEmpty(state) && File.Exists(state) ? state : Path.Combine(tmp, "x.bin");
		string version = PlayerSettings.bundleVersion;
		string message = "";
		try { GenerateCdnUpdate.BuildUpdate("0.0.1", EditorUserBuildSettings.activeBuildTarget, realState, Path.Combine(tmp, "o.zip")); } catch (Exception e) { message = e.Message; }
		Check(message.Contains("0.0.1") && message.Contains(version), "an update for the wrong client version is refused, naming both versions");
		message = "";
		try { GenerateCdnUpdate.BuildUpdate(version, BuildTarget.Android == EditorUserBuildSettings.activeBuildTarget ? BuildTarget.iOS : BuildTarget.Android, realState, Path.Combine(tmp, "o.zip")); } catch (Exception e) { message = e.Message; }
		Check(message.Contains("switch platform"), "an update for another build target than the active one is refused");
		Check(Throws(() => GenerateCdnUpdate.BuildUpdate(version, EditorUserBuildSettings.activeBuildTarget, Path.Combine(tmp, "missing.bin"), Path.Combine(tmp, "o.zip"))), "a missing state file is refused");
		if (string.IsNullOrEmpty(state) || !File.Exists(state))
		{
			Debug.Log("CHECK SKIPPED: nothing-to-build with a real state file (set CDN_TEST_STATE)");
			return;
		}
		string nothing = GenerateCdnUpdate.BuildUpdate(version, EditorUserBuildSettings.activeBuildTarget, state, Path.Combine(Path.GetTempPath(), "cdn-nothing.zip"));
		Check(nothing == null && !File.Exists(Path.Combine(Path.GetTempPath(), "cdn-nothing.zip")), "with no asset changed since the state was saved, there is nothing to build and no zip");
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
