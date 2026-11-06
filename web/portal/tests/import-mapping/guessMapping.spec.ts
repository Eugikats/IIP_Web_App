import { expect, test } from "@playwright/test";
import { guessMapping, LAST_NAME_KEY, nameOf } from "../../lib/acquisition/guessMapping";

// Unit tests for the column guess on the customer list import page. The regression they pin: a bank list
// that starts with a "Customer ID" column used to have that ID taken as the person's name.

test.describe("guessMapping", () => {
  test("a Customer ID column is never taken as the name", () => {
    const headings = ["Customer ID", "First Name", "Last Name", "Phone", "Occupation", "Loan Type", "Insurance Interest", "WhatsApp Opt-in", "National ID"];
    const mapping = guessMapping(headings);

    expect(mapping.name).toBe(1);
    expect(mapping[LAST_NAME_KEY]).toBe(2);
    expect(Object.values(mapping)).not.toContain(0);
    expect(mapping.phone).toBe(3);
    expect(mapping.occupation).toBe(4);
    expect(mapping.bank_products).toBe(5);
    expect(mapping.consent).toBe(7);
  });

  test("a single full-name column wins over everything else", () => {
    const mapping = guessMapping(["Client No", "Full Name", "Mobile", "Branch"]);
    expect(mapping.name).toBe(1);
    expect(mapping[LAST_NAME_KEY]).toBeUndefined();
    expect(mapping.phone).toBe(2);
    expect(mapping.branch).toBe(3);
  });

  test("Customer Name is a name, Customer Number is not", () => {
    expect(guessMapping(["Customer Number", "Customer Name", "Phone"]).name).toBe(1);
  });

  test("a bare Customer heading is a name, but only when it is not an identifier", () => {
    expect(guessMapping(["Customer", "Phone"]).name).toBe(0);
    expect(guessMapping(["Customer ID", "Phone"]).name).toBeUndefined();
  });

  test("company, branch and user names are not the person's name", () => {
    const mapping = guessMapping(["Company Name", "Branch Name", "User Name", "Phone"]);
    expect(mapping.name).toBeUndefined();
    expect(mapping.phone).toBe(3);
  });

  test("an account number is not taken as the products held", () => {
    const mapping = guessMapping(["Name", "Phone", "Account Number", "Account Type"]);
    expect(mapping.bank_products).toBe(3);
  });

  test("a column is used for at most one field", () => {
    const mapping = guessMapping(["Name", "Phone", "Contact"]);
    const used = Object.values(mapping);
    expect(new Set(used).size).toBe(used.length);
  });

  test("nothing recognisable gives an empty guess rather than a wrong one", () => {
    expect(guessMapping(["A", "B", "C"])).toEqual({});
  });
});

test.describe("nameOf", () => {
  test("joins first and last name, skipping blanks and trimming", () => {
    const mapping = guessMapping(["ID", "First Name", "Last Name"]);
    expect(nameOf(["C1", " Grace ", "Nakato"], mapping)).toBe("Grace Nakato");
    expect(nameOf(["C2", "Grace", ""], mapping)).toBe("Grace");
    expect(nameOf(["C3", "", ""], mapping)).toBe("");
  });

  test("uses the single name column when there is no last name", () => {
    expect(nameOf(["C1", "Peter Mugisha"], guessMapping(["ID", "Full Name"]))).toBe("Peter Mugisha");
  });

  test("a missing name column gives an empty name, never the ID", () => {
    expect(nameOf(["C1", "0700000001"], guessMapping(["Customer ID", "Phone"]))).toBe("");
  });
});
