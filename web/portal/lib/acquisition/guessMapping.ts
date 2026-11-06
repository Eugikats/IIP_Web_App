// Guesses which column of an uploaded customer list holds each thing IIP needs. The guess only pre-fills
// the dropdowns on the import page; a person can change every one before anything is imported.
//
// The person's name is the field that goes wrong most often. Bank lists usually start with a customer ID
// column, so a loose "contains customer" test picks the ID and every prospect is then saved under a number.
// Names are therefore matched in order of confidence, a column that looks like an identifier is never
// taken as a name, and separate First name and Last name columns are joined.

export type ImportFieldKey = "name" | "phone" | "occupation" | "area" | "branch" | "bank_products" | "consent";

export const IMPORT_FIELDS: readonly { key: ImportFieldKey; label: string; required: boolean; hints: readonly string[] }[] = [
  { key: "name", label: "Full name", required: true, hints: [] },
  { key: "phone", label: "Phone number", required: true, hints: ["phone", "mobile", "msisdn", "contact", "tel"] },
  { key: "occupation", label: "Work", required: false, hints: ["occupation", "work", "job", "profession"] },
  { key: "area", label: "Area", required: false, hints: ["area", "town", "location", "district"] },
  { key: "branch", label: "Bank branch", required: false, hints: ["branch"] },
  { key: "bank_products", label: "Products held at the bank", required: false, hints: ["product", "account type", "accounts", "loan"] },
  { key: "consent", label: "Agreed to be contacted", required: false, hints: ["consent", "opt", "agree", "marketing"] },
];

/** The key under which a last-name column is kept when the name comes from separate first and last columns. */
export const LAST_NAME_KEY = "name_last";

export type ColumnMapping = Partial<Record<ImportFieldKey | typeof LAST_NAME_KEY, number>>;

const norm = (heading: string) => heading.toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
const words = (heading: string) => norm(heading).split(" ").filter(Boolean);

/** A heading that names a number or code rather than a person, such as "Customer ID" or "Account No". */
function looksLikeIdentifier(heading: string): boolean {
  return words(heading).some((word) => ["id", "no", "number", "ref", "reference", "code", "nin", "cif"].includes(word));
}

const FULL_NAME = new Set(["name", "full name", "fullname", "names", "customer name", "client name", "account name", "member name"]);
const FIRST_NAME = new Set(["first name", "firstname", "given name", "forename"]);
const LAST_NAME = new Set(["last name", "lastname", "surname", "family name"]);

function findNameColumns(headings: string[]): { name?: number; last?: number } {
  const exact = (set: Set<string>) => headings.findIndex((heading) => set.has(norm(heading)));

  const full = exact(FULL_NAME);
  if (full >= 0) return { name: full };

  const first = exact(FIRST_NAME);
  const last = exact(LAST_NAME);
  if (first >= 0 && last >= 0) return { name: first, last };
  if (first >= 0) return { name: first };

  // A heading that contains "name" and is not an identifier, a company, a branch or part of a name pair.
  const loose = headings.findIndex((heading) => {
    const text = norm(heading);
    if (!/\bname\b/.test(text) || looksLikeIdentifier(heading)) return false;
    return !/\b(user|company|business|branch|product|bank|first|last|middle|father|mother|next|kin|spouse|employer)\b/.test(text);
  });
  if (loose >= 0) return { name: loose };

  // A bare "customer" or "client" heading is a name only when it is not an identifier.
  const bare = headings.findIndex((heading) => ["customer", "client", "member"].includes(norm(heading)));
  if (bare >= 0) return { name: bare };

  return {};
}

export function guessMapping(headings: string[]): ColumnMapping {
  const mapping: ColumnMapping = {};
  const used = new Set<number>();

  const { name, last } = findNameColumns(headings);
  if (name !== undefined) {
    mapping.name = name;
    used.add(name);
    if (last !== undefined) {
      mapping[LAST_NAME_KEY] = last;
      used.add(last);
    }
  }

  for (const field of IMPORT_FIELDS) {
    if (field.key === "name") continue;
    const at = headings.findIndex((heading, index) => !used.has(index) && field.hints.some((hint) => norm(heading).includes(hint)));
    if (at >= 0) {
      mapping[field.key] = at;
      used.add(at);
    }
  }

  return mapping;
}

/** The person's name for one row: the mapped name column, joined with the last-name column when there is one. */
export function nameOf(row: string[], mapping: ColumnMapping): string {
  const first = mapping.name === undefined ? "" : (row[mapping.name] ?? "").trim();
  const last = mapping[LAST_NAME_KEY] === undefined ? "" : (row[mapping[LAST_NAME_KEY]!] ?? "").trim();
  return [first, last].filter(Boolean).join(" ");
}
