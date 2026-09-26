import { describe, expect, it } from 'vitest';
import { setPath, toDotNotation } from './validationProblem';

describe('setPath', () => {
  it('sets a top-level value', () => {
    const body: Record<string, unknown> = {};
    setPath(body, 'firstName', 'Ada');
    expect(body).toEqual({ firstName: 'Ada' });
  });

  it('builds nested objects from a wire path', () => {
    const body: Record<string, unknown> = {};
    setPath(body, 'workAddress.country.alpha2Code', 'DK');
    expect(body).toEqual({ workAddress: { country: { alpha2Code: 'DK' } } });
  });

  it('keeps sibling values in an existing nested object', () => {
    const body: Record<string, unknown> = {};
    setPath(body, 'workAddress.cityName', 'Aarhus');
    setPath(body, 'workAddress.country.alpha2Code', 'DK');
    expect(body).toEqual({ workAddress: { cityName: 'Aarhus', country: { alpha2Code: 'DK' } } });
  });
});

describe('toDotNotation', () => {
  it('converts collection indexers to dot notation (e.g. for react-hook-form)', () => {
    expect(toDotNotation('lines[2].sku')).toBe('lines.2.sku');
  });

  it('converts nested indexers', () => {
    expect(toDotNotation('orders[1].lines[0].sku')).toBe('orders.1.lines.0.sku');
  });

  it('leaves keys without indexers unchanged', () => {
    expect(toDotNotation('workAddress.cityName')).toBe('workAddress.cityName');
  });
});
