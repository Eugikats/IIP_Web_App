import GlobalSearch from "@/lovable-pages/GlobalSearch";

export default async function Page({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const { q = "" } = await searchParams;
  return <GlobalSearch initialQuery={q} />;
}
