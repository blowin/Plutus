<?php

/**
 * Handles a request.
 */
final class Handler
{
    # Shell-style comment, removed as well.
    public const ENDPOINT = 'https://example.test/api//v1';

    public function render(): string
    {
        $body = <<<HTML
        <p>// not a comment in a heredoc</p>
        <!-- not a markup comment either -->
        HTML;
        $note = "path#1"; // trailing comment
        return $body . $note;
    }
}
