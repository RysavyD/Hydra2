// ----- Theme toggle (Phase 10 — updated Phase 11 for BS5 data-bs-theme) -----
// Cycles auto -> light -> dark -> auto. "auto" follows OS via prefers-color-scheme.
// Uses Bootstrap 5.3 native data-bs-theme attribute on <html>.
(function () {
    var STORAGE_KEY = "hydra2-theme";

    function getOsPreference() {
        try {
            return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        } catch (e) {
            return "light";
        }
    }

    function currentMode() {
        var stored = null;
        try { stored = localStorage.getItem(STORAGE_KEY); } catch (e) { /* blocked */ }
        if (stored === "light" || stored === "dark") return stored;
        return "auto";
    }

    function applyMode(mode) {
        var effective;
        if (mode === "auto") {
            try { localStorage.removeItem(STORAGE_KEY); } catch (e) { /* blocked */ }
            effective = getOsPreference();
        } else {
            try { localStorage.setItem(STORAGE_KEY, mode); } catch (e) { /* blocked */ }
            effective = mode;
        }
        document.documentElement.setAttribute("data-bs-theme", effective);
    }

    function nextMode(mode) {
        if (mode === "auto") return "light";
        if (mode === "light") return "dark";
        return "auto";
    }

    function labelFor(mode) {
        if (mode === "auto") return "Téma: auto";
        if (mode === "light") return "Téma: světlé";
        return "Téma: tmavé";
    }

    function updateButton(button) {
        var mode = currentMode();
        button.textContent = labelFor(mode);
        button.setAttribute("aria-label", labelFor(mode) + " (kliknutím přepnete)");
    }

    document.addEventListener("DOMContentLoaded", function () {
        var button = document.getElementById("theme-toggle");
        if (!button) return;
        updateButton(button);
        button.addEventListener("click", function () {
            applyMode(nextMode(currentMode()));
            updateButton(button);
        });
    });
})();
