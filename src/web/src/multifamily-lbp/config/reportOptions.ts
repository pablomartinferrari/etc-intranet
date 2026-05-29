export const REPORT_DATA_TYPES = [
  { value: "units", label: "Units" },
  { value: "commonAreas", label: "Common Areas" },
] as const;

export const DATA_TYPE_FILTER_OPTIONS = [
  { value: "", label: "All types" },
  ...REPORT_DATA_TYPES,
] as const;

export const DATA_TYPE_NORMALIZE_OPTIONS = [
  { value: "", label: "Both" },
  ...REPORT_DATA_TYPES,
] as const;

export const RESULT_FILTER_OPTIONS = [
  { value: "", label: "All results" },
  { value: "positive", label: "Positive only" },
  { value: "negative", label: "Negative only" },
] as const;

export const UNIFORM_THRESHOLD_OPTIONS = [
  { value: "20", label: "20 readings" },
  { value: "25", label: "25 readings" },
  { value: "30", label: "30 readings" },
  { value: "35", label: "35 readings" },
  { value: "40", label: "40 readings (default)" },
  { value: "45", label: "45 readings" },
  { value: "50", label: "50 readings" },
  { value: "55", label: "55 readings" },
  { value: "60", label: "60 readings" },
] as const;

/** HUD/EPA statistical sample size — groups with this many or more readings use the Average tab. */
export const STATISTICAL_SAMPLE_SIZE = 40;

/** Sections available when configuring a report (keys match API result JSON). */
export const REPORT_SECTIONS = [
  { key: "allShots", label: "All shots" },
  { key: "averageComponents", label: "Average" },
  { key: "uniformShots", label: "Uniform shots" },
  { key: "nonUniformShots", label: "Non-uniform shots" },
] as const;

/** Report viewer tab keys (match API result JSON). */
export const REPORT_VIEWER_TABS = [
  { key: "allShots", label: "All shots" },
  { key: "averageComponents", label: "Average" },
  { key: "uniformShots", label: "Uniform" },
  { key: "nonUniformShots", label: "Non-uniform" },
] as const;

/** Fixed columns per report section (null = infer from data). */
export const REPORT_SECTION_COLUMNS: Partial<
  Record<(typeof REPORT_VIEWER_TABS)[number]["key"], readonly string[]>
> = {
  averageComponents: [
    "component",
    "substrate",
    "result",
    "positivePercent",
    "negativePercent",
    "totalReadings",
  ],
  uniformShots: ["component", "substrate", "result", "totalReadings"],
  nonUniformShots: [
    "component",
    "substrate",
    "positiveCount",
    "negativeCount",
    "positivePercent",
    "totalReadings",
  ],
};

export function dataTypeLabel(value: string): string {
  return REPORT_DATA_TYPES.find((d) => d.value === value)?.label ?? value;
}

/** Fluent Dropdown `value` is display text — map stored option value to its label. */
export function dropdownDisplayValue(
  options: ReadonlyArray<{ value: string; label: string }>,
  selectedValue: string
): string {
  return options.find((o) => o.value === selectedValue)?.label ?? "";
}

export function uniformThresholdLabel(value: string): string {
  return UNIFORM_THRESHOLD_OPTIONS.find((d) => d.value === value)?.label ?? value;
}
