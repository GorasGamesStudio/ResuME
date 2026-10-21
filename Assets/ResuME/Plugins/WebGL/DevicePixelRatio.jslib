// Browser device pixel ratio for the WebGL build: Unity sizes its canvas in physical pixels
// (CSS size times this ratio), and the responsive canvas scaler needs it to reason in CSS pixels.
mergeInto(LibraryManager.library, {
  ResumeDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  }
});
