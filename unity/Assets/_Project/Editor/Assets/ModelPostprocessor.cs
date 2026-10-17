using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Fpv450.Assets
{
    /// <summary>
    /// The import settings of the art folders (setup-asset-pipeline, design decision 8), applied by
    /// <see cref="ModelPostprocessor"/> on every import and checked again by the catalog importer. FBX files under
    /// <c>Art/Models/</c>: metres stay metres, no Unity mesh LODs (the project uses its own LOD groups, knowledge.md
    /// 14.4), no Unity colliders, static. Textures under <c>Art/ThirdParty/</c>: typed from the Poly Haven suffix.
    /// </summary>
    public static class ArtImportSettings
    {
        public const string ModelsFolder = "Assets/_Project/Art/Models/";
        public const string ThirdPartyFolder = "Assets/_Project/Art/ThirdParty/";

        /// <summary>Applies the model settings and reports whether any of them changed.</summary>
        public static bool ApplyModel(ModelImporter importer)
        {
            bool changed = false;
            changed |= Set(importer.globalScale, 1f, v => importer.globalScale = v);
            changed |= Set(importer.useFileScale, true, v => importer.useFileScale = v); // the file's unit scale makes the exporter's centimetres metres
            changed |= Set(importer.generateMeshLods, false, v => importer.generateMeshLods = v);
            changed |= Set(importer.addCollider, false, v => importer.addCollider = v);
            // Materials are imported, embedded and never used: they are the only way the FBX's material slot names
            // reach the catalog importer, which assigns the project's materials by those names.
            changed |= Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, v => importer.materialImportMode = v);
            changed |= Set(importer.materialLocation, ModelImporterMaterialLocation.InPrefab, v => importer.materialLocation = v);
            changed |= Set(importer.isReadable, false, v => importer.isReadable = v);
            changed |= Set(importer.meshCompression, ModelImporterMeshCompression.Off, v => importer.meshCompression = v);
            changed |= Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            changed |= Set(importer.importAnimation, false, v => importer.importAnimation = v);
            changed |= Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            changed |= Set(importer.importCameras, false, v => importer.importCameras = v);
            changed |= Set(importer.importLights, false, v => importer.importLights = v);
            return changed;
        }

        /// <summary>Applies the texture settings for a third-party map, by its file name, and reports whether any changed.</summary>
        public static bool ApplyTexture(TextureImporter importer, string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool normal = name.Contains("_nor_gl");
            bool mask = name.Contains("_rough") || name.Contains("_ao");
            bool diffuse = name.Contains("_diff");
            TextureImporterType type = normal ? TextureImporterType.NormalMap
                : mask ? TextureImporterType.SingleChannel
                : TextureImporterType.Default;

            bool changed = false;
            changed |= Set(importer.textureType, type, v => importer.textureType = v);
            changed |= Set(importer.sRGBTexture, diffuse, v => importer.sRGBTexture = v);
            if (mask)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.singleChannelComponent != TextureImporterSingleChannelComponent.Red)
                {
                    settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                    importer.SetTextureSettings(settings);
                    changed = true;
                }
            }
            return changed;
        }

        static bool Set<T>(T current, T wanted, Action<T> assign)
        {
            if (EqualityComparer<T>.Default.Equals(current, wanted))
                return false;
            assign(wanted);
            return true;
        }
    }

    /// <summary>Applies <see cref="ArtImportSettings"/> whenever Unity imports a file of the art folders.</summary>
    public sealed class ModelPostprocessor : AssetPostprocessor
    {
        // Bump when ArtImportSettings changes, so Unity reimports the files it covers.
        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(ArtImportSettings.ModelsFolder, StringComparison.Ordinal))
                ArtImportSettings.ApplyModel((ModelImporter)assetImporter);
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(ArtImportSettings.ThirdPartyFolder, StringComparison.Ordinal))
                ArtImportSettings.ApplyTexture((TextureImporter)assetImporter, assetPath);
        }
    }
}
