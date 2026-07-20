using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OFS.ModAuthoring.Editor
{
    /// <summary>
    /// Generates a game-independent fixture used to verify the public bundle
    /// authoring pipeline against the exact Unity version embedded by OFS.
    /// </summary>
    public static class OFSBundleFixtureGenerator
    {
        private const string FixtureRoot = "Assets/ModContent/Fixture";
        private const string AssetBundleName = "ofs-sdk-fixture-assets";
        private const string SceneBundleName = "ofs-sdk-fixture-scene";

        public static void GenerateAndBuildFromCommandLine()
        {
            Generate();
            OFSBundleBuilder.BuildFromCommandLine();
        }

        [MenuItem("OFS SDK/Generate Verification Fixture")]
        public static void Generate()
        {
            if (AssetDatabase.IsValidFolder(FixtureRoot))
            {
                if (!AssetDatabase.DeleteAsset(FixtureRoot))
                    throw new InvalidOperationException($"Could not replace '{FixtureRoot}'.");
            }
            EnsureFolder("Assets/ModContent", "Fixture");

            var texturePath = $"{FixtureRoot}/OFSFixtureSprite.png";
            WriteFixtureTexture(texturePath);
            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter
                ?? throw new InvalidOperationException("Unity did not create a TextureImporter for the fixture PNG.");
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.spritePixelsPerUnit = 32f;
            textureImporter.mipmapEnabled = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.SaveAndReimport();

            var materialPath = $"{FixtureRoot}/OFSFixtureMaterial.mat";
            var shader = Shader.Find("Standard")
                ?? throw new InvalidOperationException("Unity's built-in Standard shader was not found.");
            var material = new Material(shader)
            {
                name = "OFS Fixture Material",
                color = new Color(0.12f, 0.72f, 0.95f, 1f),
            };
            AssetDatabase.CreateAsset(material, materialPath);

            var prefabPath = $"{FixtureRoot}/OFSFixturePrefab.prefab";
            var prefabRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                prefabRoot.name = "OFS Fixture Prefab";
                prefabRoot.transform.localScale = new Vector3(1.25f, 0.75f, 1.5f);
                var renderer = prefabRoot.GetComponent<MeshRenderer>()
                    ?? throw new InvalidOperationException("Primitive fixture has no MeshRenderer.");
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }

            var scenePath = $"{FixtureRoot}/OFSFixtureScene.unity";
            var fixtureScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            try
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = "OFS Fixture Scene Marker";
                marker.transform.position = new Vector3(0f, 1.5f, 0f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = material;
                SceneManager.MoveGameObjectToScene(marker, fixtureScene);

                var lightObject = new GameObject("OFS Fixture Scene Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.8f;
                lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                SceneManager.MoveGameObjectToScene(lightObject, fixtureScene);

                if (!EditorSceneManager.SaveScene(fixtureScene, scenePath))
                    throw new InvalidOperationException($"Could not save fixture scene '{scenePath}'.");
            }
            finally
            {
                if (fixtureScene.IsValid() && fixtureScene.isLoaded)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            AssignBundle(texturePath, AssetBundleName);
            AssignBundle(materialPath, AssetBundleName);
            AssignBundle(prefabPath, AssetBundleName);
            AssignBundle(scenePath, SceneBundleName);
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"OFS generated verification fixture at '{FixtureRoot}' with bundles " +
                $"'{AssetBundleName}' and '{SceneBundleName}'.");
        }

        private static void WriteFixtureTexture(string assetPath)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false);
            try
            {
                for (var y = 0; y < texture.height; ++y)
                for (var x = 0; x < texture.width; ++x)
                {
                    var checker = ((x / 4) + (y / 4)) % 2 == 0;
                    texture.SetPixel(
                        x,
                        y,
                        checker
                            ? new Color(0.05f, 0.8f, 1f, 1f)
                            : new Color(1f, 0.72f, 0.08f, 1f));
                }
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                var absolute = Path.GetFullPath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)
                    ?? throw new InvalidOperationException("Fixture texture has no parent directory."));
                File.WriteAllBytes(absolute, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path) &&
                string.IsNullOrWhiteSpace(AssetDatabase.CreateFolder(parent, name)))
                throw new InvalidOperationException($"Could not create Unity asset folder '{path}'.");
        }

        private static void AssignBundle(string assetPath, string bundleName)
        {
            var importer = AssetImporter.GetAtPath(assetPath)
                ?? throw new InvalidOperationException($"No importer exists for '{assetPath}'.");
            importer.assetBundleName = bundleName;
            importer.assetBundleVariant = string.Empty;
            importer.SaveAndReimport();
        }
    }
}
