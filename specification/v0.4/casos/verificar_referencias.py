#!/usr/bin/env python3
"""Verifica ejemplos sintéticos con fracciones, no una aplicación VALDRIO.

Biblioteca estándar de Python. Las salidas esperadas están escritas en el JSON.
Este oráculo es deliberadamente distinto del solver LU decimal propuesto en C#.
No importa código del motor futuro ni genera sus resultados esperados.
"""
from fractions import Fraction as F
from pathlib import Path
import json
import sys


def f(value):
    return value if isinstance(value, F) else F(value)


def balanced_round(source, raw, quantum):
    source, quantum = f(source), f(quantum)
    values = {k: f(v) for k, v in raw.items()}
    assert sum(values.values(), F(0)) == source, "Fuente sin conservar"
    assert quantum > 0 and (source / quantum).denominator == 1
    sign = -1 if source < 0 else 1
    units = {k: sign * v / quantum for k, v in values.items()}
    base = {k: v.numerator // v.denominator for k, v in units.items()}
    needed = int(sign * source / quantum) - sum(base.values())
    assert 0 <= needed < max(1, len(base))
    order = sorted(units, key=lambda k: (-(units[k] - base[k]), k))
    for key in order[:needed]:
        base[key] += 1
    result = {k: sign * n * quantum for k, n in base.items()}
    assert sum(result.values(), F(0)) == source
    return result


def weighted(source, weights, quantum):
    total = sum((f(v) for v in weights.values()), F(0))
    assert total > 0 and all(f(v) >= 0 for v in weights.values())
    return balanced_round(source, {k: f(source) * f(v) / total for k, v in weights.items()}, quantum)


def solve_exact(matrix, rhs):
    n = len(rhs)
    a = [[f(x) for x in row] + [f(rhs[i])] for i, row in enumerate(matrix)]
    for j in range(n):
        pivot = next((i for i in range(j, n) if a[i][j] != 0), None)
        if pivot is None:
            raise ValueError("SINGULAR")
        a[j], a[pivot] = a[pivot], a[j]
        divisor = a[j][j]
        a[j] = [v / divisor for v in a[j]]
        for i in range(n):
            if i != j:
                factor = a[i][j]
                a[i] = [a[i][k] - factor * a[j][k] for k in range(n + 1)]
    return [a[i][-1] for i in range(n)]


def reciprocal(data):
    q = [[f(v) for v in row] for row in data["Q"]]
    b = [[f(v) for v in row] for row in data["B"]]
    ba = list(map(f, data["b_aux"]))
    bp = list(map(f, data["b_principal"]))
    n, p = len(ba), len(bp)
    assert all(sum(q[i]) + sum(b[i]) == 1 for i in range(n))
    assert all(v >= 0 for row in q + b for v in row)
    if not data["allow_self"] and any(q[i][i] != 0 for i in range(n)):
        return {}, "GRAPH_SELF_NOT_ALLOWED"
    escaping = {i for i in range(n) if any(b[i])}
    while True:
        extended = escaping | {i for i in range(n) if any(q[i][j] > 0 and j in escaping for j in range(n))}
        if extended == escaping:
            break
        escaping = extended
    if len(escaping) != n:
        return {}, "GRAPH_CLOSED_COMPONENT"
    matrix = [[(F(1) if i == j else F(0)) - q[j][i] for j in range(n)] for i in range(n)]
    gross = solve_exact(matrix, ba)
    if "max_amplification" in data:
        amplification = max((abs(v) for v in gross), default=F(0)) / max(max((abs(v) for v in ba), default=F(0)), F(1))
        if amplification > f(data["max_amplification"]):
            return {}, "SOLVER_SENSITIVE"
    final = [bp[j] + sum((b[i][j] * gross[i] for i in range(n)), F(0)) for j in range(p)]
    assert sum(final) == sum(ba) + sum(bp)
    origins = {}
    posted = {"0.00000001": [F(0)] * p, "0.01": [F(0)] * p}
    for i in range(n):
        rhs = [ba[k] if i == k else F(0) for k in range(n)]
        z = solve_exact(matrix, rhs)
        contributions = [sum((b[k][j] * z[k] for k in range(n)), F(0)) for j in range(p)]
        assert sum(contributions) == ba[i]
        origins[data["aux_ids"][i]] = contributions
        raw = dict(zip(data["principal_ids"], contributions))
        for quantum in posted:
            rounded = balanced_round(ba[i], raw, quantum)
            posted[quantum] = [posted[quantum][j] + rounded[key] for j, key in enumerate(data["principal_ids"])]
    for quantum in posted:
        for j, value in enumerate(bp):
            # Los primarios propios de este corpus son importes íntegros de moneda.
            assert (value / f(quantum)).denominator == 1
            posted[quantum][j] += value
        assert sum(posted[quantum]) == sum(final)
    return {"gross": gross, "final": final, "by_aux_origin": origins, "total": sum(final),
            "posted_8": posted["0.00000001"], "projected_currency": posted["0.01"]}, None


def capacity(data):
    cf, cv, qn, qr = (f(data[k]) for k in ("fixed", "variable", "normal", "real"))
    quantities = {k: f(v) for k, v in data["objects"].items()}
    if qn <= 0 and cf != 0:
        return {}, "CAP_ZERO_DENOMINATOR"
    if qr == 0 and cv != 0:
        return {}, "ACT_COST_WITH_ZERO_ACTIVITY"
    assert sum(quantities.values(), F(0)) <= qr and qr >= 0
    denom = max(qn, qr) if data["policy"] == "ProductionLimitedReference" else qn
    rf = cf / denom if denom else None
    rv = cv / qr if qr else None
    objects = {k: ((rf or F(0)) + (rv or F(0))) * v for k, v in quantities.items()}
    cap = cf - (rf or F(0)) * qr
    unassigned = ((rf or F(0)) + (rv or F(0))) * (qr - sum(quantities.values(), F(0)))
    assert sum(objects.values(), F(0)) + cap + unassigned == cf + cv
    return {"fixed_rate": rf, "variable_rate": rv, "objects": objects, "capacity_variance": cap,
            "activity_unassigned": unassigned, "source_total": cf + cv}, None


def evaluate(case):
    d, family = case["input"], case["family"]
    if family == "fx":
        original = f(d["original_amount"])
        if d["original_currency"] == d["functional_currency"]:
            quote = F(1)
        elif d["quote"] is None:
            return {}, "FX_RATE_MISSING"
        else:
            quote = f(d["quote"])
        if quote <= 0:
            return {}, "FX_RATE_INVALID"
        raw = original * quote
        quantum = F(1, 100000000)
        units = raw / quantum
        lower = units.numerator // units.denominator
        rest = units - lower
        rounded_units = lower + (1 if rest > F(1,2) or (rest == F(1,2) and lower % 2 != 0) else 0)
        converted = rounded_units * quantum
        return {"original_amount":original, "original_currency":d["original_currency"],
                "functional_amount":converted, "functional_currency":d["functional_currency"],
                "quote":quote, "conversion_rounding":converted-raw}, None
    if family == "reciprocal":
        return reciprocal(d)
    if family == "capacity":
        return capacity(d)
    if family == "period_difference":
        amount = f(d["current"]) - f(d["previous"]) if d["mode"] == "CumulativeExpenseDifference" else f(d["current"])
        return {"period_amount": amount}, None
    if family == "invalid_amount":
        try:
            f(d["text"])
        except (ValueError, ZeroDivisionError):
            return {}, "IMP_INVALID_AMOUNT"
        raise AssertionError("Caso inválido aceptado")
    if family == "adjust_partition":
        effective = f(d["original"]) + sum(map(f, d["adjustments"]), F(0))
        difference = effective - sum(map(f, d["parts"].values()), F(0))
        return {"effective": effective, "difference": difference}, "RES_PARTITION_MISMATCH" if difference else None
    if family == "percentage_coverage":
        coverage = sum(map(f, d["percentages"]), F(0))
        return {"coverage": coverage}, "KEY_INCOMPLETE" if coverage != 100 else None
    if family in ("rounding", "signed_rounding"):
        if family == "rounding":
            posted = weighted(d["amount"], d["weights"], d["quantum"])
        else:
            posted = balanced_round(d["source"], d["raw"], d["quantum"])
        return {"posted": posted, "total": sum(posted.values(), F(0))}, None
    if family == "aux_owner_capacity":
        a, b = d["A"], d["B"]
        acf, bcf = f(a["fixed"]), f(b["fixed"])
        aa = acf * f(a["real"]) / f(a["normal"])
        ba = bcf * f(b["real"]) / f(b["normal"])
        assert f(a["service_to_B"]) + f(a["service_to_P"]) == f(a["real"])
        assert f(b["service_to_P"]) == f(b["real"])
        atb = aa * f(a["service_to_B"]) / f(a["real"]) if f(a["real"]) else F(0)
        atp = aa - atb
        btp = ba + atb
        acap, bcap = acf - aa, bcf - ba
        assert atp + btp + acap + bcap == acf + bcf
        return {"A_applied": aa, "A_capacity": acap, "A_to_B": atb, "A_to_P": atp,
                "B_own_applied": ba, "B_capacity": bcap, "B_to_P": btp, "P": atp + btp,
                "source_total": acf + bcf}, None
    if family == "batch_order":
        quantity, size = f(d["quantity"]), f(d["batch_size"])
        q = quantity / size
        batches = F(-(-q.numerator // q.denominator))
        setup, process = batches * f(d["setup_per_batch"]), quantity * f(d["process_per_unit"])
        sc, pc = setup * f(d["setup_rate"]), process * f(d["process_rate"])
        return {"started_batches": batches, "setup_quantity": setup, "process_quantity": process,
                "setup_cost": sc, "process_cost": pc, "total": sc + pc, "unit_cost": (sc + pc) / quantity}, None
    if family == "margin":
        sales, variable, fixed = map(f, (d["sales"], d["variable_cost"], d["fixed_applied"]))
        cost = variable + fixed
        return {"cost": cost, "margin": sales-cost, "margin_on_sales": (sales-cost)/sales,
                "markup": (sales-cost)/cost, "contribution": sales-variable,
                "contribution_on_sales": (sales-variable)/sales}, None
    if family == "abc":
        objects = {}
        assert sum((f(p["cost"]) for p in d["pools"]), F(0)) == f(d["center_cost"])
        for pool in d["pools"]:
            denom = sum(map(f, pool["weights"].values()), F(0))
            for obj, value in pool["weights"].items():
                objects[obj] = objects.get(obj, F(0)) + f(pool["cost"]) * f(value)/denom
        assert sum(objects.values()) == f(d["center_cost"])
        return {"objects": objects, "total": sum(objects.values())}, None
    if family == "frontier":
        products, customers = {}, {}
        total = F(0)
        assert len({fact["id"] for fact in d["facts"]}) == len(d["facts"])
        for fact in d["facts"]:
            amount = f(fact["amount"])
            total += amount
            for dimension, result in (("product", products), ("customer", customers)):
                key = fact[dimension]
                result[key] = result.get(key, F(0)) + amount
        assert sum(products.values()) == sum(customers.values()) == total
        return {"source_total": total, "by_product": products, "by_customer": customers}, None
    if family == "tdabc":
        rate = f(d["supplied_cost"]) / f(d["practical_minutes"])
        minutes = f(d["base_minutes"]) + f(d["minutes_per_line"])*f(d["line_count"])
        cost = rate * minutes
        return {"rate_per_minute": rate, "transaction_minutes": minutes, "transaction_cost": cost,
                "remaining_capacity_cost": f(d["supplied_cost"])-cost}, None
    if family == "standard_variance":
        pa, qa, ps, qs = (f(d[k]) for k in ("actual_price", "actual_quantity", "standard_price", "allowed_standard_quantity"))
        actual, allowed = pa*qa, ps*qs
        price, quantity = (pa-ps)*qa, ps*(qa-qs)
        assert actual-allowed == price+quantity
        return {"actual": actual, "allowed": allowed, "total_variance": actual-allowed,
                "price_variance": price, "quantity_variance": quantity}, None
    if family == "per_source_reconciliation":
        diffs = {k: f(v)-f(d["outputs"][k]) for k, v in d["sources"].items()}
        return {"global_difference": sum(diffs.values()), "source_differences": diffs}, "RES_PARTITION_MISMATCH" if any(diffs.values()) else None
    raise AssertionError(f"Familia sin implementar {family}")


def compare(actual, expected, path):
    if isinstance(expected, dict):
        for key, value in expected.items():
            assert key in actual, f"{path}.{key} no producido"
            compare(actual[key], value, f"{path}.{key}")
    elif isinstance(expected, list):
        assert len(actual) == len(expected), path
        for i, value in enumerate(expected):
            compare(actual[i], value, f"{path}[{i}]")
    elif expected is None:
        assert actual is None, f"{path}: esperado null, observado {actual}"
    else:
        target = expected if isinstance(actual, str) else f(expected)
        assert actual == target, f"{path}: observado {actual}, esperado {expected}"


def main():
    location = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).with_name("casos_metodologicos.json")
    corpus = json.loads(location.read_text(encoding="utf-8"))
    assert len({c["id"] for c in corpus["cases"]}) == len(corpus["cases"])
    verified = []
    for case in corpus["cases"]:
        result, diagnostic = evaluate(case)
        assert diagnostic == case.get("expected_diagnostic"), f"{case['id']}: diagnóstico {diagnostic}"
        compare(result, case.get("expected", {}), case["id"])
        verified.append(case["id"])
    print(json.dumps({"status":"passed", "verified_count":len(verified), "case_ids":verified,
                      "scope":"Referencias sintéticas con Fraction; no prueba del motor C#, de SQLite o de WPF"}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
