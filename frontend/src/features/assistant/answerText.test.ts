import { describe, expect, it } from "vitest";
import { citedNumbers, parseBlocks, parseInline } from "./answerText";

describe("parseInline", () => {
  it("turns [n] into citations and keeps the surrounding text", () => {
    expect(parseInline("El proyecto dura 24 meses [2].")).toEqual([
      { kind: "text", text: "El proyecto dura 24 meses " },
      { kind: "cite", n: 2 },
      { kind: "text", text: "." },
    ]);
  });

  it("expands grouped citations like [1, 3]", () => {
    expect(parseInline("Ver [1, 3]").filter((t) => t.kind === "cite")).toEqual([
      { kind: "cite", n: 1 },
      { kind: "cite", n: 3 },
    ]);
  });

  it("leaves non-numeric brackets and [0] as prose", () => {
    expect(parseInline("[sic] y [0]")).toEqual([{ kind: "text", text: "[sic] y [0]" }]);
  });

  it("renders **bold** with citations inside", () => {
    expect(parseInline("**Climatol [4]** homogeniza")).toEqual([
      { kind: "bold", text: "Climatol " },
      { kind: "cite", n: 4 },
      { kind: "text", text: " homogeniza" },
    ]);
  });

  it("renders the text after an unclosed ** as plain text, without the asterisks", () => {
    expect(parseInline("Uno **dos")).toEqual([
      { kind: "text", text: "Uno " },
      { kind: "text", text: "dos" },
    ]);
    expect(parseInline("**a** b **c [2]")).toEqual([
      { kind: "bold", text: "a" },
      { kind: "text", text: " b " },
      { kind: "text", text: "c " },
      { kind: "cite", n: 2 },
    ]);
    expect(parseInline("Fin **")).toEqual([{ kind: "text", text: "Fin " }]);
  });
});

describe("parseBlocks", () => {
  it("splits paragraphs, bullet lists and numbered lists", () => {
    expect(parseBlocks("Intro\nsigue.\n\n- a\n- b\n1. uno\n2) dos\n\nFin")).toEqual([
      { kind: "paragraph", text: "Intro sigue." },
      { kind: "list", ordered: false, items: ["a", "b"] },
      { kind: "list", ordered: true, items: ["uno", "dos"] },
      { kind: "paragraph", text: "Fin" },
    ]);
  });

  it("turns headings into bold paragraphs", () => {
    expect(parseBlocks("## Métodos\nTexto")).toEqual([
      { kind: "paragraph", text: "**Métodos**" },
      { kind: "paragraph", text: "Texto" },
    ]);
  });
});

describe("citedNumbers", () => {
  it("lists each cited source once in order of appearance", () => {
    expect(citedNumbers("a [2] b [1, 2] c [3]")).toEqual([2, 1, 3]);
  });
});
