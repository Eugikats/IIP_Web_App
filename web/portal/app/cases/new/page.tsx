import { NewCaseForm } from "./NewCaseForm";

export default function NewCasePage() {
  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-2xl font-semibold text-ink">New claim intake</h2>
        <p className="mt-1 text-sm text-muted">
          Create a claim case and write the initial audit event.
        </p>
      </div>
      <NewCaseForm />
    </div>
  );
}
