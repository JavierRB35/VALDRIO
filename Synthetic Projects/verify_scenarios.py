#!/usr/bin/env python3
"""Independent exact-arithmetic audit for the two T13 synthetic VALDRIO projects.

This is an oracle, NOT a substitute for running VALDRIO and comparing its
persisted company files and reciprocal allocation results.
"""
from __future__ import annotations
import csv
import json
import sys
import tempfile
from decimal import Decimal
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def as_fraction(value):
    return Fraction(str(value))


def load(folder: Path):
    manifest = json.loads((folder / "scenario.json").read_text(encoding="utf-8-sig"))
    with (folder / "accounts.csv").open(encoding="utf-8-sig", newline="") as source:
        rows = list(csv.DictReader(source, delimiter=";"))
    return manifest, rows


def solve(matrix, vector):
    n = len(vector)
    rows = [list(matrix[i]) + [vector[i]] for i in range(n)]
    for c in range(n):
        pivot = next((i for i in range(c, n) if rows[i][c] != 0), None)
        if pivot is None:
            raise ValueError("GRAPH_CLOSED_COMPONENT: singular reciprocal system")
        rows[c], rows[pivot] = rows[pivot], rows[c]
        scale = rows[c][c]
        rows[c] = [item / scale for item in rows[c]]
        for r in range(n):
            if r == c:
                continue
            factor = rows[r][c]
            rows[r] = [x - factor * y for x, y in zip(rows[r], rows[c])]
    return [row[-1] for row in rows]


def verify(folder: Path):
    scenario, rows = load(folder)
    if scenario["schema"] != "T13/SyntheticProject/v1":
        raise ValueError("Unsupported scenario schema")
    if scenario["currency"] != "EUR":
        raise ValueError("These reference examples must be in EUR")
    if not rows:
        raise ValueError("Empty imported accounts")

    nodes = scenario["nodes"]
    codes = [node["code"] for node in nodes]
    centers = [c["code"] for c in scenario["centers"]]
    if len(set(codes)) != len(codes) or len(set(centers)) != len(centers):
        raise ValueError("Duplicate node or center code")
    if any(n["center"] not in centers or n["role"] not in {"Principal", "Auxiliary"}
           or n["unit"] not in {"hour", "lot"} for n in nodes):
        raise ValueError("Invalid center, role, or unit")
    principals = [n["code"] for n in nodes if n["role"] == "Principal"]
    auxiliaries = [n["code"] for n in nodes if n["role"] == "Auxiliary"]
    rules = scenario["primaryRules"]
    secondary = scenario["secondaryRules"]
    if len(set(r["account"] for r in rules)) != len(rules):
        raise ValueError("Duplicate primary rule")
    if sorted(r["auxiliary"] for r in secondary) != sorted(auxiliaries):
        raise ValueError("Expected exactly one secondary rule per auxiliary")

    def validated_weights(weights):
        if not weights or set(weights).difference(codes):
            raise ValueError("Unknown allocation destination")
        values = {key: as_fraction(value) for key, value in weights.items()}
        if any(value < 0 for value in values.values()) or sum(values.values()) != 100:
            raise ValueError("Allocation percentages do not total 100")
        return values

    primary_rules = {r["account"]: validated_weights(r["weights"]) for r in rules}
    secondary_rules = {r["auxiliary"]: validated_weights(r["weights"]) for r in secondary}
    raw = {}
    for row in rows:
        if set(row) != {"Amount", "Description", "Account"}:
            raise ValueError("Unexpected CSV columns")
        code = row["Account"]
        if not code or not row["Description"].strip() or code in raw:
            raise ValueError("Missing account/description or duplicate account")
        if code not in primary_rules:
            raise ValueError(f"Unmapped account: {code}")
        # Decimal commas are explicit in this synthetic source.
        amount = Fraction(Decimal(row["Amount"].replace(",", ".")))
        raw[code] = amount
    if set(raw) != set(primary_rules):
        raise ValueError("Unaccounted source or unused primary key")

    expected = scenario["expected"]
    tolerance = as_fraction(expected["maxAbsoluteError"])
    total = sum(raw.values())
    if total != as_fraction(expected["importTotal"]):
        raise ValueError("Import total differs from expected")
    primary = {node: Fraction(0) for node in codes}
    for account, amount in raw.items():
        for node, weight in primary_rules[account].items():
            primary[node] += amount * weight / 100
    if sum(primary.values()) != total:
        raise ValueError("Primary allocation is not conserved")
    for node in codes:
        if abs(primary[node] - as_fraction(expected["primaryTotals"][node])) > tolerance:
            raise ValueError(f"Primary node total mismatch: {node}")

    q = [[secondary_rules[source].get(dest, Fraction(0)) / 100
          for dest in auxiliaries] for source in auxiliaries]
    b = [[secondary_rules[source].get(dest, Fraction(0)) / 100
          for dest in principals] for source in auxiliaries]
    # Reachability must be checked independently of costs, including zero-cost nodes.
    reachable = {auxiliaries[i] for i in range(len(auxiliaries)) if any(b[i])}
    changed = True
    while changed:
        before = len(reachable)
        for i, source in enumerate(auxiliaries):
            if any(q[i][j] and auxiliaries[j] in reachable for j in range(len(auxiliaries))):
                reachable.add(source)
        changed = len(reachable) > before
    if len(reachable) != len(auxiliaries):
        raise ValueError("GRAPH_CLOSED_COMPONENT: no terminal route")

    system = [[Fraction(i == j) - q[j][i]
               for j in range(len(auxiliaries))] for i in range(len(auxiliaries))]
    gross = solve(system, [primary[aux] for aux in auxiliaries])
    terminal = {key: primary[key] for key in principals}
    for i, _aux in enumerate(auxiliaries):
        for j, node in enumerate(principals):
            terminal[node] += gross[i] * b[i][j]
    if sum(terminal.values()) != total:
        raise ValueError("Secondary allocation is not conserved")
    for node in principals:
        if abs(terminal[node] - as_fraction(expected["terminalTotals"][node])) > tolerance:
            raise ValueError(f"Terminal amount mismatch: {node}")
    print(f"{scenario['id']}: OK, {len(rows)} accounts, "
          f"{len(principals)} principals, {len(auxiliaries)} reciprocal auxiliaries, "
          f"EUR {float(total):.2f}")
    return terminal


