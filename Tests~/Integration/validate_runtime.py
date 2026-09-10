#!/usr/bin/env python3
import argparse
import json
import urllib.parse
from pathlib import Path


RESULT_PREFIX = "APPSTACK_RUNTIME_RESULT:"
LINK_PREFIX = "APPSTACK_RUNTIME_LINK:"
DELIVERED_LINK_PREFIX = "APPSTACK_RUNTIME_DEEPLINK:"
PACKAGE_MANIFEST = Path(__file__).resolve().parents[2] / "package.json"


def expected_wrapper_version() -> str:
    version = json.loads(PACKAGE_MANIFEST.read_text(encoding="utf-8"))["version"]
    return f"unity-{version}"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def collect(log: str, prefix: str) -> list:
    return [
        json.loads(line.split(prefix, 1)[1].strip())
        for line in log.splitlines()
        if prefix in line
    ]


def validate_delivered_link(case: dict, source: str, url: str) -> None:
    """A link the operating system routed to the player, rather than one the test
    handed to the SDK. Its parse must match the URL that was actually delivered."""
    expected_id = urllib.parse.unquote(urllib.parse.urlsplit(url).path.lstrip("/"))
    # Mirror the native parsers rather than using parse_qsl: a repeated name keeps
    # its final value, a missing value becomes empty, and "+" stays a plus sign.
    expected_query = {}
    for item in (urllib.parse.urlsplit(url).query or "").split("&"):
        if not item:
            continue
        name, separator, value = item.partition("=")
        expected_query[urllib.parse.unquote(name)] = (
            urllib.parse.unquote(value) if separator else ""
        )
    expected_params = sorted(
        f"{name}={value}" for name, value in expected_query.items()
    )

    require(not case.get("error"), f"{source} link threw {case.get('error')}")
    require(case.get("matched") is True,
            f"{source} link was delivered but the SDK rejected it")
    require(case.get("url") == url,
            f"{source} link url was {case.get('url')!r}, expected {url!r}")
    require(case.get("deeplinkId") == expected_id,
            f"{source} link deeplinkId was {case.get('deeplinkId')!r}, "
            f"expected {expected_id!r}")
    require((case.get("queryParams") or []) == expected_params,
            f"{source} link query params were {case.get('queryParams')}, "
            f"expected {expected_params}")


# Native link parsing is a pure, offline function of the tapped URL, so both
# platforms must produce exactly this result through the production bridge.
# "url" is the URL handed to the SDK and must survive the round trip unchanged.
UNIVERSAL_LINK_EXPECTATIONS = {
    "standard": {
        "matched": True,
        "deeplinkId": "abc123",
        "url": "https://links.example.com/abc123"
               "?deep_link_path=%2Fshop%2F42&utf8=caf%C3%A9%20%F0%9F%9A%80&flag&empty=",
        "queryParams": [
            "deep_link_path=/shop/42",
            "empty=",
            "flag=",
            "utf8=café 🚀",
        ],
    },
    "anyHostAllowed": {
        "matched": True,
        "deeplinkId": "xyz789",
        "url": "https://links.example.com/xyz789?a=1",
        "queryParams": ["a=1"],
    },
    "hostCaseInsensitive": {
        "matched": True,
        "deeplinkId": "abc123",
        "url": "https://LINKS.EXAMPLE.COM/abc123",
        "queryParams": [],
    },
    "encodedIdDecoded": {
        "matched": True,
        "deeplinkId": "abc 123",
        "url": "https://links.example.com/abc%20123",
        "queryParams": [],
    },
    "repeatedParamLastWins": {
        "matched": True,
        "deeplinkId": "abc123",
        "url": "https://links.example.com/abc123?k=1&k=2",
        "queryParams": ["k=2"],
    },
    "hostNotAllowed": {"matched": False},
    "sharedHostRejected": {"matched": False},
    "devSharedHostRejected": {"matched": False},
    "twoPathSegments": {"matched": False},
    "noPathSegment": {"matched": False},
    "plainHttpRejected": {"matched": False},
    "emptyAllowedHostsRejects": {"matched": False},
}


