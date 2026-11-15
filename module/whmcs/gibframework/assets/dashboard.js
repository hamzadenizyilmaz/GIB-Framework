(() => {
<<<<<<< Updated upstream
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
=======
    const root = document.getElementById("gibframework-app");
    if (!root) return;

    // Tam sayfa: modül ekranını <body>'nin doğrudan altına taşı (WHMCS şablon kapsayıcılarının
    // transform / overflow ayarları fixed konumlandırmayı bozmasın) ve arka sayfanın kaydırmasını kapat.
    if (root.parentElement !== document.body) document.body.appendChild(root);
    document.documentElement.classList.add("gf-open");

    // Gönderim sırasında düğmeyi kilitle (çift tıklamayı önler).
    for (const form of root.querySelectorAll("form[data-gib-busy]")) {
        const button = form.querySelector("button[type='submit']");
        if (!button) continue;
        const label = button.querySelector("span");
        const original = label ? label.textContent : "";
        form.addEventListener("submit", () => {
            button.disabled = true;
            if (label && button.dataset.busyLabel) label.textContent = button.dataset.busyLabel;
        });
        window.addEventListener("pageshow", () => {
            button.disabled = false;
            if (label) label.textContent = original;
        });
    }

    // Bildirim adresini panoya kopyala.
    for (const button of root.querySelectorAll("[data-gib-copy]")) {
        button.addEventListener("click", async () => {
            const input = root.querySelector(button.dataset.gibCopy);
            const label = button.querySelector("span");
            if (!input || !label) return;
            try {
                await navigator.clipboard.writeText(input.value);
            } catch {
                input.select();
                document.execCommand("copy");
            }
            label.textContent = "Kopyalandı";
            setTimeout(() => { label.textContent = "Kopyala"; }, 1800);
>>>>>>> Stashed changes
        });
    }
})();
