# Research bonuses — 0.6.7, 2026-10-25

Raw saved definitions plus source points produce final reports exactly once.
Engine research modifies its engine reliability (+0.02/REL), materials
(-0.01/PROD) and work (-0.02/PROD), including cost/work breakdowns. The same
researched engine is used in standalone catalogs and installed in aircraft.

Aircraft family and type REL/PROD points add before calculating effects. Each
source is 0..10, so combined points may reach 20: +0.40 reliability, materials
multiplier 0.80 and MD multiplier 0.60. Manufacturing optimization affects only
airframe/systems/assembly; a finished stocked motor is never discounted again.
FAST changes only the development quote. Power, weight, drag and fuel burn do
not receive invented research bonuses.

Approval previews rebuild with the same final pipeline and report actual
before/after complete costs, MD, reliability and expected life. Each project's
development quote neutralizes its own physical modifiers before applying its
own development surcharges. Other projects' points are not charged twice.
Re-saving, approving, rebuilding and restoring retain frozen technology dates.

Aircraft life keeps its prior <=100% curve; above 100% its reliability factor
is (1/0.8)^1.5 * (1 + 1.5*(R-1)), linear without a ceiling. Motor life retains
its existing linear rating factor, minimum and 100-hour rounding. Reliability
is a rating, not an event probability: nonnegative failure safeguards and
historical accident floors remain intact.

Save payload version 13 keeps source point entries and embeds family/installed
engine context with aircraft definitions. Restoration builds from raw values,
not old discounted reports. Frozen accepted offers, delivered fleet cohorts,
accounting entries and paid production work are not retrospectively rewritten.
The user explicitly authorized a full player-data reset for this release; tests
still use only in-memory payloads and do not write player saves.
