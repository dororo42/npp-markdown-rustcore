#!/usr/bin/env python3
"""Generate bench markdown samples (1KB / 100KB / 1MB / 10MB).

Deterministic content: headings, paragraphs, code fences (rust/js/python),
tables, task lists, inline code, links — exercising the full render pipeline.
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
random.seed(42)

LANGS = ["rust", "javascript", "python", "c", "html"]
CODE = {
    "rust": 'fn main() {\n    let msg = "hello";\n    println!("{}, world!", msg);\n}',
    "javascript": 'function main() {\n  const msg = "hello";\n  console.log(`${msg}, world!`);\n}',
    "python": 'def main():\n    msg = "hello"\n    print(f"{msg}, world!")',
    "c": '#include <stdio.h>\nint main(void) {\n    printf("hello, world!\\n");\n    return 0;\n}',
    "html": '<div class="doc"><p>hello <strong>world</strong></p></div>',
}


def block(i: int) -> str:
    lang = random.choice(LANGS)
    parts = []
    if i % 17 == 0:
        parts.append(f"> [!NOTE]\n> Bench callout number {i}: keep an eye on render time.\n")
    if i % 11 == 0:
        parts.append("| col a | col b | col c |\n|:------|:-----:|------:|\n| 1 | two | 3.0 |\n| x | y | z |\n")
    parts.append(f"## Section {i} — mixed content\n\n")
    parts.append(
        f"Paragraph {i} with **bold**, *italic*, ~~strike~~, `inline code`, "
        f"a [link](https://example.com/{i}) and an image ![img](assets/pic{i % 7}.png).\n\n"
    )
    if i % 5 == 0:
        parts.append("- [x] task one\n- [ ] task two\n- [x] task three\n\n")
    parts.append(f"```{lang}\n{CODE[lang]}\n```\n\n")
    if i % 23 == 0:
        parts.append("| a | b |\n|---|---|\n")
        for r in range(20):
            parts.append(f"| cell {r} | value {r * i} |\n")
        parts.append("\n")
    if i % 29 == 0:
        parts.append("$$\\int_0^\\infty e^{-x^2} dx = \\frac{\\sqrt{\\pi}}{2}$$\n\n")
    return "".join(parts)


def gen(name: str, target_bytes: int) -> None:
    buf = ["# Bench document: %s\n\nGenerated for the rustrender performance suite.\n\n" % name]
    total = len(buf[0].encode("utf-8"))
    i = 0
    while total < target_bytes:
        b = block(i)
        buf.append(b)
        total += len(b.encode("utf-8"))
        i += 1
    out = os.path.join(HERE, name)
    with open(out, "w", encoding="utf-8") as f:
        f.write("".join(buf))
    size = os.path.getsize(out)
    print(f"{name}: {size:,} bytes")


if __name__ == "__main__":
    only = sys.argv[1] if len(sys.argv) > 1 else None
    specs = {
        "1KB.md": 1024,
        "100KB.md": 100 * 1024,
        "1MB.md": 1024 * 1024,
        "10MB.md": 10 * 1024 * 1024,
    }
    for n, sz in specs.items():
        if only is None or only == n:
            gen(n, sz)
