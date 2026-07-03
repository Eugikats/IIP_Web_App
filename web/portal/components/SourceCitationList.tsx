import { EmptyState } from "./States";

export type SourceCitation = {
  chunkId: string;
  documentName: string;
  productLine: string;
  version: string;
  excerpt: string;
  label: string;
};

export function SourceCitationList({ sources }: { sources: SourceCitation[] }) {
  if (sources.length === 0) {
    return <EmptyState title="No policy references" description="Approved source citations will appear after a grounded assistant answer." />;
  }

  return (
    <ul className="space-y-2">
      {sources.map((source) => (
        <li key={source.chunkId} className="rounded-xl border border-line bg-mist p-4 transition hover:-translate-y-0.5 hover:border-accent/50 hover:shadow-glow-cyan text-sm">
          <p className="font-semibold text-ink font-mono">{source.label}</p>
          <p className="mt-2 text-muted leading-6">{source.excerpt}</p>
        </li>
      ))}
    </ul>
  );
}
