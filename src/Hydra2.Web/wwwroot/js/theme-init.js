// Loaded synchronously in <head> BEFORE the body renders to prevent
// flash-of-wrong-theme. Reads localStorage and applies data-bs-theme attr.
// Bootstrap 5.3 native dark mode: data-bs-theme="dark"|"light" on <html>.
// "auto" mode resolves OS preference and sets the attribute explicitly.
(function () {
    try {
        var saved = localStorage.getItem("hydra2-theme");
        var effective;
        if (saved === "light" || saved === "dark") {
            effective = saved;
        } else {
            // "auto" — respect OS preference
            effective = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        }
        document.documentElement.setAttribute("data-bs-theme", effective);
    } catch (e) {
        // localStorage or matchMedia may be blocked — silently ignore.
    }
})();
