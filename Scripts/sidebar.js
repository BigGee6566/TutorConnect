/* Sidebar drawer for screens below 1024px.

   Above that breakpoint the sidebar is always visible and this script does nothing.
   Without JavaScript the sidebar still renders and every link still works - it simply
   sits at the top of the page rather than sliding in. */
(function () {
    'use strict';

    var OPEN_CLASS = 'sidebar-open';

    function toggle(open) {
        document.body.classList.toggle(OPEN_CLASS, open);

        var trigger = document.querySelector('[data-sidebar-open]');
        if (trigger) { trigger.setAttribute('aria-expanded', open ? 'true' : 'false'); }

        if (open) {
            // Move focus into the drawer so a keyboard user is not left behind it.
            var first = document.querySelector('.sidebar__close');
            if (first) { first.focus(); }
        } else if (trigger) {
            trigger.focus();
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var opener = document.querySelector('[data-sidebar-open]');
        if (opener) {
            opener.addEventListener('click', function () { toggle(true); });
        }

        var closers = document.querySelectorAll('[data-sidebar-close]');
        for (var i = 0; i < closers.length; i++) {
            closers[i].addEventListener('click', function () { toggle(false); });
        }

        // Escape closes the drawer, as it would any overlay.
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && document.body.classList.contains(OPEN_CLASS)) {
                toggle(false);
            }
        });

        // Leaving the drawer breakpoint must not strand the page in the open state.
        if (window.matchMedia) {
            var wide = window.matchMedia('(min-width: 1024px)');
            var onChange = function (m) { if (m.matches) { document.body.classList.remove(OPEN_CLASS); } };
            if (wide.addEventListener) { wide.addEventListener('change', onChange); }
            else if (wide.addListener) { wide.addListener(onChange); }
        }
    });
})();
