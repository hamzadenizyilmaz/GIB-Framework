(() => {
    const forms = document.querySelectorAll(".gibframework-dashboard form[data-gib-probe]");
    for (const form of forms) {
        const button = form.querySelector("button[type='submit']");
        if (!button) continue;
        const label = button.textContent;
        form.addEventListener("submit", () => {
            button.disabled = true;
            button.textContent = button.dataset.busyLabel;
        });
        window.addEventListener("pageshow", () => {
            button.disabled = false;
            button.textContent = label;
        });
    }
})();
