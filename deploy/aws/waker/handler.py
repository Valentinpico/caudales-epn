"""Waker for the on-demand demo boxes.

HTTP (Lambda Function URL), GET /<app>: if the box is up and healthy, redirect to it; if it is
stopped, start it and show a page that reloads until it is ready.

EventBridge (EC2 state change to "running"): reset the box's idle alarm to OK. CloudWatch alarm
actions fire only on a state transition, so a box started while its alarm sits in ALARM would
otherwise never be stopped again.
"""

import html
import json
import os
import urllib.request

import boto3

APPS = json.loads(os.environ["APPS"])  # {"caudales": {"name", "instance", "url", "alarm"}}
ec2 = boto3.client("ec2")
cloudwatch = boto3.client("cloudwatch")


def handler(event, _context):
    if event.get("source") == "aws.ec2":
        instance = event["detail"]["instance-id"]
        for app in APPS.values():
            if app["instance"] == instance:
                cloudwatch.set_alarm_state(AlarmName=app["alarm"], StateValue="OK", StateReason="instance started")
        return None

    key = event.get("rawPath", "/").strip("/").lower()
    app = APPS.get(key)
    if app is None:
        links = "".join(f'<li><a href="/{k}">{html.escape(a["name"])}</a></li>' for k, a in APPS.items())
        return page(404, "Aplicación no encontrada", f"<p>Aplicaciones disponibles:</p><ul>{links}</ul>", refresh=False)

    state = ec2.describe_instances(InstanceIds=[app["instance"]])["Reservations"][0]["Instances"][0]["State"]["Name"]
    if state == "running" and healthy(app["url"]):
        return {"statusCode": 302, "headers": {"Location": app["url"], "Cache-Control": "no-store"}}
    if state == "stopped":
        ec2.start_instances(InstanceIds=[app["instance"]])
        state = "pending"

    message = {
        "pending": "El servidor se está encendiendo.",
        "running": "El servidor está encendido; la aplicación está terminando de iniciar.",
        "stopping": "El servidor se estaba apagando por inactividad; se encenderá en cuanto termine.",
    }.get(state, "El servidor se está preparando.")
    body = (
        f"<p>{message}</p>"
        "<p>Suele tardar entre 2 y 5 minutos. Esta página se actualiza sola y te llevará a la "
        "aplicación cuando esté lista.</p>"
        "<p class=note>Para ahorrar costos, el servidor se apaga tras unos minutos sin uso.</p>"
    )
    return page(200, html.escape(app["name"]), body, refresh=True)


def healthy(url):
    try:
        with urllib.request.urlopen(url.rstrip("/") + "/health", timeout=3) as r:
            return r.status == 200
    except Exception:
        return False


def page(status, title, body, refresh):
    meta = '<meta http-equiv="refresh" content="10">' if refresh else ""
    doc = f"""<!doctype html><html lang="es"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">{meta}<title>{title}</title>
<style>
body{{font-family:system-ui,sans-serif;background:#f4f6f5;color:#14232e;display:grid;place-items:center;min-height:100vh;margin:0;padding:16px}}
main{{max-width:34rem;background:#fff;border:1px solid #d5dcde;border-radius:4px;padding:28px}}
h1{{font-size:1.4rem;margin:0 0 12px}}.note{{color:#5b6b72;font-size:.9rem}}a{{color:#1f6f8b}}
</style></head><body><main role="status" aria-live="polite"><h1>{title}</h1>{body}</main></body></html>"""
    return {"statusCode": status, "headers": {"Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store"}, "body": doc}
