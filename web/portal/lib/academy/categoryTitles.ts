// The names the catalogue files courses under. Mirrors the categories the course content uses.
export const CATEGORY_TITLES: Record<string, string> = {
  "ai-foundations": "AI foundations",
  "claims-fraud": "Claims and fraud",
  "underwriting-risk": "Underwriting and risk",
  "sales-experience": "Sales and customer experience",
  "build-govern": "Build and govern",
};

export const categoryTitle = (id: string) => CATEGORY_TITLES[id] ?? "Course";
