using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace OFS.ModAuthoring.Editor
{
    public static class OFSBundleBuilder
    {
        private const string RequiredUnityVersion = "6000.3.13f1";
        private const string OutputArgument = "-ofsOutput";

        [MenuItem("OFS SDK/Validate AssetBundles")]
        public static void ValidateFromMenu()
        {
            ValidateBundleAssignments();
            Debug.Log("OFS AssetBundle validation passed.");
        }

        [MenuItem("OFS SDK/Build Windows x64 AssetBundles")]
        public static void BuildFromMenu()
        {
            Build(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "Windows"));
        }

        public static void BuildFromCommandLine()
        {
            var output = ReadArgument(OutputArgument);
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new ArgumentException($"Missing required command-line argument {OutputArgument}.");
            }

            Build(Path.GetFullPath(output));
        }

        private static void Build(string outputDirectory)
        {
            RequireEditorVersion();
            ValidateBundleAssignments();
            Directory.CreateDirectory(outputDirectory);

            var manifest = BuildPipeline.BuildAssetBundles(
                outputDirectory,
                BuildAssetBundleOptions.ChunkBasedCompression |
                BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);
            if (manifest == null)
            {
                throw new InvalidOperationException("Unity BuildPipeline returned no AssetBundleManifest.");
            }

            var bundles = manifest.GetAllAssetBundles()
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => DescribeBundle(outputDirectory, manifest, name))
                .ToArray();
            var index = new BundleIndex
            {
                unityVersion = Application.unityVersion,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                bundles = bundles,
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "ofs-bundles.json"),
                JsonUtility.ToJson(index, prettyPrint: true));
            Debug.Log($"OFS built {bundles.Length} AssetBundle(s) at '{outputDirectory}'.");
        }

        private static void ValidateBundleAssignments()
        {
            RequireEditorVersion();
            var names = AssetDatabase.GetAllAssetBundleNames();
            if (names.Length == 0)
            {
                throw new InvalidOperationException(
                    "No AssetBundle labels exist. Select an asset and assign its AssetBundle name in the Inspector.");
            }

            var errors = new List<string>();
            foreach (var bundleName in names.OrderBy(value => value, StringComparer.Ordinal))
            {
                var roots = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                if (roots.Length == 0)
                {
                    errors.Add($"Bundle '{bundleName}' has no root assets.");
                    continue;
                }

                foreach (var dependency in AssetDatabase.GetDependencies(roots, recursive: true))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(dependency) == typeof(MonoScript))
                    {
                        errors.Add(
                            $"Bundle '{bundleName}' contains script '{dependency}'. " +
                            "Code belongs in the mod DLL; bundles must contain data and Unity-native components only.");
                    }
                }

                foreach (var prefabPath in roots.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(value => value == null))
                    {
                        errors.Add($"Prefab '{prefabPath}' contains a missing MonoBehaviour script.");
                    }
                }
            }

            if (errors.Count != 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }
        }

        private static BundleRecord DescribeBundle(
            string outputDirectory,
            AssetBundleManifest manifest,
            string name)
        {
            var path = Path.Combine(outputDirectory, name);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Unity reported bundle '{name}' but did not write it.", path);
            }

            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return new BundleRecord
            {
                name = name,
                bytes = stream.Length,
                sha256 = ToLowerHex(sha256.ComputeHash(stream)),
                unityHash = manifest.GetAssetBundleHash(name).ToString(),
                dependencies = manifest.GetAllDependencies(name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            };
        }

        private static void RequireEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"OFS bundles require Unity {RequiredUnityVersion}; running {Application.unityVersion}.");
            }
        }

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; ++index)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }
            return null;
        }

        private static string ToLowerHex(byte[] value) =>
            string.Concat(value.Select(part => part.ToString("x2")));

        [Serializable]
        private sealed class BundleIndex
        {
            public int schemaVersion = 1;
            public string unityVersion;
            public string buildTarget;
            public BundleRecord[] bundles;
        }

        [Serializable]
        private sealed class BundleRecord
        {
            public string name;
            public long bytes;
            public string sha256;
            public string unityHash;
            public string[] dependencies;
        }
    }
}
