#!/usr/bin/env python3
"""Generate pluginmaster.json from the compiled plugin manifest.

Usage:
    python3 scripts/update-pluginmaster.py <built-manifest.json> <release-tag> [owner/repo]

The built manifest is produced by `dotnet build` in bin/<Configuration>/.
The release tag is used to build the GitHub Releases download URL.
"""

from __future__ import annotations

import json
import os
import sys
import time


def main() -> int:
    manifest_path = sys.argv[1] if len(sys.argv) > 1 else "bin/Release/Linkpearl.json"
    tag = sys.argv[2] if len(sys.argv) > 2 else os.environ.get("GITHUB_REF_NAME", "")
    repo = (
        sys.argv[3]
        if len(sys.argv) > 3
        else os.environ.get("GITHUB_REPOSITORY", "yjkz/ChatAnywhere")
    )

    if not tag:
        raise SystemExit("release tag is required (argument or GITHUB_REF_NAME)")

    with open(manifest_path, encoding="utf-8") as handle:
        manifest = json.load(handle)

    version = tag[1:] if tag.startswith("v") else tag
    download_url = f"https://github.com/{repo}/releases/download/{tag}/Linkpearl.zip"
    raw_base = f"https://raw.githubusercontent.com/{repo}/main/images"

    entry = {
        "Author": manifest.get("Author", ""),
        "Name": manifest.get("Name", "Linkpearl"),
        "Description": manifest.get("Description", ""),
        "InternalName": manifest.get("InternalName", "Linkpearl"),
        "AssemblyVersion": manifest.get("AssemblyVersion", version),
        "TestingAssemblyVersion": None,
        "RepoUrl": manifest.get("RepoUrl", f"https://github.com/{repo}"),
        "ApplicableVersion": manifest.get("ApplicableVersion", "any"),
        "DalamudApiLevel": manifest.get("DalamudApiLevel", 15),
        "Punchline": manifest.get("Punchline", ""),
        "IsHide": False,
        "IsTestingExclusive": False,
        "DownloadLinkInstall": download_url,
        "DownloadLinkUpdate": download_url,
        "LastUpdate": int(time.time()),
        "IconUrl": manifest.get("IconUrl", f"{raw_base}/icon.png"),
        "ImageUrls": manifest.get(
            "ImageUrls",
            [
                f"{raw_base}/readme-chat.png",
                f"{raw_base}/readme-mobile.png",
                f"{raw_base}/readme-filter.png",
                f"{raw_base}/readme-appearance.png",
            ],
        ),
        "Tags": manifest.get("Tags", []),
    }

    if str(entry["AssemblyVersion"]) != version:
        print(
            f"warning: manifest AssemblyVersion {entry['AssemblyVersion']} does not match tag {version}",
            file=sys.stderr,
        )

    with open("pluginmaster.json", "w", encoding="utf-8") as handle:
        json.dump([entry], handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    print(f"Wrote pluginmaster.json for {entry['InternalName']} {entry['AssemblyVersion']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
