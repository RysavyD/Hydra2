// Loaded synchronously in <head> BEFORE the body renders to prevent
// flash-of-wrong-theme. Reads localStorage and applies data-theme attr.
// If localStorage has nothing, CSS @media (prefers-color-scheme) handles it.
(function () {
    try {
        var saved = localStorage.getItem("hydra2-theme");
        if (saved === "light" || saved === "dark") {
            document.documentElement.setAttribute("data-theme", saved);
        }
    } catch (e) {
        // localStorage may be blocked (private mode) - silently ignore.
    }
})();
