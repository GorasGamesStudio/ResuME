// Browser clipboard bridge for the WebGL build: Unity's GUIUtility.systemCopyBuffer does nothing there.
mergeInto(LibraryManager.library, {
  ResumeCopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function () {});
    }
  }
});
