"use client";

import React, { useState } from "react";
import { Sparkles, ChevronDown, CheckCircle2, Database } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";

export function PriorityBadge({ priority }: { priority: string }) {
  if (priority === "High") {
    return (
      <span className="rounded-full border border-danger/30 bg-danger/10 px-2.5 py-1 text-xs font-semibold text-danger">
        High Priority
      </span>
    );
  }
  if (priority === "Medium") {
    return (
      <span className="rounded-full border border-warning/30 bg-warning/10 px-2.5 py-1 text-xs font-semibold text-warning">
        Medium Priority
      </span>
    );
  }
  return (
    <span className="rounded-full border border-line bg-surface-soft px-2.5 py-1 text-xs font-semibold text-muted">
      Low Priority
    </span>
  );
}

export function StatusBadge({ status }: { status: string }) {
  if (status === "Connected" || status === "Active") {
    return (
      <span className="flex items-center gap-1.5 rounded-full border border-success/30 bg-success/10 px-2.5 py-1 text-xs font-semibold text-success">
        <span className="h-1.5 w-1.5 rounded-full bg-success" />
        {status}
      </span>
    );
  }
  if (status === "Pending") {
    return (
      <span className="flex items-center gap-1.5 rounded-full border border-warning/30 bg-warning/10 px-2.5 py-1 text-xs font-semibold text-warning">
        <span className="h-1.5 w-1.5 rounded-full bg-warning" />
        {status}
      </span>
    );
  }
  return (
    <span className="flex items-center gap-1.5 rounded-full border border-line bg-surface-soft px-2.5 py-1 text-xs font-semibold text-muted">
      <span className="h-1.5 w-1.5 rounded-full bg-muted-soft" />
      {status}
    </span>
  );
}

export function InfoCard({
  label,
  value,
  helper,
  tone = "neutral",
}: {
  label: string;
  value: string;
  helper?: string;
  tone?: "neutral" | "success" | "danger";
}) {
  return (
    <motion.div
      whileHover={{ y: -4, scale: 1.01 }}
      transition={{ type: "spring", stiffness: 400, damping: 30 }}
      className="rounded-xl border border-line bg-surface p-5 shadow-panel transition-shadow hover:shadow-card"
    >
      <div className="text-sm font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-ink">
        {value}
      </div>
      {helper && (
        <div
          className={`mt-1 text-xs font-medium ${
            tone === "success"
              ? "text-success"
              : tone === "danger"
              ? "text-danger"
              : "text-muted"
          }`}
        >
          {helper}
        </div>
      )}
    </motion.div>
  );
}

export function Card({
  title,
  icon: Icon,
  children,
}: {
  title: string;
  icon?: React.ElementType;
  children: React.ReactNode;
}) {
  return (
    <motion.div
      whileHover={{ y: -2 }}
      transition={{ type: "spring", stiffness: 300, damping: 25 }}
      className="rounded-xl border border-line bg-surface p-5 shadow-panel"
    >
      <div className="mb-4 flex items-center gap-2">
        {Icon && <Icon size={18} className="text-violet" />}
        <h2 className="text-sm font-semibold text-ink">{title}</h2>
      </div>
      {children}
    </motion.div>
  );
}

export function DataRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between border-b border-line pb-2 last:border-0 last:pb-0">
      <span className="text-muted">{label}</span>
      <span className="text-right font-medium text-ink">{value}</span>
    </div>
  );
}

export function ChannelIcon({ type }: { type: string }) {
  return (
    <div className="flex h-10 w-10 items-center justify-center rounded-xl border border-line bg-surface-soft text-muted shadow-panel">
      <span className="text-xs font-bold">{type.charAt(0)}</span>
    </div>
  );
}

export function PromptBar({
  placeholder,
  compact = false,
  onSubmit,
}: {
  placeholder: string;
  compact?: boolean;
  onSubmit?: (text: string) => void;
}) {
  const [value, setValue] = useState("");

  const handleTrigger = () => {
    if (value.trim() && onSubmit) {
      onSubmit(value);
      setValue("");
    }
  };

  return (
    <motion.div
      whileTap={{ scale: 0.99 }}
      className={`flex items-center gap-2 rounded-xl border border-line bg-surface shadow-panel ${
        compact ? "p-1.5" : "p-2"
      }`}
    >
      <button 
        onClick={handleTrigger}
        className="flex h-9 w-9 items-center justify-center rounded-lg bg-surface-soft text-muted transition-colors hover:bg-surface-ai hover:text-violet"
      >
        <Sparkles size={16} />
      </button>
      <input
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            handleTrigger();
          }
        }}
        placeholder={placeholder}
        className="w-full bg-transparent text-sm outline-none placeholder:text-muted-soft focus:text-ink"
      />
    </motion.div>
  );
}

export function AssistantMessage({ children }: { children: React.ReactNode }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ type: "spring", stiffness: 300, damping: 25 }}
      className="mb-6 flex gap-4"
    >
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-gradient text-white shadow-panel">
        <Sparkles size={14} />
      </div>
      <div className="flex-1 text-muted">{children}</div>
    </motion.div>
  );
}

export function UserMessage({ children }: { children: React.ReactNode }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ type: "spring", stiffness: 300, damping: 25 }}
      className="mb-6 flex gap-4"
    >
      <div className="flex-1 text-right text-muted">{children}</div>
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-line bg-surface-soft text-muted shadow-panel">
        U
      </div>
    </motion.div>
  );
}

export function AiContextWindow({
  title,
  contextData,
  grounded = true,
}: {
  title: string;
  contextData: { label: string; value: string }[];
  grounded?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="mb-2 flex flex-col items-center">
      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0, y: 10 }}
            animate={{ opacity: 1, height: "auto", y: 0 }}
            exit={{ opacity: 0, height: 0, y: 10 }}
            transition={{ type: "spring", stiffness: 300, damping: 25 }}
            className="mb-2 w-full overflow-hidden rounded-xl border border-line bg-surface p-4 shadow-panel"
          >
            <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink">
              <Database size={16} className="text-violet" />
              Active Context
            </div>
            <div className="space-y-2 text-sm">
              {contextData.map((data, index) => (
                <div key={index} className="flex justify-between border-b border-line pb-1 last:border-0 last:pb-0">
                  <span className="text-muted">{data.label}</span>
                  <span className="font-medium text-ink text-right">{data.value}</span>
                </div>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <button
        onClick={() => setIsOpen(!isOpen)}
        className="group flex items-center gap-2 rounded-full border border-line bg-surface-soft px-3 py-1 text-xs font-medium text-muted shadow-panel transition-colors hover:bg-surface hover:text-ink"
      >
        {grounded && <CheckCircle2 size={12} className="text-success" />}
        {title}
        <ChevronDown
          size={14}
          className={`text-muted-soft transition-transform duration-300 ${isOpen ? "rotate-180" : ""}`}
        />
      </button>
    </div>
  );
}
