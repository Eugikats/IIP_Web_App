"use client";
// Ported from the CIP web app (apps/web/components/dashboard/ui) so IIP and CIP share one look.
// Only the import of `cn` and a trend check that refuses to draw a flat line were changed.
import { useId } from "react";
import { cn } from "@/lib/utils";

export type PremiumSparklineTrend = "positive" | "negative" | "neutral";

type PremiumSparklineProps = {
  data: number[];
  trend: PremiumSparklineTrend;
  width?: number | string;
  height?: number;
  ariaLabel: string;
  className?: string;
  showEndpoint?: boolean;
  showArea?: boolean;
};

const palette = {
  positive: {
    stroke: "#059669",
    fill: "rgba(5, 150, 105, 0.14)",
    dot: "#047857",
  },
  negative: {
    stroke: "#EA580C",
    fill: "rgba(234, 88, 12, 0.12)",
    dot: "#C2410C",
  },
  neutral: {
    stroke: "#4F46E5",
    fill: "rgba(79, 70, 229, 0.12)",
    dot: "#4338CA",
  },
};

function buildSmoothPath(points: Array<{ x: number; y: number }>) {
  if (points.length === 0) return "";
  if (points.length === 1) return `M ${points[0].x} ${points[0].y}`;

  return points.reduce((path, point, index, allPoints) => {
    if (index === 0) return `M ${point.x} ${point.y}`;

    const previous = allPoints[index - 1];
    const controlX = previous.x + (point.x - previous.x) * 0.5;
    return `${path} C ${controlX} ${previous.y}, ${controlX} ${point.y}, ${point.x} ${point.y}`;
  }, "");
}

export default function PremiumSparkline({
  data,
  trend,
  width = 72,
  height = 28,
  ariaLabel,
  className,
  showEndpoint = true,
  showArea = true,
}: PremiumSparklineProps) {
  const gradientId = useId().replace(/:/g, "");
  const colors = palette[trend];
  const viewBoxWidth = typeof width === "number" ? width : 96;
  const values = data.length > 0 ? data : [0];
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;
  const padding = 3.5;
  const points = values.map((value, index) => {
    const denominator = Math.max(values.length - 1, 1);
    const x = padding + (index / denominator) * (viewBoxWidth - padding * 2);
    const y = height - padding - ((value - min) / range) * (height - padding * 2);
    return { x, y };
  });
  const linePath = buildSmoothPath(points);
  const first = points[0];
  const last = points[points.length - 1];
  const areaPath = `${linePath} L ${last.x} ${height - padding} L ${first.x} ${height - padding} Z`;

  return (
    <svg
      width={width}
      height={height}
      viewBox={`0 0 ${viewBoxWidth} ${height}`}
      preserveAspectRatio="none"
      className={cn("block shrink-0 overflow-visible", className)}
      role="img"
      aria-label={ariaLabel}
    >
      <defs>
        <linearGradient id={`${gradientId}-area`} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor={colors.fill} />
          <stop offset="100%" stopColor="rgba(255,255,255,0)" />
        </linearGradient>
      </defs>
      {showArea ? <path d={areaPath} fill={`url(#${gradientId}-area)`} /> : null}
      <path
        d={linePath}
        fill="none"
        stroke={colors.stroke}
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.6"
        vectorEffect="non-scaling-stroke"
      />
      {showEndpoint ? (
        <circle
          cx={last.x}
          cy={last.y}
          r="2.2"
          fill={colors.dot}
          stroke="white"
          strokeWidth="1.3"
          vectorEffect="non-scaling-stroke"
        />
      ) : null}
    </svg>
  );
}
