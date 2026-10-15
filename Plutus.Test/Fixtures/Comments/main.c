#include <stdio.h>

/* A fixture that the C compiler checks after comment stripping. */
static const char *URL = "https://example.test/api//v1";
static const char *MARKERS = "/* not a comment */";

// A line comment.
int main(void)
{
    char separator = '/'; // a character literal
    printf("%s %c %s\n", URL, separator, MARKERS);
    return 0;
}
