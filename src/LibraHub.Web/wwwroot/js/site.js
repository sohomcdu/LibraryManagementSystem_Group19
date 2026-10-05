// Auto-submit helpers used by the staff pages.
document.addEventListener("change", function (e) {
    if (e.target.matches("[data-autosubmit]")) e.target.form.submit();
});
