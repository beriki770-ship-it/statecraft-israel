// Opens a URL in a new tab inside the player's own gesture (#1168, ExternalLink.cs). Unity handles a press on the next
// frame, outside the browser's event, where window.open is popup-blocked; so a press arms a one-shot listener for its
// release (pointerup on the canvas, or keyup for the keyboard's Enter / Space), and the release opens the tab. Whichever
// comes first opens it and removes both; a second press before the release only replaces the URL. A release that never
// reaches the listener (a quick tap whose pointerup beat the next frame, a release off the canvas, a gamepad's Submit)
// would leave it armed for an unrelated later click, so it disarms itself after 1.5 s and opens nothing then.
mergeInto(LibraryManager.library, {
  StatecraftOpenOnRelease: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (typeof window === "undefined") return;
    var armed = window.statecraftOpenOnRelease;
    if (armed) { armed.url = url; return; }
    var canvas = document.querySelector("#unity-canvas") || document;
    armed = window.statecraftOpenOnRelease = { url: url };
    var timer;
    var disarm = function () {
      clearTimeout(timer);
      canvas.removeEventListener("pointerup", open);
      document.removeEventListener("keyup", open);
      window.statecraftOpenOnRelease = null;
    };
    var open = function () { disarm(); window.open(armed.url, "_blank", "noopener"); };
    canvas.addEventListener("pointerup", open);
    document.addEventListener("keyup", open);
    timer = setTimeout(disarm, 1500);
  },
});
