import argparse
import json
import os
import shutil
import subprocess
import time
import zipfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--kind", choices=("api", "portal"), required=True)
    parser.add_argument("--dependencies", type=Path)
    parser.add_argument("--base-archive", type=Path)
    args = parser.parse_args()
    repo = args.repo.resolve()
    output = repo / "artifacts/academy-deploy" / str(int(time.time()))
    output.mkdir(parents=True)
    if args.kind == "api":
        source = repo / "artifacts/academy-api-publish"
        assert (source / "InsuranceIntelligence.Api.dll").is_file()
    else:
        portal = repo / "web/portal"
        source = output / "portal"
        shutil.copytree(portal / ".next/standalone", source, ignore=shutil.ignore_patterns("node_modules", ".env*"))
        shutil.copytree(portal / ".next/static", source / ".next/static", dirs_exist_ok=True)
        shutil.copytree(portal / "public", source / "public", dirs_exist_ok=True)
        package = json.loads((source / "package.json").read_text())
        for name in package["dependencies"]:
            resolved = json.loads((portal / "node_modules" / name / "package.json").read_text())
            package["dependencies"][name] = resolved["version"]
        package.pop("devDependencies", None)
        (source / "package.json").write_text(json.dumps(package, indent=2))
        if args.base_archive:
            with zipfile.ZipFile(args.base_archive) as previous:
                assert json.loads(previous.read("package.json"))["dependencies"] == package["dependencies"]
        elif args.dependencies:
            dependencies = args.dependencies.resolve()
            assert dependencies.is_relative_to(repo / "artifacts/academy-deploy")
            for name, version in package["dependencies"].items():
                assert json.loads((dependencies / name / "package.json").read_text())["version"] == version
            shutil.copytree(dependencies, source / "node_modules", copy_function=os.link)
        else:
            subprocess.run([shutil.which("npm") or shutil.which("npm.cmd"), "install", "--omit=dev", "--os=linux", "--cpu=x64", "--ignore-scripts", "--no-audit", "--no-fund"], cwd=source, check=True)
        for required in ("server.js", ".next/BUILD_ID"):
            assert (source / required).is_file(), required
        assert "currentPort" in (source / "server.js").read_text()
        if not args.base_archive:
            assert (source / "node_modules/next/package.json").is_file()
            assert (source / "node_modules/styled-jsx/package.json").is_file() or (source / "node_modules/next/node_modules/styled-jsx/package.json").is_file()
    archive = output / (args.kind+".zip")
    count = 0
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as target:
        if args.base_archive:
            with zipfile.ZipFile(args.base_archive) as previous:
                for entry in previous.infolist():
                    if entry.filename.startswith("node_modules/") and not entry.is_dir():
                        target.writestr(entry.filename, previous.read(entry.filename))
                        count += 1
        for file in source.rglob("*"):
            if file.is_file():
                assert not file.is_symlink(), f"Unexpected symlink: {file.name}"
                if file.name.startswith(".env"):
                    raise RuntimeError("Environment files must not be deployed in the code archive")
                target.write(file, file.relative_to(source).as_posix())
                count += 1
    with zipfile.ZipFile(archive) as final:
        assert len(final.namelist()) == len(set(final.namelist()))
        assert final.testzip() is None
    print(json.dumps({"kind": args.kind, "archive": str(archive), "files": count, "bytes": archive.stat().st_size}))


if __name__ == "__main__":
    main()
