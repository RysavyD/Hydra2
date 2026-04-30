function ShowWaitDialog() {
    bootbox.dialog({
        message: "Zpracovávám ....",
        closeButton: false,
    });
}

function HideWaitDialog() {
    bootbox.hideAll();
}

// ----- Theme toggle (Phase 10) ----------------------------------------------
// Cycles auto -> light -> dark -> auto. "auto" follows OS via @media.
(function () {
    var STORAGE_KEY = "hydra2-theme";

    function currentMode() {
        var stored = null;
        try { stored = localStorage.getItem(STORAGE_KEY); } catch (e) { /* blocked */ }
        if (stored === "light" || stored === "dark") return stored;
        return "auto";
    }

    function applyMode(mode) {
        var root = document.documentElement;
        if (mode === "auto") {
            root.removeAttribute("data-theme");
            try { localStorage.removeItem(STORAGE_KEY); } catch (e) { /* blocked */ }
        } else {
            root.setAttribute("data-theme", mode);
            try { localStorage.setItem(STORAGE_KEY, mode); } catch (e) { /* blocked */ }
        }
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
