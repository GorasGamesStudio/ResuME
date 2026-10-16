using UnityEngine;

namespace GGS.Resume.Data
{
    /// <summary>
    /// The three layers the camera plays with: the outside world, the interior of the building the
    /// character is in, and the character itself. Inside a building the camera renders only the
    /// last two, so the outside disappears without drawing anything over it.
    /// </summary>
    public static class WorldLayers
    {
        #region Fields

        public const string WORLD = "World";
        public const string INTERIOR = "Interior";
        public const string PLAYER = "Player";

        /// <summary>Index of the World layer, resolved once.</summary>
        public static int World => Resolve(WORLD);
        /// <summary>Index of the Interior layer, resolved once.</summary>
        public static int Interior => Resolve(INTERIOR);
        /// <summary>Index of the Player layer, resolved once.</summary>
        public static int Player => Resolve(PLAYER);

        #endregion

        #region Public Methods

        /// <summary>
        /// Puts a transform and all its descendants on a layer.
        /// </summary>
        /// <param name="root">Root of the hierarchy.</param>
        /// <param name="layer">Layer index.</param>
        public static void SetRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetRecursively(root.GetChild(i), layer);
        }

        /// <summary>
        /// Culling mask showing everything the game renders.
        /// </summary>
        /// <returns>Mask of every layer.</returns>
        public static int EverythingMask() => ~0;

        /// <summary>
        /// Culling mask for the inside of a building: the interior, the character, and the default layer
        /// (runtime effects such as the level burst or the ending blocks).
        /// </summary>
        /// <returns>Mask of the interior, player and default layers.</returns>
        public static int InteriorMask() => (1 << Interior) | (1 << Player) | (1 << 0);

        #endregion

        #region Private Methods

        /// <summary>
        /// Resolves a layer name, falling back to the default layer with a warning when the project lacks it.
        /// </summary>
        private static int Resolve(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                Debug.LogWarning($"[ResuME] Layer '{name}' missing: rebuild the greybox (ResuME/Greybox/Rebuild everything) to create it.");
                return 0;
            }
            return layer;
        }

        #endregion
    }
}
