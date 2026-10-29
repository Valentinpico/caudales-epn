"""Self-check for the waker: python3 deploy/aws/waker/test_handler.py (no AWS access needed)."""

import json
import os
import sys
import types

calls = []
state = {"name": "stopped"}


class FakeEC2:
    def describe_instances(self, InstanceIds):
        return {"Reservations": [{"Instances": [{"State": {"Name": state["name"]}}]}]}

    def start_instances(self, InstanceIds):
        calls.append(("start", InstanceIds[0]))


class FakeCloudWatch:
    def set_alarm_state(self, AlarmName, StateValue, StateReason):
        calls.append(("alarm", AlarmName, StateValue))


sys.modules["boto3"] = types.SimpleNamespace(client=lambda n: FakeEC2() if n == "ec2" else FakeCloudWatch())
os.environ["APPS"] = json.dumps({"caudales": {"name": "Caudales", "instance": "i-1", "url": "https://x", "alarm": "a-1"}})
sys.path.insert(0, os.path.dirname(__file__))
import handler  # noqa: E402

# Stopped box: started, waiting page that refreshes.
r = handler.handler({"rawPath": "/caudales"}, None)
assert r["statusCode"] == 200 and 'http-equiv="refresh"' in r["body"] and calls == [("start", "i-1")]

# Running and healthy: redirect, no start.
calls.clear(); state["name"] = "running"; handler.healthy = lambda url: True
r = handler.handler({"rawPath": "/caudales"}, None)
assert r["statusCode"] == 302 and r["headers"]["Location"] == "https://x" and calls == []

# Running but app not ready yet: waiting page, no start.
handler.healthy = lambda url: False
r = handler.handler({"rawPath": "/caudales"}, None)
assert r["statusCode"] == 200 and calls == []

# Unknown app: 404 listing the known ones.
r = handler.handler({"rawPath": "/nope"}, None)
assert r["statusCode"] == 404 and 'href="/caudales"' in r["body"]

# EventBridge start event resets the idle alarm; other instances are ignored.
handler.handler({"source": "aws.ec2", "detail": {"instance-id": "i-1"}}, None)
handler.handler({"source": "aws.ec2", "detail": {"instance-id": "i-other"}}, None)
assert calls == [("alarm", "a-1", "OK")]

print("waker ok")
