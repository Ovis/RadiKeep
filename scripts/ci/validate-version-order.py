#!/usr/bin/env python3
import argparse
import re
import subprocess
import sys
from functools import cmp_to_key
from typing import Optional, Tuple

SEMVER_PATTERN = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$")
SemVer = Tuple[int, int, int, Optional[str]]


def parse_semver(value: str) -> Optional[SemVer]:
    match = SEMVER_PATTERN.fullmatch(value.strip())
    if not match:
        return None
    major, minor, patch = map(int, match.group(1, 2, 3))
    prerelease = match.group(4)
    return (major, minor, patch, prerelease)


def compare_identifier(left: str, right: str) -> int:
    left_is_num = left.isdigit()
    right_is_num = right.isdigit()
    if left_is_num and right_is_num:
        return (int(left) > int(right)) - (int(left) < int(right))
    if left_is_num and not right_is_num:
        return -1
    if not left_is_num and right_is_num:
        return 1
    return (left > right) - (left < right)


def compare_semver(left: SemVer, right: SemVer) -> int:
    left_core = left[:3]
    right_core = right[:3]
    if left_core != right_core:
        return (left_core > right_core) - (left_core < right_core)

    left_pre = left[3]
    right_pre = right[3]
    if left_pre is None and right_pre is None:
        return 0
    if left_pre is None:
        return 1
    if right_pre is None:
        return -1

    left_parts = left_pre.split(".")
    right_parts = right_pre.split(".")
    for left_part, right_part in zip(left_parts, right_parts):
        compared = compare_identifier(left_part, right_part)
        if compared != 0:
            return compared
    return (len(left_parts) > len(right_parts)) - (len(left_parts) < len(right_parts))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True, help="Target version (e.g. 1.2.3 or 1.2.3-beta.1)")
    args = parser.parse_args()

    target = parse_semver(args.version)
    if target is None:
        print("version が SemVer 形式ではありません。例: 1.2.3 / 1.2.3-beta.1", file=sys.stderr)
        return 1

    completed = subprocess.run(
        ["git", "tag", "-l", "v*"],
        capture_output=True,
        text=True,
        check=True,
    )
    tags = [line.strip() for line in completed.stdout.splitlines() if line.strip()]

    parsed_tags = []
    for tag in tags:
        parsed = parse_semver(tag)
        if parsed is not None:
            parsed_tags.append((tag, parsed))

    if not parsed_tags:
        return 0

    latest_tag, latest_version = sorted(parsed_tags, key=cmp_to_key(lambda a, b: compare_semver(a[1], b[1])))[-1]
    if compare_semver(target, latest_version) <= 0:
        print(
            f"指定 version ({args.version}) は既存最新タグ ({latest_tag}) 以下のため使用できません。"
            " 既存最新より大きいバージョンを指定してください。",
            file=sys.stderr,
        )
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
