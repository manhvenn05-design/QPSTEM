document.addEventListener("DOMContentLoaded", () => {
    // ── Consultation form ──────────────────────────────
    const consultationForm = document.querySelector(".consultation-form");
    if (consultationForm) {
        consultationForm.addEventListener("submit", (e) => e.preventDefault());
    }

    // ── Contact form ───────────────────────────────────
    const contactForm = document.querySelector(".contact-form");
    if (contactForm) {
        contactForm.addEventListener("submit", (e) => e.preventDefault());
    }

    // ── Password toggle ────────────────────────────────
    document.querySelectorAll("[data-toggle-password]").forEach((toggle) => {
        toggle.addEventListener("click", () => {
            const container = toggle.closest(".input-with-icon");
            const input = container?.querySelector("[data-password-input]");
            const icon = toggle.querySelector(".material-symbols-outlined");
            if (!(input instanceof HTMLInputElement) || !icon) return;
            const isPassword = input.type === "password";
            input.type = isPassword ? "text" : "password";
            icon.textContent = isPassword ? "visibility" : "visibility_off";
        });
    });

    // ── Mobile hamburger menu ──────────────────────────
    const hamburgerBtn   = document.getElementById("hamburger-btn");
    const mobileNav      = document.getElementById("mobile-nav");
    const mobileOverlay  = document.getElementById("mobile-nav-overlay");
    const mobileClose    = document.getElementById("mobile-nav-close");

    if (!hamburgerBtn || !mobileNav || !mobileOverlay) return;

    function openMenu() {
        mobileNav.classList.add("is-open");
        mobileOverlay.classList.add("is-visible");
        hamburgerBtn.setAttribute("aria-expanded", "true");
        document.body.style.overflow = "hidden";
    }

    function closeMenu() {
        mobileNav.classList.remove("is-open");
        mobileOverlay.classList.remove("is-visible");
        hamburgerBtn.setAttribute("aria-expanded", "false");
        document.body.style.overflow = "";
    }

    hamburgerBtn.addEventListener("click", openMenu);
    mobileClose?.addEventListener("click", closeMenu);
    mobileOverlay.addEventListener("click", closeMenu);

    // Close on Escape key
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") closeMenu();
    });

    // Close when a nav link is clicked (SPA-friendly)
    mobileNav.querySelectorAll("a").forEach((link) => {
        link.addEventListener("click", closeMenu);
    });
});
