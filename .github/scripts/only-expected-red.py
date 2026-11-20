"""Fails unless exactly the expected test of a trx failed and every other test passed.

Usage: only-expected-red.py <trx> <expected red test name>
"""
import sys
import xml.etree.ElementTree as ET

trx, expected_red = sys.argv[1], sys.argv[2]
# "{*}" matches the trx XML namespace without spelling out its URI.
results = ET.parse(trx).getroot().findall(".//{*}UnitTestResult")
outcomes = {r.get("testName"): r.get("outcome") for r in results}
print("\n".join(f"{o:>8}  {n}" for n, o in sorted(outcomes.items())))
red = sorted(n for n, o in outcomes.items() if o != "Passed")
# The red test plus at least one passing test: a run that lost its other tests must not pass.
if red != [expected_red] or len(outcomes) < 2:
    sys.exit(f"::error::Expected exactly {[expected_red]} to fail and all other tests to pass; not passed: {red}")
