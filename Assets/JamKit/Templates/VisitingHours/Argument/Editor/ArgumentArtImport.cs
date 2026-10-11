using System;
using UnityEditor;

namespace JamKit.Argument.EditorTools
{
    /// <summary>
    /// Import settings for the battle sprites (Argument/Resources/ArgumentArt/*.png), whatever .meta a file arrives
    /// with: a Default texture, no mipmaps, NO power-of-two rescale (a 1024 x 1536 sprite stays 2:3), alpha is
    /// transparency, clamped, up to 2048 px. The first sprites came with hand-written .meta files that held only a
    /// GUID; Unity imported them but they never loaded (Resources.Load returned null and the code-drawn placeholder
    /// showed). Their metas are complete now, and this keeps any new drop-in (a new expression, a treatment like
    /// lena_bw_angry) right on its first import.
    /// </summary>
    public sealed class ArgumentArtImport : AssetPostprocessor
    {
        public const string Marker = "/Resources/" + ArgumentArt.ArtFolder; // ".../Resources/ArgumentArt/"

        public static bool IsBattleSprite(string path) =>
            path != null && path.IndexOf(Marker, StringComparison.Ordinal) >= 0 && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (IsBattleSprite(assetPath) && assetImporter is TextureImporter ti) Apply(ti);
        }

        /// <summary>Puts the sprite settings on <paramref name="ti"/>; true when it changed something.</summary>
        public static bool Apply(TextureImporter ti)
        {
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
            if (ti.wrapMode != UnityEngine.TextureWrapMode.Clamp) { ti.wrapMode = UnityEngine.TextureWrapMode.Clamp; dirty = true; }
            if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
            return dirty;
        }
    }
}
