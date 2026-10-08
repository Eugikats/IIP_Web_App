// Daily series for the KPI trend lines and the charts. Built only from real timestamps: a day with no
// new records is zero, and a series with no movement at all is drawn as "No trend" by the card.

export const TREND_DAYS = 14;

function startOfDay(date: Date): number {
  const copy = new Date(date);
  copy.setHours(0, 0, 0, 0);
  return copy.getTime();
}

/** How many of the given timestamps fall on each of the last `days` days, oldest first. */
export function dailyCounts(timestamps: (string | null | undefined)[], days = TREND_DAYS): number[] {
  const counts = Array.from({ length: days }, () => 0);
  const today = startOfDay(new Date());
  for (const stamp of timestamps) {
    if (!stamp) continue;
    const parsed = new Date(stamp);
    if (Number.isNaN(parsed.getTime())) continue;
    const age = Math.round((today - startOfDay(parsed)) / 86_400_000);
    if (age >= 0 && age < days) counts[days - 1 - age] += 1;
  }
  return counts;
}

/** Up when the latest half of the series is higher than the earlier half, down when lower, else flat. */
export function trendDirection(series: number[]): "up" | "down" | "flat" {
  if (series.length < 2) return "flat";
  const mid = Math.floor(series.length / 2);
  const earlier = series.slice(0, mid).reduce((sum, value) => sum + value, 0);
  const later = series.slice(mid).reduce((sum, value) => sum + value, 0);
  return later > earlier ? "up" : later < earlier ? "down" : "flat";
}

/** Day labels for the last `days` days, oldest first, for chart axes. */
export function dayLabels(days = TREND_DAYS): string[] {
  const today = startOfDay(new Date());
  return Array.from({ length: days }, (_, index) => {
    const date = new Date(today - (days - 1 - index) * 86_400_000);
    return date.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
  });
}
