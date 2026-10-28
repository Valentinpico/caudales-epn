import { describe, expect, it } from "vitest";
import { coverageColor, daysInYear, niceTicks } from "./format";

describe("coverageColor", () => {
  it("leaves empty cells transparent", () => expect(coverageColor(0)).toBe("transparent"));
  it("uses the deepest tone for full coverage", () => expect(coverageColor(1)).toBe("#0B3A53"));
  it("never uses the background tone for partial coverage", () => expect(coverageColor(0.01)).toBe("#B9D3D8"));
});

describe("niceTicks", () => {
  it("covers the range with round steps", () => expect(niceTicks(0, 380)).toEqual([0, 100, 200, 300, 400]));
  it("handles a flat series", () => expect(niceTicks(5, 5)).toEqual([5]));
});

describe("daysInYear", () => {
  it("knows leap years", () => expect([daysInYear(1960), daysInYear(1900), daysInYear(2023)]).toEqual([366, 365, 365]));
});