def validate_universal_links(cases: list) -> None:
    by_name = {case.get("name"): case for case in cases}
    require(
        set(by_name) == set(UNIVERSAL_LINK_EXPECTATIONS),
        f"universal link cases were {sorted(by_name)}, "
        f"expected {sorted(UNIVERSAL_LINK_EXPECTATIONS)}",
    )

    for name, expected in UNIVERSAL_LINK_EXPECTATIONS.items():
        case = by_name[name]
        require(not case.get("error"), f"universal link case {name} threw {case.get('error')}")
        require(
            case.get("matched") is expected["matched"],
            f"universal link case {name} matched={case.get('matched')}, "
            f"expected {expected['matched']}",
        )
        if not expected["matched"]:
            continue
        require(
            case.get("deeplinkId") == expected["deeplinkId"],
            f"universal link case {name} deeplinkId was {case.get('deeplinkId')!r}, "
            f"expected {expected['deeplinkId']!r}",
        )
        require(
            case.get("url") == expected["url"],
            f"universal link case {name} url was {case.get('url')!r}, "
            f"expected {expected['url']!r}",
        )
        require(
            (case.get("queryParams") or []) == expected["queryParams"],
            f"universal link case {name} query params were {case.get('queryParams')}, "
            f"expected {expected['queryParams']}",
        )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-log", required=True)
    parser.add_argument("--requests-file", required=True)
    parser.add_argument("--delivered-link-log")
    parser.add_argument("--delivered-link-url")
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
    link_cases = collect(runtime_log, LINK_PREFIX)
    validate_universal_links(link_cases)

    delivered = []
    if args.delivered_link_url:
        url = args.delivered_link_url
        warm = collect(runtime_log, DELIVERED_LINK_PREFIX)
        warm_case = next((case for case in warm
                          if case.get("name") == "deepLinkActivated"), None)
        require(warm_case is not None,
                "no link reached the running player through deepLinkActivated")
        validate_delivered_link(warm_case, "deepLinkActivated", url)
        delivered.append("deepLinkActivated")

        cold_log = Path(args.delivered_link_log).read_text(
            encoding="utf-8", errors="replace")
        cold = collect(cold_log, DELIVERED_LINK_PREFIX)
        cold_case = next((case for case in cold
                          if case.get("name") == "absoluteURL"), None)
        require(cold_case is not None,
                "a link-launched cold start reported no Application.absoluteURL")
        validate_delivered_link(cold_case, "absoluteURL", url)
        delivered.append("absoluteURL")

    requests = [
        json.loads(line)
        for line in Path(args.requests_file).read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    config_requests = [item for item in requests
                       if item["path"].split("?", 1)[0].endswith("/config")]
    require(len(config_requests) == 1,
            f"native SDK configuration count was {len(config_requests)}, expected exactly one")
    require(any("/attribution/match/" in item["path"] for item in requests),
            "native SDK did not perform attribution matching")

    events = [item["body"] for item in requests
              if item["path"].split("?", 1)[0].endswith("/events") and item.get("body")]
    custom = next((event for event in events
                   if event.get("event_name") == "runtime_validation_custom"), None)
    login = next((event for event in events if event.get("event_name") == "LOGIN"), None)
    require(custom is not None, "custom event never reached the native wire boundary")
    require(login is not None, "standard event never reached the native wire boundary")
    wrapper_version = expected_wrapper_version()
    require(custom.get("wrapper_version") == wrapper_version,
            f"wrong wrapper version on event, expected {wrapper_version}")
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
        "universalLinkCases": len(link_cases),
        "deliveredLinks": delivered,
        "wrapperVersion": custom.get("wrapper_version"),
    }, sort_keys=True))


if __name__ == "__main__":
    main()
