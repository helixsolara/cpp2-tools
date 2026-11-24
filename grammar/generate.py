"""Writes cpp2.tmLanguage.json, the Cpp2 TextMate grammar both editors use.

The grammar is kept here rather than as hand-written JSON so its regexes can
share pieces and be read without JSON escaping. Edit this file, then run:

    python grammar/generate.py

It also checks that every regex compiles (Python's re is close enough to
Oniguruma, the engine both editors use, for the constructs used here).
"""

import json
import re
import sys
from pathlib import Path

IDENT = r"[A-Za-z_][A-Za-z0-9_]*"

# Words that can't be the name in "name: ..." or "name(...)"
NOT_A_NAME = (
    r"(?!(?:if|else|for|do|while|next|break|continue|return|inspect|is|as|in|inout|out|copy|move"
    r"|forward|in_ref|forward_ref|type|namespace|this|that|true|false|nullptr)\b)"
)

# Template parameters or arguments, with one level of nesting
ANGLES = r"<(?:[^<>]|<[^<>]*>)*>"

# A declaration's name, not preceded by member access or a scope
DECLARED_NAME = r"(?<![\w.:])(" + NOT_A_NAME + IDENT + r")\s*(:)(?!:)"


def words(*names: str) -> str:
    return r"\b(?:" + "|".join(names) + r")\b"


def declaration(scope: str, after: str, comment: str) -> dict:
    """A "name:" whose kind is told by what follows the colon."""
    return {
        "comment": comment,
        "match": DECLARED_NAME + after,
        "captures": {
            "1": {"name": scope},
            "2": {"name": "punctuation.separator.declaration.cpp2"},
        },
    }


