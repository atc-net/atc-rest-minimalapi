/**
 * The RFC 9457 ValidationProblem body returned by Atc.Rest.MinimalApi's ValidationFilter (HTTP 400).
 * By default the keys in `errors` are the names the client sent (ValidationKeyStyle.WireNames),
 * e.g. "firstName" or "workAddress.country.alpha2Code", so they map straight onto form fields.
 */
export interface ValidationProblem {
  type?: string;
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export type FieldErrors = Record<string, string[]>;

/**
 * Collection items are keyed like "lines[2].sku". Libraries that expect dot notation
 * (e.g. react-hook-form: "lines.2.sku") can convert the key with this helper.
 */
export const toDotNotation = (key: string): string => key.replace(/\[(\d+)\]/g, '.$1');

/** Sets a value in a nested object from a wire path such as "workAddress.country.alpha2Code". */
export function setPath(target: Record<string, unknown>, path: string, value: unknown): void {
  const segments = path.split('.');
  let current = target;
  for (const segment of segments.slice(0, -1)) {
    current[segment] ??= {};
    current = current[segment] as Record<string, unknown>;
  }
  current[segments[segments.length - 1]] = value;
}
