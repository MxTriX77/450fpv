using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Fpv450.Assets
{
    /// <summary>
    /// The import settings of the art folders (setup-asset-pipeline, design decision 8), applied by
    /// <see cref="ModelPostprocessor"/> on every import and checked again by the catalog importer. FBX files under
    /// <c>Art/Models/</c>, and the EditMode fixtures (the X-sign probe, which must import exactly as every asset
    /// does): metres stay metres, no Unity mesh LODs (the project uses its own LOD groups, knowledge.md 14.4), no
    /// Unity colliders, static. Textures under <c>Art/ThirdParty/</c>: typed from the Poly Haven, ambientCG or
    /// TextureCan suffix; the maps that <c>tools/assets/derive.py</c> writes under <c>Art/Materials/</c>: the packed
    /// masks as linear RGBA data, the ground and palette albedos as sRGB, the <c>_base</c> maps of alpha-clipped
    /// recipes as sRGB diffuse with their alpha, undilated; the impostor atlases the Blender build writes beside the FBX under
    /// <c>Art/Models/</c> (p1-v01-locust): the albedo (<c>_impostor</c>) sRGB diffuse with its alpha, the normals
    /// (<c>_impostor_normal</c>) linear data. The base map of an alpha-clipped recipe and an impostor atlas also keep
    /// their mip coverage at the cutoff (<see cref="ApplyCoverage"/>, applied by the catalog importer).
    /// </summary>
    public static class ArtImportSettings
    {
        public const string ModelsFolder = "Assets/_Project/Art/Models/";
        public const string FixturesFolder = "Assets/_Project/Tests/EditMode/Fixtures/";
        public const string ThirdPartyFolder = "Assets/_Project/Art/ThirdParty/";
        public const string MaterialsFolder = "Assets/_Project/Art/Materials/";

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

        /// <summary>
        /// Applies the texture settings of a map by its file name and reports whether any changed. The name,
        /// lower-cased, types the map: Poly Haven's <c>_nor_gl</c>, ambientCG's <c>_NormalGL</c>, TextureCan's
        /// <c>_normal</c> and a derived <c>_normal</c> are normal maps; <c>_rough</c>, <c>_ao</c>,
        /// <c>ambientocclusion</c> and ambientCG's <c>_Opacity</c> are single-channel; <c>_diff</c>, <c>_color</c>, a
        /// derived <c>_albedo</c> (the ground base and detail albedos, the palettes), a derived <c>_base</c> map (the
        /// alpha-clipped, groove and paint recipes') and an <c>_impostor</c> atlas are sRGB diffuse; a derived
        /// <c>_mask</c> and anything else is a default-typed linear map. Only the diffuse maps are sRGB, and only a
        /// diffuse whose source carries an alpha channel (an impostor) is imported as transparency, its alpha kept as
        /// given, never premultiplied; a derived <c>_base</c> map with alpha (a leaf atlas, the pods) is not, as
        /// <c>derive.py</c> gives every texel its alpha clips the kept texels' mean colour, which Unity's dilation
        /// would paint over with the colours at the leaves' edges (p1-v01-locust task 1.2l). An impostor's normal
        /// atlas (<c>_impostor_normal</c>) is no normal map but linear data without mips: its shader reads it raw at
        /// full resolution.
        /// </summary>
        public static bool ApplyTexture(TextureImporter importer, string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool derivedBase = assetPath.StartsWith(MaterialsFolder, StringComparison.Ordinal) && name.EndsWith("_base");
            bool impostorNormals = name.EndsWith("_impostor_normal");
            bool normal = !impostorNormals && (name.Contains("_nor_gl") || name.Contains("_normal"));
            bool single = name.Contains("_rough") || name.Contains("_ao") || name.Contains("ambientocclusion")
                || name.Contains("_opacity");
            bool diffuse = name.Contains("_diff") || name.Contains("_color") || name.Contains("_albedo")
                || name.EndsWith("_base") || name.EndsWith("_impostor");
            TextureImporterType type = normal ? TextureImporterType.NormalMap
                : single ? TextureImporterType.SingleChannel
                : TextureImporterType.Default;

            bool changed = false;
            changed |= Set(importer.textureType, type, v => importer.textureType = v);
            changed |= Set(importer.sRGBTexture, diffuse, v => importer.sRGBTexture = v);
            changed |= Set(importer.alphaIsTransparency, diffuse && !derivedBase && importer.DoesSourceTextureHaveAlpha(), v => importer.alphaIsTransparency = v);
            if (impostorNormals)
                changed |= Set(importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
            if (single)
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

        /// <summary>
        /// Applies mip coverage preservation at <paramref name="cutoff"/> to the base map of an alpha-clipped recipe
        /// and reports whether it changed. The cutoff is the recipe's, so the catalog importer applies it, not the
        /// postprocessor; the setting lives in the map's .meta.
        /// </summary>
        public static bool ApplyCoverage(TextureImporter importer, float cutoff)
        {
            bool changed = false;
            changed |= Set(importer.mipMapsPreserveCoverage, true, v => importer.mipMapsPreserveCoverage = v);
            changed |= Set(importer.alphaTestReferenceValue, cutoff, v => importer.alphaTestReferenceValue = v);
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
        public override uint GetVersion() => 7;

        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(ArtImportSettings.ModelsFolder, StringComparison.Ordinal)
                || assetPath.StartsWith(ArtImportSettings.FixturesFolder, StringComparison.Ordinal))
                ArtImportSettings.ApplyModel((ModelImporter)assetImporter);
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(ArtImportSettings.ThirdPartyFolder, StringComparison.Ordinal)
                || assetPath.StartsWith(ArtImportSettings.MaterialsFolder, StringComparison.Ordinal)
                || assetPath.StartsWith(ArtImportSettings.ModelsFolder, StringComparison.Ordinal))
                ArtImportSettings.ApplyTexture((TextureImporter)assetImporter, assetPath);
        }
    }
}
