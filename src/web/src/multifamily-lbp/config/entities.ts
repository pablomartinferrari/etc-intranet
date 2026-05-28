export const MULTIFAMILY_LBP_SLUG = "multifamily-lbp";

export interface EntityDefinition {
  slug: string;
  displayName: string;
  description: string;
}

export const ENTITY_REGISTRY: EntityDefinition[] = [
  {
    slug: MULTIFAMILY_LBP_SLUG,
    displayName: "Multifamily LBP",
    description: "XRF lead-based paint inspection for multifamily housing",
  },
];

export function isValidEntitySlug(slug: string): boolean {
  return ENTITY_REGISTRY.some((e) => e.slug === slug);
}

export function getEntityDefinition(slug: string): EntityDefinition | undefined {
  return ENTITY_REGISTRY.find((e) => e.slug === slug);
}

export function entityDashboardPath(jobId: string, entitySlug: string): string {
  return `/jobs/${encodeURIComponent(jobId)}/${encodeURIComponent(entitySlug)}`;
}
