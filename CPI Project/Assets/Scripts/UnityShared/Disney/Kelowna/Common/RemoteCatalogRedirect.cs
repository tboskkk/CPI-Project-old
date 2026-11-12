using System;
using System.IO;
using System.Text.RegularExpressions;
using Disney.Kelowna.Common.Manifest;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Disney.Kelowna.Common
{
	// The client's Addressables remote catalog is built against a placeholder host
	// (https://cdn.invalid/<BuildTarget>) so no server address is baked in. When the
	// server's directory row names a content release, requests to the placeholder go
	// to <server CDN root>/<release id>/<BuildTarget>/... instead. With no release the
	// placeholder is left alone, the catalog request fails, and the embedded catalog
	// is used, exactly as before the remote catalog existed. Addressables would still
	// load a catalog cached from an earlier release when the request fails, so with no
	// release the cached catalogs are removed first (ClearCachedCatalogs): a release the
	// server withdraws stops being used on the clients that already loaded it.
	public static class RemoteCatalogRedirect
	{
		public const string PlaceholderHost = "https://cdn.invalid";

		// A content release that adds new content keys carries their manifest entries as this TextAsset.
		public const string ManifestDeltaAddress = "cdn/contentmanifest-delta";

		private static readonly Regex releaseIdPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$");

		private static string cdnRoot;

		public static string ReleaseId { get; private set; }

		public static void SetRelease(string cdnRoot, string releaseId)
		{
			ReleaseId = null;
			RemoteCatalogRedirect.cdnRoot = null;
			if (!string.IsNullOrEmpty(cdnRoot) && !string.IsNullOrEmpty(releaseId) && releaseId != ContentManifestDirectory.EMBEDDED_CONTENT && releaseIdPattern.IsMatch(releaseId))
			{
				RemoteCatalogRedirect.cdnRoot = cdnRoot.TrimEnd('/');
				ReleaseId = releaseId;
			}
		}

		// Removes the remote catalogs Addressables cached from earlier releases (catalog_*.bin, .json, .hash), unless a
		// release is named. Call it after SetRelease and before anything touches Addressables. Never throws: a file that
		// can't be removed leaves the stale catalog in use, which is no worse than not clearing.
		public static void ClearCachedCatalogs(string cacheDirectory)
		{
			if (ReleaseId != null || string.IsNullOrEmpty(cacheDirectory) || !Directory.Exists(cacheDirectory))
			{
				return;
			}
			try
			{
				foreach (string file in Directory.GetFiles(cacheDirectory, "catalog*"))
				{
					string extension = Path.GetExtension(file);
					if (extension != ".bin" && extension != ".json" && extension != ".hash")
					{
						continue;
					}
					try
					{
						File.Delete(file);
					}
					catch (Exception e)
					{
						Debug.LogWarning("Could not remove the cached catalog " + Path.GetFileName(file) + ": " + e.Message);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("Could not clear the cached catalogs: " + e.Message);
			}
		}

		public static void ClearCachedCatalogs()
		{
			ClearCachedCatalogs(Path.Combine(Application.persistentDataPath, "com.unity.addressables"));
		}

		public static string Transform(string internalId)
		{
			if (ReleaseId == null || internalId == null || !internalId.StartsWith(PlaceholderHost + "/", StringComparison.Ordinal))
			{
				return internalId;
			}
			return cdnRoot + "/" + ReleaseId + internalId.Substring(PlaceholderHost.Length);
		}

		// The CDN root is the manifest's cdnRoot, which is the directory URL minus
		// "/<hash>/ClientManifestDirectory.json". Null if the URL doesn't have that shape.
		public static string CdnRootFrom(string fullAssetUrl)
		{
			if (!Uri.TryCreate(fullAssetUrl, UriKind.Absolute, out var uri))
			{
				return null;
			}
			string path = uri.AbsolutePath.TrimEnd('/');
			int last = path.LastIndexOf('/');
			if (last <= 0)
			{
				return null;
			}
			int previous = path.LastIndexOf('/', last - 1);
			if (previous < 0)
			{
				return null;
			}
			return uri.GetLeftPart(UriPartial.Authority) + path.Substring(0, previous);
		}

		// The manifest with the release's new entries added. The base manifest's version and hash stay: the delta is
		// only a list of entries, so it must not overwrite them the way a full downloaded manifest does.
		public static ContentManifest MergeDelta(ContentManifest manifest, ContentManifest delta)
		{
			if (delta == null)
			{
				return manifest;
			}
			ContentManifest merged = ContentManifestUtility.Merge(manifest, delta);
			merged.BaseUri = manifest.BaseUri;
			merged.ContentVersion = manifest.ContentVersion;
			merged.ContentManifestHash = manifest.ContentManifestHash;
			return merged;
		}

		public static void Install()
		{
			Addressables.InternalIdTransformFunc = (location) => Transform(location.InternalId);
		}
	}
}
