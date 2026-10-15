"""Fixture module docstring: the // and # markers in this string are not comments."""

import re

ENDPOINT = "https://example.test/api#fragment"  # a real trailing comment
PATTERN = re.compile(r"#\d+")                   # a raw string keeps its marker
FORMATTED = f"{ENDPOINT} # label"               # an f-string keeps its marker


def load(path):  # trailing comment on a signature
    # a comment line
    with open(path, "r", encoding="utf-8") as handle:
        data = handle.read()
    return data.split("\n")


if __name__ == "__main__":
    print(load("config#1.txt"))
