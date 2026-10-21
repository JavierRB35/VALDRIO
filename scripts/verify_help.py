#!/usr/bin/env python3
"""VALDRIO: document coverage and same-PR source/help consistency. Stdlib only."""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SECTIONS = (
    "## 1. Objetivo y función financiera",
    "## 2. Guía de campos y entradas (Inputs)",
    "## 3. Interpretación de resultados (Outputs)",
    "## 4. Casos de uso, validaciones y errores frecuentes",
)

def run(base=None):
    issues = []
    manifest = json.loads((ROOT / "docs/help/help-map.json").read_text(encoding="utf-8"))
    views = manifest["views"]
    seen_ids, seen_docs, seen_ui = set(), set(), set()
    for v in views:
        view_id = v["viewId"]
        path = "docs/help/" + v["file"]
        if view_id in seen_ids:
            issues.append("ID duplicado: " + view_id)
        if path in seen_docs:
            issues.append("Documento compartido por IDs: " + path)
        seen_ids.add(view_id)
        seen_docs.add(path)
        doc = ROOT / path
        if not doc.is_file():
            issues.append("Ayuda ausente: " + path)
            continue
        content = doc.read_text(encoding="utf-8")
        if not re.search(r"\*\*ID Vista UI:\*\*\s*`" + re.escape(view_id) + r"`", content):
            issues.append("ID distinto en documento: " + path)
        if not re.search(r"\*\*Última actualización:\*\*\s*`\d{4}-\d{2}-\d{2}`", content):
            issues.append("Fecha inválida: " + path)
        if any(text not in content for text in SECTIONS):
            issues.append("Faltan los 4 apartados: " + path)
        if content.count("|") < 12 or "[Campo]" in content:
            issues.append("Tabla incompleta o plantilla sin adaptar: " + path)
        en_doc = ROOT / "docs/help/en" / v["file"]
        if not en_doc.is_file():
            issues.append("English help missing: " + str(en_doc.relative_to(ROOT)))
        else:
            english = en_doc.read_text(encoding="utf-8")
            if any(header not in english for header in (
                "## 1. Purpose and financial function", "## 2. Fields and inputs",
                "## 3. Results and interpretation", "## 4. Use cases, validation and common errors")):
                issues.append("English sections missing: " + str(en_doc.relative_to(ROOT)))
            if english.count("|") < 12:
                issues.append("English table incomplete: " + str(en_doc.relative_to(ROOT)))
        for source in v["uiSources"] + v["codeSources"]:
            if not (ROOT / source).is_file():
                issues.append("Referencia a archivo inexistente: " + source)
        seen_ui.update(v["uiSources"])

    ui_root = ROOT / "src/Valdrio.Desktop"
    xamls = {p.relative_to(ROOT).as_posix() for p in ui_root.rglob("*.xaml")
             if p.name != "App.xaml" and "Styles" not in p.parts}
    missing = xamls - seen_ui
    if missing:
        issues.append("Vistas XAML sin ayuda: " + ", ".join(sorted(missing)))
    if base:
        diff = subprocess.run(
            ["git", "diff", "--name-only", "--diff-filter=ACMRT", base + "...HEAD"],
            cwd=ROOT, capture_output=True, text=True, check=True)
        changed = set(diff.stdout.splitlines())
        for v in views:
            if changed.intersection(v["uiSources"] + v["codeSources"]):
                expected = "docs/help/" + v["file"]
                if expected not in changed:
                    issues.append("Se modificó UI/VM sin actualizar ayuda: " + v["viewId"] +
                                  " (" + expected + ")")
                english = "docs/help/en/" + v["file"]
                if english not in changed:
                    issues.append("UI/VM changed without English help revision: " + english)
    for issue in issues:
        print("ERROR:", issue, file=sys.stderr)
    if issues:
        return 1
    print(f"OK: {len(views)} vistas lógicas, {len(xamls)} archivos XAML cubiertos.")
    return 0

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", help="Rama/commit base para comprobar sincronización del PR")
    sys.exit(run(parser.parse_args().base))
