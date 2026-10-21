using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Canvas scaler that keeps the interface readable on small screens. On large screens it scales
    /// exactly like <see cref="CanvasScaler.ScaleMode.ScaleWithScreenSize"/> (reference 1280 x 720,
    /// width and height weighted half and half); on small ones (a phone, the frame of the website's
    /// resume page) it never lets one reference pixel fall under <see cref="MIN_CSS_SCALE"/> CSS
    /// pixels, so the smallest texts stay legible. The anchored layouts then get a smaller logical
    /// screen and adapt to it (corners stay in their corners, the skill row shrinks to fit).
    /// </summary>
    public class ResponsiveCanvasScaler : CanvasScaler
    {
        #region Fields

        /// <summary>Reference resolution the whole interface is designed for.</summary>
        public static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1280f, 720f);

        /// <summary>
        /// Smallest number of CSS pixels per reference pixel: the smallest 12 px reference text stays at
        /// 12 CSS pixels at least, comfortable on a phone (raised from 0.85 with the compact HUD, D29).
        /// </summary>
        public const float MIN_CSS_SCALE = 1f;

        private Canvas _canvas;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Sets the scaler to the studio's settings when the component is added.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            _canvas = GetComponent<Canvas>();
            uiScaleMode = ScaleMode.ScaleWithScreenSize;
            referenceResolution = REFERENCE_RESOLUTION;
            screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
            matchWidthOrHeight = 0.5f;
        }

        #endregion

        #region Private Methods

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float ResumeDevicePixelRatio();
#endif

        /// <summary>
        /// Physical pixels per CSS pixel: the browser's device pixel ratio in a WebGL build, 1 elsewhere.
        /// </summary>
        private static float DevicePixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            float ratio = ResumeDevicePixelRatio();
            return ratio > 0f ? ratio : 1f;
#else
            return 1f;
#endif
        }

        /// <summary>
        /// Scales with the screen size like the base scaler, then raises the factor to the readable
        /// floor when the screen is small.
        /// </summary>
        protected override void HandleScaleWithScreenSize()
        {
            // Same weighted average as the base scaler, in logarithmic space (half width, half height).
            // The base class keeps its canvas private: use ours (the screen size before Awake).
            Vector2 screenSize = _canvas != null ? _canvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            float logWidth = Mathf.Log(screenSize.x / m_ReferenceResolution.x, 2f);
            float logHeight = Mathf.Log(screenSize.y / m_ReferenceResolution.y, 2f);
            float scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, m_MatchWidthOrHeight));

            // The floor is in CSS pixels, so it follows the device pixel ratio of the browser.
            float floor = MIN_CSS_SCALE * DevicePixelRatio();
            SetScaleFactor(Mathf.Max(scaleFactor, floor));
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

        #endregion
    }
}
