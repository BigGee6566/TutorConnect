/* Show/hide password control.
   Progressive enhancement: without JavaScript the field is still a normal password
   input and the form still submits. The button is type="button" in the markup, so it
   can never submit the form even if this script fails to load. */
(function () {
    'use strict';

    function findInput(button) {
        // The markup carries the server control's ID fragment; ASP.NET may prefix it,
        // so match on a suffix rather than an exact id.
        var wanted = button.getAttribute('data-password-toggle');
        if (!wanted) { return null; }

        var candidates = document.querySelectorAll('input[type="password"], input[type="text"]');
        for (var i = 0; i < candidates.length; i++) {
            var id = candidates[i].id || '';
            if (id === wanted || id.endsWith('_' + wanted) || id.endsWith(wanted)) {
                return candidates[i];
            }
        }
        return null;
    }

    function toggle(button) {
        var input = findInput(button);
        if (!input) { return; }

        var nowVisible = input.type === 'password';
        input.type = nowVisible ? 'text' : 'password';

        button.setAttribute('aria-pressed', nowVisible ? 'true' : 'false');
        button.setAttribute('aria-label', nowVisible ? 'Hide password' : 'Show password');

        // Keep the caret where the user left it rather than jumping to the start.
        if (typeof input.selectionStart === 'number') {
            var position = input.value.length;
            input.focus();
            input.setSelectionRange(position, position);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var buttons = document.querySelectorAll('[data-password-toggle]');
        for (var i = 0; i < buttons.length; i++) {
            (function (button) {
                button.addEventListener('click', function () { toggle(button); });
            })(buttons[i]);
        }
    });
})();
