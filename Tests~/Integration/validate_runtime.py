#!/usr/bin/env python3
import argparse
import json
from pathlib import Path


RESULT_PREFIX = "APPSTACK_RUNTIME_RESULT:"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-log", required=True)
    parser.add_argument("--requests-file", required=True)
    args = parser.parse_args()

    runtime_log = Path(args.runtime_log).read_text(encoding="utf-8", errors="replace")
    result_lines = [line for line in runtime_log.splitlines() if RESULT_PREFIX in line]
    require(result_lines, "runtime log has no terminal validation result")
    result = json.loads(result_lines[-1].split(RESULT_PREFIX, 1)[1].strip())

    require(result.get("appstackIdPresent") is True, "native SDK returned no Appstack ID")
    require(result.get("sdkDisabled") is False, "native SDK reports disabled")
    require(result.get("callbackCount") == 3, "not all attribution callbacks completed")
    require(result.get("successCount") == 3, "attribution callbacks did not all succeed")
    require(result.get("callbacksOnMainThread") is True, "callback left Unity's main thread")
    require(result.get("attributionValidated") is True, "attribution payload was corrupted")
    require(not result.get("errors"), f"runtime callback errors: {result.get('errors')}")

    requests = [
        json.loads(line)
        for line in Path(args.requests_file).read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    require(any(item["path"].split("?", 1)[0].endswith("/config") for item in requests),
            "native SDK did not fetch remote configuration")
    require(any("/attribution/match/" in item["path"] for item in requests),
            "native SDK did not perform attribution matching")

    events = [item["body"] for item in requests
              if item["path"].split("?", 1)[0].endswith("/events") and item.get("body")]
    custom = next((event for event in events
                   if event.get("event_name") == "runtime_validation_custom"), None)
    login = next((event for event in events if event.get("event_name") == "LOGIN"), None)
    require(custom is not None, "custom event never reached the native wire boundary")
    require(login is not None, "standard event never reached the native wire boundary")
    require(custom.get("wrapper_version") == "unity-1.0.0", "wrong wrapper version on event")
    require(custom.get("customer_user_id") == "runtime-validation-user",
            "customer ID was not forwarded")

    parameters = custom.get("custom_parameters") or {}
    require(parameters.get("number") == 42, "numeric custom parameter changed")
    require(parameters.get("unicode") == "café 🚀", "UTF-8 custom parameter changed")
    require(parameters.get("nested") == {"enabled": True, "items": ["one", 2, False]},
            "nested custom parameters changed")

    login_parameters = login.get("custom_parameters") or {}
    require(login_parameters.get("state") == "ready", "standard event string parameter changed")
    require(login_parameters.get("sequence") == 2, "standard event numeric parameter changed")

    print(json.dumps({
        "platform": result.get("platform"),
        "callbacks": result.get("callbackCount"),
        "eventsRecorded": len(events),
        "wrapperVersion": custom.get("wrapper_version"),
    }, sort_keys=True))


if __name__ == "__main__":
    main()
