#!/usr/bin/env python3
"""Evaluación de punta a punta del asistente documental.

Envía cada pregunta de preguntas.json a POST /api/chat/ask, lee el flujo SSE y guarda
tipo de respuesta, puntaje máximo, latencia, fuentes y respuesta. Las preguntas de
seguimiento envían como historial la pregunta previa y la respuesta real que obtuvo.
Calcula además una calificación automática por hechos clave y la validez de las citas.

Uso:
    python3 scripts/rag_eval/run_eval.py [--api http://localhost:5000] [--pausa 3.5]

Solo usa la biblioteca estándar.
"""
import argparse
import json
import re
import statistics
import sys
import time
import unicodedata
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent


def ask(api, question, history, timeout=120):
    body = json.dumps({"question": question, "history": history}).encode()
    req = urllib.request.Request(f"{api}/api/chat/ask", data=body, method="POST",
                                 headers={"Content-Type": "application/json", "Accept": "text/event-stream"})
    start = time.perf_counter()
    first_token = None
    events = []
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        event, data = None, []
        for raw in resp:
            line = raw.decode("utf-8").rstrip("\r\n")
            if line.startswith("event:"):
                event = line[6:].strip()
            elif line.startswith("data:"):
                data.append(line[5:].strip())
            elif line == "" and event:
                payload = json.loads("\n".join(data))
                if event == "token" and first_token is None:
                    first_token = time.perf_counter() - start
                events.append((event, payload))
                event, data = None, []
    total = time.perf_counter() - start
    done = next((p for e, p in events if e == "done"), None)
    error = next((p.get("message") for e, p in events if e == "error"), None)
    sources_event = next((p for e, p in events if e == "sources"), None)
    return {
        "kind": done["kind"] if done else "error",
        "initialKind": sources_event["kind"] if sources_event else None,
        "topScore": done["topScore"] if done else None,
        "answer": done["answer"] if done else "",
        "sources": done["sources"] if done else [],
        "latency": round(total, 3),
        "firstToken": round(first_token, 3) if first_token is not None else None,
        "error": error,
    }


def norm(text):
    text = unicodedata.normalize("NFD", text.lower())
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


def grade(q, r):
    """Calificación automática: correcta si aparecen todos los grupos de hechos, parcial si alguno."""
    if q["expected"] == "abstained" or r["kind"] != "rag":
        return None, []
    answer = norm(r["answer"])
    missing = [group for group in q["facts"] if not any(norm(alt) in answer for alt in group)]
    if not missing:
        return "correcta", missing
    return ("parcial" if len(missing) < len(q["facts"]) else "incorrecta"), missing


def citations_valid(r):
    cited = {int(n) for n in re.findall(r"\[(\d+)\]", r["answer"])}
    available = {s["n"] for s in r["sources"]}
    return cited <= available, sorted(cited - available)


def pct(values, p):
    values = sorted(values)
    if not values:
        return None
    k = (len(values) - 1) * p
    lo, hi = int(k), min(int(k) + 1, len(values) - 1)
    return round(values[lo] + (values[hi] - values[lo]) * (k - lo), 3)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5000")
    ap.add_argument("--preguntas", default=str(HERE / "preguntas.json"))
    ap.add_argument("--pausa", type=float, default=3.5, help="segundos entre consultas")
    ap.add_argument("--salida", default=None)
    ap.add_argument("--solo", default=None, help="ids separados por coma")
    args = ap.parse_args()

    health = json.load(urllib.request.urlopen(f"{args.api}/api/chat/health", timeout=10))
    questions = json.load(open(args.preguntas, encoding="utf-8"))["preguntas"]
    if args.solo:
        wanted = set(args.solo.split(","))
        parents = {q["followUp"] for q in questions if q["id"] in wanted and q.get("followUp")}
        questions = [q for q in questions if q["id"] in wanted | parents]

    results, by_id = [], {}
    for i, q in enumerate(questions):
        history = []
        if q.get("followUp"):
            parent_q = next(p for p in questions if p["id"] == q["followUp"])
            parent = by_id[q["followUp"]]
            history = [{"role": "user", "content": parent_q["question"]},
                       {"role": "assistant", "content": parent["answer"]}]
        if i:
            time.sleep(args.pausa)
        r = ask(args.api, q["question"], history)
        auto, missing = grade(q, r)
        valid, invalid = citations_valid(r)
        row = {"id": q["id"], "tema": q["tema"], "question": q["question"], "followUp": q.get("followUp"),
               "expected": q["expected"], **r, "abstentionCorrect": (r["kind"] == "abstained") == (q["expected"] == "abstained"),
               "autoGrade": auto, "missingFacts": missing, "citationsValid": valid, "invalidCitations": invalid}
        by_id[q["id"]] = r
        results.append(row)
        print(f"{q['id']:4} {r['kind']:9} score={r['topScore']} {r['latency']:5.1f}s "
              f"{'OK ' if row['abstentionCorrect'] else 'MAL'} {auto or '-':10} {q['question'][:60]}", flush=True)

    summary = summarize(results)
    out = Path(args.salida) if args.salida else HERE / "resultados" / f"eval-{datetime.now(timezone.utc):%Y%m%dT%H%M%SZ}.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({"health": health, "summary": summary, "results": results}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"Resultado: {out}")


def summarize(results):
    expected_abst = [r for r in results if r["expected"] == "abstained"]
    predicted_abst = [r for r in results if r["kind"] == "abstained"]
    true_abst = [r for r in expected_abst if r["kind"] == "abstained"]
    answerable = [r for r in results if r["expected"] == "rag"]
    graded = [r["autoGrade"] or "abstención indebida" for r in answerable]
    rag_answers = [r for r in results if r["kind"] == "rag"]
    latencies = [r["latency"] for r in results]
    rag_lat = [r["latency"] for r in rag_answers]
    return {
        "preguntas": len(results),
        "abstencion": {
            "precision": round(len(true_abst) / len(predicted_abst), 3) if predicted_abst else None,
            "recall": round(len(true_abst) / len(expected_abst), 3) if expected_abst else None,
            "esperadas": len(expected_abst), "emitidas": len(predicted_abst), "acertadas": len(true_abst),
        },
        "respuestas": {g: graded.count(g) for g in sorted(set(graded))},
        "citasValidas": f"{sum(r['citationsValid'] for r in rag_answers)}/{len(rag_answers)}",
        "latencia": {"mediana": pct(latencies, 0.5), "p95": pct(latencies, 0.95),
                     "medianaRag": pct(rag_lat, 0.5), "p95Rag": pct(rag_lat, 0.95)},
        "topScore": {
            "enCorpus": score_stats([r["topScore"] for r in answerable if r["topScore"] is not None and not r["followUp"]]),
            "fueraDeCorpus": score_stats([r["topScore"] for r in expected_abst if r["topScore"] is not None and not r["followUp"]]),
        },
    }


def score_stats(values):
    if not values:
        return None
    return {"min": min(values), "mediana": round(statistics.median(values), 4), "max": max(values), "n": len(values)}


if __name__ == "__main__":
    sys.exit(main())
