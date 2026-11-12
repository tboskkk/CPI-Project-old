using System;
using System.IO;

// A shipped client's content state file and baseline catalog, kept per client version and platform so a later
// "Generate CDN Update" can build against exactly what that client shipped with. A release artifact: never committed.
public static class ContentArchive
{
	public const string RootEnvironmentVariable = "CPI_CONTENT_ARCHIVE";

	// <root>/<clientVersion>/<Platform>, where Platform is the Unity build target name (StandaloneLinux64, Android...).
	public static string FolderFor(string root, string clientVersion, string platform)
	{
		if (string.IsNullOrEmpty(clientVersion) || string.IsNullOrEmpty(platform) || clientVersion.IndexOfAny(new[] { '/', '\\' }) >= 0 || platform.IndexOfAny(new[] { '/', '\\' }) >= 0 || clientVersion == ".." || platform == "..")
		{
			throw new ArgumentException("bad client version or platform for the content archive");
		}
		return Path.Combine(root, clientVersion, platform);
	}

	public static string DefaultRoot()
	{
		string fromEnvironment = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
		if (!string.IsNullOrEmpty(fromEnvironment))
		{
			return fromEnvironment;
		}
		return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "ContentArchive"));
	}

	// Copies the state file and every catalog_* file in serverDataFolder into the archive folder. Returns that folder.
	public static string Archive(string root, string clientVersion, string platform, string statePath, string serverDataFolder)
	{
		if (!File.Exists(statePath))
		{
			throw new FileNotFoundException("content state file not found", statePath);
		}
		string[] catalogs = Directory.Exists(serverDataFolder) ? Directory.GetFiles(serverDataFolder, "catalog_*") : new string[0];
		if (catalogs.Length == 0)
		{
			throw new FileNotFoundException("no catalog_* files in " + serverDataFolder);
		}
		string folder = FolderFor(root, clientVersion, platform);
		Directory.CreateDirectory(folder);
		File.Copy(statePath, Path.Combine(folder, "addressables_content_state.bin"), true);
		foreach (string catalog in catalogs)
		{
			File.Copy(catalog, Path.Combine(folder, Path.GetFileName(catalog)), true);
		}
		return folder;
	}

	public static string StatePathIn(string root, string clientVersion, string platform)
	{
		return Path.Combine(FolderFor(root, clientVersion, platform), "addressables_content_state.bin");
	}
}
