function ShowWaitDialog() {
    bootbox.dialog({
        message: "Zpracovávám ....",
        closeButton: false,
    });
};

function HideWaitDialog() {
    bootbox.hideAll();
};