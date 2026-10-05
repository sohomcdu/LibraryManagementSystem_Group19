// Kiosk helpers (spec F1): idle timeout, text-size toggle, on-screen keypad, keep scanner input focused.
window.Kiosk = (function () {
    function init(opts) {
        var timer;
        function reset() {
            clearTimeout(timer);
            timer = setTimeout(function () { location.href = opts.idleUrl; }, opts.idleSeconds * 1000);
        }
        ["click", "touchstart", "keydown", "mousemove"].forEach(function (e) { document.addEventListener(e, reset, { passive: true }); });
        reset();

        var body = document.getElementById("kioskBody");
        if (localStorage.getItem("kiosk.large") === "1") body.classList.add("large");
        var t = document.getElementById("textToggle");
        if (t) t.addEventListener("click", function () {
            body.classList.toggle("large");
            localStorage.setItem("kiosk.large", body.classList.contains("large") ? "1" : "0");
        });

        var scan = document.querySelector("[data-scan]");
        if (scan) {
            scan.focus();
            document.addEventListener("click", function (e) { if (e.target.tagName !== "BUTTON" && e.target.tagName !== "A") scan.focus(); });
        }
        document.querySelectorAll("[data-key]").forEach(function (b) {
            b.addEventListener("click", function () {
                var f = document.querySelector("[data-scan]");
                f.value += b.getAttribute("data-key");
                f.focus();
            });
        });
        var clr = document.querySelector("[data-clear]");
        if (clr) clr.addEventListener("click", function () { var f = document.querySelector("[data-scan]"); f.value = ""; f.focus(); });

        var cd = document.querySelector("[data-countdown]");
        if (cd) {
            var left = parseInt(cd.getAttribute("data-countdown"), 10), url = cd.getAttribute("data-url");
            var iv = setInterval(function () {
                left--; cd.textContent = left;
                if (left <= 0) { clearInterval(iv); location.href = url; }
            }, 1000);
        }
    }
    return { init: init };
})();
