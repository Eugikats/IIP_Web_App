export const formatPrice = (amount: number) => Number(amount).toLocaleString("en-US");

export const todayLabel = (iso?: string) =>
  new Date(iso ?? Date.now()).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });

export const timeAgo = (iso: string): string => {
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 90) return "Just now";
  if (seconds < 3600) return `${Math.round(seconds / 60)} min ago`;
  if (seconds < 86400) return `${Math.round(seconds / 3600)} h ago`;
  if (seconds < 172800) return "Yesterday";
  return todayLabel(iso);
};
