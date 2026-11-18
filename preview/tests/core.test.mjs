// Tests for the web's port of Margin.Core (js/core.js). They check the same rules as the C# tests in
// Assets/Tests/EditMode and tests-dotnet, so the web and the Unity app behave the same.
// Run from the repo root: node --test preview/tests/*.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

// core.js is a plain browser script: run it in a sandbox and pull out the objects under test.
const src = readFileSync(new URL("../js/core.js", import.meta.url), "utf8");
const ctx = vm.createContext({ console });
vm.runInContext(`${src}
;globalThis.M = { D, S, HabitEngine, ReadingService, CheckInError, NextBook, Catalog, ReminderPlanner, newState };`, ctx);
const { D, HabitEngine, ReadingService, CheckInError, NextBook, Catalog, ReminderPlanner, newState } = ctx.M;

const book = (id, pages, tags, extra = {}) => ({ id, title: id, author: "A " + id, pages, year: 1950, tags, moods: [], shelves: [],
  chapters: [{ number: 1, title: "One", endPage: Math.floor(pages / 2) }, { number: 2, title: "Two", endPage: pages }], ...extra });
const catalog = () => ({ shelves: [], books: [book("seed", 200, ["dystopia"]), book("dys", 120, ["dystopia"]), book("love", 300, ["love"])] });

test("momentum: a missed reading day drops it by 15 %, it does not reset", () => {
  assert.equal(HabitEngine.step(0.8, 0, 20, true, true), 0.85 * 0.8);
});

test("momentum: a rest day leaves it as it was", () => {
  assert.equal(HabitEngine.step(0.8, 0, 20, false, true), 0.8);
});

test("momentum: one huge day counts at most 1.5 goals", () => {
  assert.ok(Math.abs(HabitEngine.step(0, 1000, 10, true, true) - 0.15 * HabitEngine.DailyCap) < 1e-9);
});

test("check-in: rejects a page behind or past the end of the book", () => {
  Catalog.data = catalog();
  const s = newState(); s.profile.currentBookId = "love"; s.progress.push({ bookId: "love", currentPage: 50, finished: false });
  assert.equal(ReadingService.validate(s, 40), CheckInError.PageNotAhead);
  assert.equal(ReadingService.validate(s, 301), CheckInError.PageBeyondBook);
  assert.equal(ReadingService.validate(s, 80), CheckInError.None);
});

test("next book: never suggests a book already read or the current one", () => {
  const s = newState(); s.profile.currentBookId = "dys"; s.want = []; s.dnf = [];
  s.progress.push({ bookId: "seed", currentPage: 200, finished: true, rating: 5 });
  const ids = NextBook.recommend(s, {}, catalog()).map(r => r.book.id);
  assert.deepEqual([...ids], ["love"]); // copy: arrays from the sandbox belong to another realm
});

test("next book: everything read gives an empty list (the screen shows 'no matches left')", () => {
  const s = newState(); s.want = []; s.dnf = [];
  for (const b of catalog().books) s.progress.push({ bookId: b.id, currentPage: b.pages, finished: true, rating: 4 });
  assert.deepEqual([...NextBook.recommend(s, {}, catalog())], []);
});

test("reminders: never more than the weekly budget", () => {
  const s = newState(); s.profile.goals.readingDays = [0, 1, 2, 3, 4, 5, 6];
  const plan = ReminderPlanner.planNext7Days(s, D.parse("2026-11-16"));
  assert.ok(plan.length <= ReminderPlanner.WeeklyBudget, `${plan.length} reminders`);
});
