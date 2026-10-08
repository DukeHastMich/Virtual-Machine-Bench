"""Small lossless-atom S-expression reader for the board experiment."""
import re


def parse(text):
    """Read expressions, preserving quoted atoms for safe reserialization."""
    text = text.replace('(string_quote ")', '(string_quote quote)')
    stack, roots = [], []
    for token in re.findall(r'"(?:\\.|[^"\\])*"|\(|\)|[^\s()]+', text):
        if token == "(":
            node = []
            (stack[-1] if stack else roots).append(node)
            stack.append(node)
        elif token == ")":
            stack.pop()
        else:
            stack[-1].append(token)
    if stack:
        raise ValueError("Unclosed expression")
    return roots


def children(node, key):
    return [n for n in node if isinstance(n, list) and n and n[0] == key]


def child(node, key, default=None):
    return next(iter(children(node, key)), default)


def atom(text):
    return text[1:-1] if text.startswith('"') else text


def dump(node):
    return "(" + " ".join(dump(n) if isinstance(n, list) else str(n) for n in node) + ")"