def repository() -> dict:
    return {
        "statements": {
            "patterns": [
                {"include": f"#{name}"}
                for name in (
                    "comments", "preprocessor", "strings", "numbers", "characters",
                    "metafunctions", "declarations", "keywords", "types", "scopes",
                    "calls", "operators", "punctuation",
                )
            ]
        },
        "comments": {
            "patterns": [
                {
                    "name": "comment.line.double-slash.cpp2",
                    "begin": "//",
                    "end": "$",
                    "beginCaptures": {"0": {"name": "punctuation.definition.comment.cpp2"}},
                },
                {
                    "name": "comment.block.cpp2",
                    "begin": r"/\*",
                    "end": r"\*/",
                    "captures": {"0": {"name": "punctuation.definition.comment.cpp2"}},
                },
            ]
        },
        "preprocessor": {
            "name": "meta.preprocessor.cpp2",
            "begin": r"^\s*(#)\s*(" + IDENT + r")\b",
            "beginCaptures": {
                "1": {"name": "punctuation.definition.directive.cpp2"},
                "2": {"name": "keyword.control.directive.cpp2"},
            },
            "end": r"(?<!\\)$",
            "patterns": [
                {"include": "#comments"},
                {"name": "string.quoted.other.lt-gt.include.cpp2", "match": r"<[^>\s]+>"},
                {"include": "#strings"},
                {"include": "#numbers"},
            ],
        },
        "strings": {
            "patterns": [
                {
                    "name": "string.quoted.raw.cpp2",
                    "begin": r"(\$?(?:u8|u|U|L)?R)\"([^(\s\\]{0,16})\(",
                    "beginCaptures": {
                        "0": {"name": "punctuation.definition.string.begin.cpp2"},
                        "1": {"name": "storage.type.string.cpp2"},
                    },
                    "end": r"\)\2\"",
                    "endCaptures": {"0": {"name": "punctuation.definition.string.end.cpp2"}},
                    "patterns": [{"include": "#interpolation"}],
                },
                {
                    "name": "string.quoted.double.cpp2",
                    "begin": r"((?:u8|u|U|L)?)\"",
                    "beginCaptures": {
                        "0": {"name": "punctuation.definition.string.begin.cpp2"},
                        "1": {"name": "storage.type.string.cpp2"},
                    },
                    "end": r"\"(" + IDENT + r")?",
                    "endCaptures": {
                        "0": {"name": "punctuation.definition.string.end.cpp2"},
                        "1": {"name": "keyword.other.suffix.cpp2"},
                    },
                    "patterns": [{"include": "#escapes"}, {"include": "#interpolation"}],
                },
            ]
        },
        "escapes": {
            "name": "constant.character.escape.cpp2",
            "match": r"\\(?:[abfnrtv'\"?\\0]|x[0-9a-fA-F]+|u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|[0-7]{1,3}|N\{[^}]*\})",
        },
        # Cpp2's string interpolation, "Value: (expression)$". The lookahead
        # requires the closing ")$", allowing two levels of nested parentheses
        "interpolation": {
            "name": "meta.interpolation.cpp2",
            "begin": r"\((?=(?:[^()\"]|\((?:[^()]|\([^()]*\))*\))*\)\$)",
            "beginCaptures": {"0": {"name": "punctuation.section.interpolation.begin.cpp2"}},
            "end": r"\)\$",
            "endCaptures": {"0": {"name": "punctuation.section.interpolation.end.cpp2"}},
            "contentName": "source.cpp2.embedded",
            "patterns": [{"include": "#statements"}],
        },
        "characters": {
            "name": "string.quoted.single.cpp2",
            "match": r"(?:u8|u|U|L)?'(?:\\.|[^'\\])+'",
            "captures": {"0": {"patterns": [{"include": "#escapes"}]}},
        },
        "numbers": {
            "name": "constant.numeric.cpp2",
            "match": (
                r"(?<![\w.])(?:0[xX][0-9a-fA-F](?:'?[0-9a-fA-F])*|0[bB][01](?:'?[01])*"
                r"|\d(?:'?\d)*(?:\.\d(?:'?\d)*)?(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)"
                r"(?:[A-Za-z_]\w*)?"
            ),
        },
        # @struct, @enum<u8>, @polymorphic_base
        "metafunctions": {
            "match": r"(@)(" + IDENT + r")",
            "captures": {
                "1": {"name": "punctuation.definition.decorator.cpp2"},
                "2": {"name": "entity.name.function.decorator.cpp2"},
            },
        },
        "declarations": {
            "patterns": [
                declaration("entity.name.namespace.cpp2", r"(?=\s*namespace\b)", "name: namespace"),
                declaration(
                    "entity.name.type.cpp2",
                    r"(?=\s*(?:" + ANGLES + r"\s*)?(?:@" + IDENT + r"(?:" + ANGLES + r")?\s*)*(?:final\s+)?type\b)",
                    "name: <T: type> @meta final type",
                ),
                declaration(
                    "entity.name.type.concept.cpp2",
                    r"(?=\s*(?:" + ANGLES + r"\s*)?concept\b)",
                    "name: <T> concept",
                ),
                {
                    "comment": "operator=: (...) and the other operator functions",
                    "match": (
                        r"\b(operator)\s*(==|!=|<=>|<=|>=|<<=|>>=|<<|>>|\+\+|--|->|\(\)|\[\]"
                        r"|[-+*/%^&|~!=<>]=?)\s*(:)(?!:)"
                    ),
                    "captures": {
                        "1": {"name": "storage.type.declaration.cpp2"},
                        "2": {"name": "entity.name.function.operator.cpp2"},
                        "3": {"name": "punctuation.separator.declaration.cpp2"},
                    },
                },
                declaration(
                    "entity.name.function.cpp2",
                    r"(?=\s*(?:" + ANGLES + r"\s*)?\()",
                    "name: <T> (parameters) -> result",
                ),
                declaration("variable.other.declaration.cpp2", "", "name: type = value, name := value"),
            ]
        },
        "keywords": {
            "patterns": [
                {
                    "name": "keyword.control.cpp2",
                    "match": words(
                        "if", "else", "for", "do", "while", "next", "break", "continue", "return",
                        "inspect", "try", "catch", "throw", "co_await", "co_yield", "co_return",
                    ),
                },
                {
                    "name": "keyword.operator.word.cpp2",
                    "match": words(
                        "is", "as", "new", "sizeof", "alignof", "typeid", "decltype", "type_of",
                        "unchecked_cast", "unsafe_cast",
                    ),
                },
                {"name": "keyword.other.contract.cpp2", "match": words("pre", "post", "assert") + r"(?=\s*[<(])"},
                {
                    "name": "storage.type.declaration.cpp2",
                    "match": words("type", "namespace", "concept", "operator", "using"),
                },
                {"name": "keyword.other.module.cpp2", "match": words("import", "export", "module")},
                {
                    "name": "storage.modifier.parameter.cpp2",
                    "match": words("in", "inout", "out", "copy", "move", "forward", "in_ref", "forward_ref"),
                },
                {
                    "name": "storage.modifier.cpp2",
                    "match": words(
                        "virtual", "override", "final", "implicit", "public", "protected", "private",
                        "throws", "const", "static", "constexpr", "consteval", "inline",
                    ),
                },
                {"name": "variable.language.cpp2", "match": words("this", "that") + r"|(?<!\w)_(?!\w)"},
                {"name": "constant.language.cpp2", "match": words("true", "false", "nullptr")},
            ]
        },
        "types": {
            "name": "storage.type.built-in.cpp2",
            "match": words(
                "void", "bool", "char", "int", "float", "double", "auto", "short", "long",
                "i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64", "f32", "f64",
                "_schar", "_uchar", "ushort", "uint", "ulong", "longlong", "ulonglong",
                "longdouble", "char8_t", "char16_t", "char32_t", "wchar_t",
            ),
        },
        # std::vector, ena::platform::window
        "scopes": {
            "match": r"\b(" + IDENT + r")\s*(::)",
            "captures": {
                "1": {"name": "entity.name.scope-resolution.cpp2"},
                "2": {"name": "punctuation.separator.scope-resolution.cpp2"},
            },
        },
        # f(x), x.f() (UFCS), x..f(), f<T>(x)
        "calls": {
            "match": r"(?<!\w)(" + NOT_A_NAME + IDENT + r")\s*(?=(?:" + ANGLES + r")?\s*\()",
            "captures": {"1": {"name": "entity.name.function.call.cpp2"}},
        },
        "operators": {
            "patterns": [
                {"name": "keyword.operator.member.cpp2", "match": r"\.\.(?![.<=])|\.(?!\.)|->"},
                {"name": "keyword.operator.range.cpp2", "match": r"\.\.\.|\.\.[<=]"},
                {"name": "keyword.operator.comparison.cpp2", "match": r"<=>|==|!=|<=|>="},
                {"name": "keyword.operator.assignment.cpp2", "match": r":=|(?:<<|>>|[-+*/%&|^])?="},
                {"name": "keyword.operator.logical.cpp2", "match": r"&&|\|\||!"},
                {"name": "keyword.operator.capture.cpp2", "match": r"\$"},
                {"name": "keyword.operator.cpp2", "match": r"\+\+|--|<<|>>|[-+*/%&|^~<>]"},
            ]
        },
        "punctuation": {
            "patterns": [
                {"name": "punctuation.terminator.statement.cpp2", "match": ";"},
                {"name": "punctuation.separator.delimiter.cpp2", "match": ","},
                {"name": "punctuation.separator.colon.cpp2", "match": ":"},
            ]
        },
    }


def grammar() -> dict:
    return {
        "$schema": "https://raw.githubusercontent.com/martinring/tmlanguage/master/tmlanguage.json",
        "name": "Cpp2",
        "scopeName": "source.cpp2",
        "fileTypes": ["cpp2", "h2"],
        "patterns": [{"include": "#statements"}],
        "repository": repository(),
    }


def check_patterns(node: object) -> None:
    """Raises re.error for a regex that doesn't compile."""
    if isinstance(node, dict):
        for key in ("match", "begin", "end"):
            if key in node:
                # An end pattern's \2 refers to the begin pattern's group
                pattern = node[key].replace(r"\2", "x") if key == "end" else node[key]
                re.compile(pattern)
        for value in node.values():
            check_patterns(value)
    elif isinstance(node, list):
        for value in node:
            check_patterns(value)


def main() -> int:
    result = grammar()
    try:
        check_patterns(result)
    except re.error as error:
        print(f"A regex doesn't compile: {error}", file=sys.stderr)
        return 1
    output = Path(__file__).resolve().parent / "cpp2.tmLanguage.json"
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
