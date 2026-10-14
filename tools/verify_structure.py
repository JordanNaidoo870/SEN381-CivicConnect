"""Static package consistency check only; cannot replace dotnet build/test."""
from pathlib import Path
import json
import re
from xml.etree import ElementTree

root = Path(__file__).resolve().parents[1]
projects = list(root.rglob("*.csproj"))
assert len(projects) == 6, f"Expected six projects, found {len(projects)}"
for p in projects:
    doc = ElementTree.parse(p)
    assert any("net10.0" in (elem.text or "") for elem in doc.iter()), p
    for ref in doc.iter("ProjectReference"):
        assert (p.parent / ref.attrib["Include"]).exists(), ref.attrib["Include"]
for config in root.rglob("*.json"):
    json.loads(config.read_text())
for path in (root / "src/CivicConnect.Web/Views").rglob("*.cshtml"):
    assert path.read_text().strip(), path
ids = []
for p in (root / "tests").rglob("*.cs"):
    ids.extend(re.findall(r"(?:UNIT|INT|BB|E2E|SEC)\d\d(?=_)", p.read_text()))
ids = set(ids)
for tag, count in [("UNIT", 12), ("INT", 4), ("BB", 5), ("E2E", 2), ("SEC", 3)]:
    found = sum(x.startswith(tag) for x in ids)
    assert found >= count, f"Missing expected {tag} test definitions: {found}/{count}"
assert "Password=YOUR_LOCAL_PASSWORD" in (root/"README.md").read_text()
print(f"PASS structural checks: {len(projects)} projects, {len(ids)} named test scenarios, valid XML/JSON, references/views present")
print("NOT A BUILD OR TEST RUN: execute dotnet restore/build/test on Windows or GitHub Actions")