def main():
    try:
        directories = [ROOT / "Service Company", ROOT / "Industrial Company"]
        for folder in directories:
            verify(folder)
        if "--self-test" in sys.argv:
            try:
                solve([[Fraction(0)]], [Fraction(1)])
            except ValueError:
                pass
            else:
                raise AssertionError("Singular system must be rejected")

            with tempfile.TemporaryDirectory(prefix="valdrio-t13-") as scratch:
                test_dir = Path(scratch)
                original = ROOT / "Service Company"
                scenario, rows = load(original)
                content = (original / "accounts.csv").read_text(encoding="utf-8")

                def rejected(label, mutated, csv_content=content):
                    (test_dir / "scenario.json").write_text(
                        json.dumps(mutated), encoding="utf-8")
                    (test_dir / "accounts.csv").write_text(
                        csv_content, encoding="utf-8")
                    try:
                        verify(test_dir)
                    except (ValueError, KeyError, ArithmeticError):
                        print(f"Negative {label}: OK")
                    else:
                        raise AssertionError(f"Invalid {label} was accepted")

                invalid = json.loads(json.dumps(scenario))
                invalid["primaryRules"][0]["weights"]["DELIVERY"] += 1
                rejected("percentages", invalid)

                invalid = json.loads(json.dumps(scenario))
                invalid["secondaryRules"][0]["weights"] = {"ADMIN": 100}
                rejected("closed auxiliary self-loop", invalid)

                invalid = json.loads(json.dumps(scenario))
                invalid["expected"]["importTotal"] = "17501.00"
                rejected("incorrect expected money", invalid)

                invalid = json.loads(json.dumps(scenario))
                rejected("duplicate account", invalid, content + rows[0]["Amount"] +
                         ";" + rows[0]["Description"] + ";" + rows[0]["Account"] + "\\n")
            print("All negative verification self-tests: OK")
    except (ValueError, KeyError, OSError, ArithmeticError, AssertionError) as exc:
        print(f"T13 REJECTED: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
