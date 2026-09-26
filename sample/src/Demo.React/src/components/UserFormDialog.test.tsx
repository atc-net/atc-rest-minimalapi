// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createUserSections, emptyValues } from '../forms/userFields';
import { UserFormDialog } from './UserFormDialog';

afterEach(cleanup);

const input = (name: string) => document.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;

function renderDialog(errors: Record<string, string[]>, onSubmit = vi.fn()) {
  render(
    <UserFormDialog
      open
      title="Create User"
      submitLabel="Create"
      sections={createUserSections}
      values={emptyValues(createUserSections)}
      errors={errors}
      saving={false}
      onChange={vi.fn()}
      onSubmit={onSubmit}
      onClose={vi.fn()}
    />,
  );
}

describe('UserFormDialog', () => {
  it('renders one input per field, named by its wire path', () => {
    renderDialog({});

    for (const field of createUserSections.flatMap((s) => s.fields)) {
      expect(input(field.name), field.name).not.toBeNull();
    }
  });

  it('shows each server error as the helper text of the field whose name equals the error key', () => {
    renderDialog({ 'workAddress.cityName': ['WorkAddress.CityName must be min 3 characters long.'] });

    const city = input('workAddress.cityName');
    expect(city.getAttribute('aria-invalid')).toBe('true');
    const helperText = document.getElementById(city.getAttribute('aria-describedby')!);
    expect(helperText?.textContent).toBe('WorkAddress.CityName must be min 3 characters long.');

    // The same label ("City") exists in the home address, which has no error
    expect(input('homeAddress.cityName').getAttribute('aria-invalid')).toBe('false');
  });

  it('shows several messages for the same field one per line (one element each, no joined string)', () => {
    renderDialog({ lastName: ['The LastName field is required.', "'Last Name' must not be empty."] });

    const helperText = document.getElementById(input('lastName').getAttribute('aria-describedby')!)!;
    const lines = [...helperText.querySelectorAll('[data-error-line]')].map((line) => line.textContent);
    expect(lines).toEqual(['The LastName field is required.', "'Last Name' must not be empty."]);
  });

  it('marks the gender select as invalid', () => {
    renderDialog({ gender: ['Gender is required.'] });

    expect(screen.getByText('Gender is required.')).toBeTruthy();
    expect(input('gender').closest('.MuiFormControl-root')?.querySelector('.Mui-error')).not.toBeNull();
  });

  it('calls onSubmit when the submit button is clicked', () => {
    const onSubmit = vi.fn();
    renderDialog({}, onSubmit);

    fireEvent.click(screen.getByRole('button', { name: 'Create' }));

    expect(onSubmit).toHaveBeenCalledOnce();
  });
});
