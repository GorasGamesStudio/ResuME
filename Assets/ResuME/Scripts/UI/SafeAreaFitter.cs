using UnityEngine;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Keeps a full-screen container inside <see cref="Screen.safeArea"/> (notches, rounded corners,
    /// gesture bars) by moving its anchors, so everything that must stay visible lives under it.
    /// Re-applied whenever the resolution, orientation or safe area changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        #region Fields

        private RectTransform _rect;
        private Rect _applied;
        private Vector2Int _appliedScreen;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Caches the rect transform and applies the safe area once.
        /// </summary>
        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        /// <summary>
        /// Re-applies the safe area when the screen or the safe area changed.
        /// </summary>
        private void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
                Apply();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Converts the safe area from pixels to anchors of the container.
        /// </summary>
        private void Apply()
        {
            Rect safe = Screen.safeArea;
            _applied = safe;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Anchors are fractions of the screen: divide the safe rectangle by the screen size.
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
