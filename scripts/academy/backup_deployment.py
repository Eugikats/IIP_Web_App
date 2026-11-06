import json
import shutil
import subprocess
import urllib.error
import urllib.request
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def main():
    result = subprocess.run([shutil.which("az") or shutil.which("az.cmd"), "account", "get-access-token", "--resource",
                             "https://management.azure.com/", "--subscription", "5f5765ae-985f-42e4-a443-8e2447be33ca",
                             "--query", "accessToken", "-o", "tsv"], capture_output=True, text=True, check=True)
    token = result.stdout.strip()
    target = ROOT / "artifacts/academy-rollback"
    target.mkdir(parents=True, exist_ok=True)
    for kind in ("api", "portal"):
        archive = target / (kind+"-before.zip")
        if archive.exists():
            raise RuntimeError("Do not overwrite a saved pre-deployment backup")
        call = urllib.request.Request(f"https://app-{kind}-dev-h5girbnzrayqe.scm.azurewebsites.net/api/zip/site/wwwroot/",
                                      headers={"Authorization": "Bearer "+token})
        try:
            with urllib.request.urlopen(call, timeout=300) as response, archive.with_suffix(".partial").open("wb") as output:
                shutil.copyfileobj(response, output)
            archive.with_suffix(".partial").replace(archive)
            with zipfile.ZipFile(archive) as saved:
                assert saved.testzip() is None
            print(json.dumps({"rollback": kind, "archive": str(archive), "bytes": archive.stat().st_size}), flush=True)
        except urllib.error.HTTPError as error:
            raise RuntimeError(f"Rollback backup returned HTTP {error.code}; no response or token logged") from None


if __name__ == "__main__":
    main()
