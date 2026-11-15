using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EasyWorshipControllerMvp
{
    // One scripture announced for a sermon section.
    internal sealed class PlannedScripture
    {
        public string Book;
        public int Chapter;
        public int Verse;
        public int VerseEnd;
        public int Order;
        public bool Presented;
        public string ReferenceText
        {
            get { return Book + " " + Chapter + ":" + (VerseEnd > Verse ? Verse + "-" + VerseEnd : Verse.ToString()); }
        }
    }

    // The ordered set of scriptures announced in a sermon section, and the cursor
    // within it. This is the "scripture plan": a preacher who says "we'd be
    // reading three scriptures, Psalms 1:3, Hebrews 10:2 and Deuteronomy 28:1,
    // let's start from Hebrews 10:2" has declared three and activated one, and
    // later refers back to them by ordinal ("the next verse I called in Psalms",
    // "the final scripture").
    //
    // Resolution is deterministic lookup over a set this process built itself, not
    // inference. When an ordinal plus a book constraint does not identify exactly
    // one member, resolution abstains and returns null: "the next one" with two
    // candidates is genuinely ambiguous, and a guess would be worse than silence.
    //
    // This type deliberately depends on nothing else in the application, so it can
    // be compiled and tested on its own.
    internal sealed class ScripturePlan
    {
        private readonly List<PlannedScripture> items = new List<PlannedScripture>();
        private int activeIndex = -1;

        public int Count { get { return items.Count; } }
        public bool Empty { get { return items.Count == 0; } }
        public IList<PlannedScripture> Items { get { return items; } }

        public PlannedScripture Active
        {
            get { return activeIndex >= 0 && activeIndex < items.Count ? items[activeIndex] : null; }
        }
        public int ActiveIndex { get { return activeIndex; } }

        public void Reset() { items.Clear(); activeIndex = -1; }

        // Adds a scripture if it is not already in the plan. A repeated reference
        // keeps its original position and announcement order, so a preacher who
        // re-reads a reference does not consume a second slot.
        public PlannedScripture Add(string book, int chapter, int verse, int verseEnd, bool presented)
        {
            if (string.IsNullOrEmpty(book) || chapter <= 0 || verse <= 0) return null;
            foreach (PlannedScripture existing in items)
            {
                if (string.Equals(existing.Book, book, StringComparison.OrdinalIgnoreCase) &&
                    existing.Chapter == chapter && existing.Verse == verse)
                {
                    if (presented) existing.Presented = true;
                    if (verseEnd > existing.VerseEnd) existing.VerseEnd = verseEnd;
                    return existing;
                }
            }
            PlannedScripture entry = new PlannedScripture();
            entry.Book = book; entry.Chapter = chapter; entry.Verse = verse;
            entry.VerseEnd = verseEnd; entry.Order = items.Count; entry.Presented = presented;
            items.Add(entry);
            return entry;
        }

        // Marks a member active by reference. Used when a scripture is announced
        // as the one to start from.
        public PlannedScripture Activate(string book, int chapter, int verse)
        {
            for (int i = 0; i < items.Count; i++)
            {
                PlannedScripture item = items[i];
                if (string.Equals(item.Book, book, StringComparison.OrdinalIgnoreCase) &&
                    item.Chapter == chapter && item.Verse == verse)
                { activeIndex = i; return item; }
            }
            return null;
        }

        public void MarkPresented(string book, int chapter, int verse)
        {
            foreach (PlannedScripture item in items)
                if (string.Equals(item.Book, book, StringComparison.OrdinalIgnoreCase) &&
                    item.Chapter == chapter && item.Verse == verse)
                    item.Presented = true;
        }

        // Resolves a spoken ordinal, optionally constrained to a book. Returns null
        // when the reference is ambiguous, out of range, or already presented.
        public PlannedScripture Resolve(PlanOrdinal ordinal, string bookConstraint)
        {
            if (items.Count == 0) return null;

            List<PlannedScripture> candidates = new List<PlannedScripture>();
            foreach (PlannedScripture item in items)
                if (string.IsNullOrEmpty(bookConstraint) ||
                    string.Equals(item.Book, bookConstraint, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(item);
            if (candidates.Count == 0) return null;

            // A book constraint only helps when it narrows the plan to one member.
            // "The next one in Psalms" with two Psalms announced is genuinely
            // ambiguous, and a guess is worse than silence, so abstain instead of
            // advancing through the filtered set. Ordinals that count from the
            // whole plan (first/second/third/last) are exempt: they name a
            // position, so a book constraint is a check on the answer rather than
            // a narrowing of the field.
            bool narrowing = !string.IsNullOrEmpty(bookConstraint) &&
                (ordinal == PlanOrdinal.Next || ordinal == PlanOrdinal.Previous);
            if (narrowing && candidates.Count != 1) return null;

            switch (ordinal)
            {
                case PlanOrdinal.First:
                    // "the first one" means the first announced, not the first unread.
                    return candidates[0];
                case PlanOrdinal.Second:
                    return candidates.Count >= 2 ? candidates[1] : null;
                case PlanOrdinal.Third:
                    return candidates.Count >= 3 ? candidates[2] : null;
                case PlanOrdinal.Last:
                    return candidates[candidates.Count - 1];
                case PlanOrdinal.Next:
                {
                    // "the next one" advances from the active member within the
                    // filtered set. With no active member it is the first unread.
                    PlannedScripture active = Active;
                    if (active != null)
                    {
                        int at = candidates.IndexOf(active);
                        if (at >= 0) return at + 1 < candidates.Count ? candidates[at + 1] : null;
                    }
                    foreach (PlannedScripture item in candidates)
                        if (!item.Presented) return item;
                    return null;
                }
                case PlanOrdinal.Previous:
                {
                    PlannedScripture active = Active;
                    if (active == null) return null;
                    int at = candidates.IndexOf(active);
                    return at > 0 ? candidates[at - 1] : null;
                }
                default:
                    return null;
            }
        }

        // A short operator-facing summary: "3 announced · Ps 1:3 · Heb 10:2 (live) · Dt 28:1"
        public string Summary()
        {
            if (items.Count == 0) return string.Empty;
            List<string> parts = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                PlannedScripture item = items[i];
                string text = Abbreviate(item.Book) + " " + item.Chapter + ":" + item.Verse;
                if (item.Presented) text += " (live)";
                else if (i == activeIndex) text += " (next)";
                parts.Add(text);
            }
            return items.Count + " announced · " + string.Join(" · ", parts.ToArray());
        }

        private static string Abbreviate(string book)
        {
            if (string.IsNullOrEmpty(book)) return string.Empty;
            if (book.Length <= 4) return book;
            return book.Substring(0, 3) + ".";
        }
    }

    internal enum PlanOrdinal { None = 0, First, Second, Third, Next, Previous, Last }

    // Why a plan was cleared, so the operator can be told rather than left
    // guessing when a deictic reference suddenly stops resolving.
    internal enum PlanBoundary { None = 0, NewSection, Silence, Capacity, Manual }

    // The lifetime rules for a plan. A plan must outlive the short parser contexts
    // (6s book, 30s chapter/verse) because it spans a sermon section, but it must
    // not survive into the next one, where "the next one I called" would silently
    // resolve against a stale set. Every expiry is explicit and reported.
    internal static class PlanLifecycle
    {
        // Total silence after which the section is treated as over. Deliberately
        // generous: a pastoral pause, a prayer or a long illustration can run
        // minutes, and expiring during one would drop a valid plan.
        public const double SilenceSeconds = 300;

        // A runaway plan is a bug, not a sermon. Also bounds the operator-facing
        // strip to something readable.
        public const int MaximumMembers = 20;

        // A newly declared count ("we're reading two scriptures") is an explicit
        // announcement of a new set, so it starts one. Detected for free, since
        // the same wording the plan already parses carries it.
        public static bool StartsNewSection(int declaredCount, bool alreadyHasMembers)
        {
            return declaredCount > 0 && alreadyHasMembers;
        }

        public static bool ExceedsCapacity(int members) { return members > MaximumMembers; }

        public static bool Expired(double secondsSinceLastSpeech) { return secondsSinceLastSpeech >= SilenceSeconds; }
    }


    // Recognizes the spoken forms the plan depends on. Kept separate from the plan
    // itself so the wording can be tested without any state.
    internal static class PlanDirectives
    {
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"one",1},{"two",2},{"three",3},{"four",4},{"five",5},{"six",6},{"seven",7},{"eight",8},{"nine",9},{"ten",10},
            {"a couple of",2},{"a few",3}
        };

        // "we'd be reading three scriptures" -> 3. Zero when no count is announced.
        public static int DeclaredCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            Match match = Regex.Match(text,
                @"\b(?:reading|looking at|studying|considering|examining|using)\s+(?:about\s+)?(\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|a couple of|a few)\s+(?:scriptures?|passages?|verses?|texts?|portions?)\b",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                // Also accept a bare count before the noun: "three scriptures today".
                match = Regex.Match(text,
                    @"\b(\d{1,2}|one|two|three|four|five|six|seven|eight|nine|ten|a couple of|a few)\s+(?:scriptures?|passages?|texts?|portions?)\b",
                    RegexOptions.IgnoreCase);
                if (!match.Success) return 0;
            }
            string token = match.Groups[1].Value.Trim().ToLowerInvariant();
            int numeric;
            if (int.TryParse(token, out numeric)) return numeric >= 1 && numeric <= 20 ? numeric : 0;
            int worded;
            return counts.TryGetValue(token, out worded) ? worded : 0;
        }

        // "let's start from", "turn to", "go to", "open to", "let's begin with".
        // This distinguishes the one reference being activated from the others
        // merely being announced.
        public static bool IsActivation(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text,
                @"\b(?:let'?s\s+(?:start|begin|read|go|turn|look)\s+(?:from|with|at|to)?|start\s+(?:from|with)|begin\s+(?:from|with)|turn\s+to|go\s+to|open\s+(?:to|at)|let'?s\s+go\s+to|let\s+us\s+(?:start|begin|read|turn))\b",
                RegexOptions.IgnoreCase);
        }

        // Resolves the ordinal and any book constraint in a deictic reference such
        // as "the next verse I called in Psalms" or "the final scripture".
        public static bool TryResolveReference(string text, out PlanOrdinal ordinal, out string book)
        {
            ordinal = PlanOrdinal.None; book = string.Empty;
            if (string.IsNullOrEmpty(text)) return false;

            if (Regex.IsMatch(text, @"\b(?:final|last)\s+(?:scripture|passage|text|one|verse|reading)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.Last;
            else if (Regex.IsMatch(text, @"\b(?:next)\s+(?:scripture|passage|text|one|verse|reading)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.Next;
            else if (Regex.IsMatch(text, @"\b(?:previous|last)\s+verse\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.Previous;
            else if (Regex.IsMatch(text, @"\b(?:first|1st)\s+(?:scripture|passage|text|one|verse|reading)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.First;
            else if (Regex.IsMatch(text, @"\b(?:second|2nd)\s+(?:scripture|passage|text|one|verse|reading)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.Second;
            else if (Regex.IsMatch(text, @"\b(?:third|3rd)\s+(?:scripture|passage|text|one|verse|reading)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.Third;
            else if (Regex.IsMatch(text, @"\bfirst\s+one\s+i\s+(?:called|mentioned|read)\b", RegexOptions.IgnoreCase))
                ordinal = PlanOrdinal.First;
            else return false;

            // "the next verse I called in Psalms" / "the next one in Psalms".
            Match constraint = Regex.Match(text,
                @"\b(?:in|from|of)\s+(?!the\b)([a-z]{3,}(?:\s+[a-z]{3,})?)\s*(?=$|[.,;!?])",
                RegexOptions.IgnoreCase);
            if (constraint.Success) book = constraint.Groups[1].Value.Trim();
            return true;
        }

        // "give me that scripture!", "what was that scripture", "find that verse".
        // A request to surface the passage just quoted, which the operator directs
        // rather than the machine deciding.
        public static bool IsQuotePromotion(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text,
                @"\b(?:give\s+me|show\s+me|bring\s+up|find|pull\s+up|what(?:'s| is)|where(?:'s| is))\s+(?:me\s+)?(?:that|the)\s+(?:scripture|passage|verse|text|one)\b",
                RegexOptions.IgnoreCase);
        }
    }
}
