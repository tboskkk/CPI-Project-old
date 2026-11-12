using System;
using System.Collections.Generic;
using Disney.Kelowna.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// Headless checks for the remote catalog setup (the project has no test framework package):
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod RemoteCatalogChecks.Run -logFile <log>
// Exits 0 if every check passes, 1 otherwise. Failures are logged as "CHECK FAILED: ...".
public static class RemoteCatalogChecks
{
    private static int failures;

    private static void Check(bool ok, string what)
    {
        Debug.Log((ok ? "CHECK OK: " : "CHECK FAILED: ") + what);
        if (!ok) failures++;
    }

    public static void Run()
    {
        try
        {
            Redirect();
            BuildConfiguration();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            failures++;
        }
        Debug.Log("CHECKS DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Redirect()
    {
        const string root = "https://h.example/cdn.disney.io";
        string hash = "https://cdn.invalid/Android/catalog_1.17.0.hash";
        RemoteCatalogRedirect.SetRelease(root, "rel1");
        Check(RemoteCatalogRedirect.Transform(hash) == root + "/rel1/Android/catalog_1.17.0.hash", "valid release id rewrites the placeholder host");
        Check(RemoteCatalogRedirect.Transform("https://other.example/x") == "https://other.example/x" && RemoteCatalogRedirect.Transform("Assets/a.bundle") == "Assets/a.bundle", "other ids pass through");
        Check(RemoteCatalogRedirect.Transform("https://cdn.invalid.evil.example/x") == "https://cdn.invalid.evil.example/x", "a host that merely starts with the placeholder isn't rewritten");
        foreach (string bad in new[] { "../x", "", null, "embedded", "a/b", new string('x', 41), "-x" })
        {
            RemoteCatalogRedirect.SetRelease(root, bad);
            Check(RemoteCatalogRedirect.ReleaseId == null && RemoteCatalogRedirect.Transform(hash) == hash, "release id " + (bad == null ? "null" : "\"" + bad + "\"") + " leaves everything unchanged");
        }
        RemoteCatalogRedirect.SetRelease(null, "rel1");
        Check(RemoteCatalogRedirect.Transform(hash) == hash, "no CDN root leaves everything unchanged");
        Check(RemoteCatalogRedirect.CdnRootFrom("https://h.example/cdn.disney.io/0123456789abcdef/ClientManifestDirectory.json?access_token=x") == "https://h.example/cdn.disney.io", "CDN root is the directory URL minus hash and file, query ignored");
        Check(RemoteCatalogRedirect.CdnRootFrom("http://h.example:8080/a/b/c/h2/ClientManifestDirectory.json") == "http://h.example:8080/a/b/c", "CDN root keeps port and a longer path");
        Check(RemoteCatalogRedirect.CdnRootFrom("not a url") == null && RemoteCatalogRedirect.CdnRootFrom("https://h.example/x.json") == null, "a URL without the expected shape gives no root");
        RemoteCatalogRedirect.SetRelease(null, null);
    }

    private static void BuildConfiguration()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        GenerateAddressables.ConfigureRemoteCatalog(settings);
        Check(settings.BuildRemoteCatalog, "remote catalog is on");
        Check(settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath) == "https://cdn.invalid/[BuildTarget]" && settings.RemoteCatalogLoadPath.GetName(settings) == AddressableAssetSettings.kRemoteLoadPath, "remote load path is the placeholder host");
        Check(settings.profileSettings.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath) == "ServerData/[BuildTarget]" && settings.RemoteCatalogBuildPath.GetName(settings) == AddressableAssetSettings.kRemoteBuildPath, "remote build path is ServerData/[BuildTarget]");
        Check(settings.CatalogRequestsTimeout == 5, "catalog request timeout is short");
        int groups = 0, bad = 0, noSchemas = 0;
        List<string> noSchemaNames = new List<string>();
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            if (group.Schemas.Count == 0) { noSchemas++; noSchemaNames.Add(group.Name); continue; }
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            ContentUpdateGroupSchema update = group.GetSchema<ContentUpdateGroupSchema>();
            if (bundled != null && (bundled.UseAssetBundleCrc || bundled.UseAssetBundleCrcForCachedBundles)) bad++;
            if (update != null && !update.StaticContent) bad++;
            groups++;
        }
        Check(groups > 0 && bad == 0, "every group with schemas is static with CRC checks off (" + groups + " groups)");
        Debug.Log("INFO groups with no schemas: " + noSchemas + " [" + string.Join(", ", noSchemaNames) + "]");
    }
}
