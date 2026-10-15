/**
 * Widget bootstrap.
 * A JSDoc block: removed by default, kept with --preserve-doc-comments.
 */
(function ($) {
    'use strict';

    var ENDPOINT = 'https://example.test/api//v1';        // the string keeps its double slash
    var PATTERN = /^https?:\/\//;                          // a regex literal, not a comment
    var CHARACTER_CLASS = /[/*]/;                          // a comment opener inside a character class
    var HASH = "#not-a-comment";

    /* Block comment
       spanning lines. */
    function render(items) {
        var markup = `<ul data-count="${items.length /* hole comment */}">`;
        markup += '<li>https://example.test/#frag</li>';   // trailing comment
        return markup;
    }

    // A division, not a regex: half of the value.
    var half = render([]).length / 2;
    return { render: render, pattern: PATTERN, characterClass: CHARACTER_CLASS, hash: HASH, half: half };
})(window.jQuery);
