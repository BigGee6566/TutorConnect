/* Registration niceties: a live password-strength meter and a match check.

   Progressive enhancement only. Every rule shown here is enforced again on the server
   by UserValidator, so disabling JavaScript weakens nothing. The meter is hidden in the
   markup and revealed here, so it never appears as a dead control when this file fails
   to load. */
(function () {
    'use strict';

    var MIN_LENGTH = 8;

    function score(value) {
        if (!value) { return 0; }

        var points = 0;
        if (value.length >= MIN_LENGTH) { points++; }
        if (value.length >= 12) { points++; }
        if (/[A-Za-z]/.test(value) && /\d/.test(value)) { points++; }
        if (/[^A-Za-z0-9]/.test(value)) { points++; }
        return points;
    }

    var LEVELS = [
        { label: '', width: '0%', colour: 'var(--red-600)' },
        { label: 'Weak', width: '25%', colour: 'var(--red-600)' },
        { label: 'Fair', width: '50%', colour: 'var(--orange-500)' },
        { label: 'Good', width: '75%', colour: 'var(--blue-600)' },
        { label: 'Strong', width: '100%', colour: 'var(--green-600)' }
    ];

    function bySuffix(attribute) {
        return document.querySelector('[' + attribute + ']');
    }

    document.addEventListener('DOMContentLoaded', function () {
        var password = bySuffix('data-strength-source');
        var confirm = bySuffix('data-strength-confirm');
        var meter = bySuffix('data-strength-meter');
        var fill = bySuffix('data-strength-fill');
        var label = bySuffix('data-strength-label');
        var matchLabel = bySuffix('data-match-label');

        if (!password || !meter || !fill || !label) { return; }

        meter.hidden = false;

        password.addEventListener('input', function () {
            var level = LEVELS[score(password.value)];
            fill.style.width = level.width;
            fill.style.backgroundColor = level.colour;
            label.textContent = level.label ? 'Password strength: ' + level.label : '';
            checkMatch();
        });

        function checkMatch() {
            if (!confirm || !matchLabel) { return; }
            if (!confirm.value) { matchLabel.textContent = ''; return; }

            var same = confirm.value === password.value;
            matchLabel.textContent = same ? 'Passwords match.' : 'Passwords do not match yet.';
            matchLabel.style.color = same ? 'var(--green-700)' : 'var(--red-700)';
        }

        if (confirm) { confirm.addEventListener('input', checkMatch); }
    });
})();
