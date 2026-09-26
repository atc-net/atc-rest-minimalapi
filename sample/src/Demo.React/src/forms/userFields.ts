import type { Address, RequestBody, User } from '../api/usersApi';
import { setPath } from '../validationProblem';

/**
 * A form field. Its `name` IS the wire path of the value in the request body, which is also the key of its
 * validation errors in the API's ValidationProblem response, so errors[name] belongs to this field.
 */
export interface FieldDefinition {
  name: string;
  label: string;
  type?: 'text' | 'email' | 'url' | 'tel';
  options?: string[];
}

export interface FieldSection {
  title: string;
  fields: FieldDefinition[];
}

export type FormValues = Record<string, string>;

const genderOptions = ['None', 'NonBinary', 'Male', 'Female'];

const addressFields = (prefix: string): FieldDefinition[] => [
  { name: `${prefix}.streetName`, label: 'Street Name' },
  { name: `${prefix}.streetNumber`, label: 'Street Number' },
  { name: `${prefix}.postalCode`, label: 'Postal Code' },
  { name: `${prefix}.cityName`, label: 'City' },
  { name: `${prefix}.country.name`, label: 'Country Name' },
  { name: `${prefix}.country.alpha2Code`, label: 'Country Code (2 letters)' },
  { name: `${prefix}.country.alpha3Code`, label: 'Country Code (3 letters)' },
];

const personalFields: FieldDefinition[] = [
  { name: 'gender', label: 'Gender', options: genderOptions },
  { name: 'firstName', label: 'First Name' },
  { name: 'lastName', label: 'Last Name' },
  { name: 'email', label: 'Email', type: 'email' },
];

/** CreateUserRequest. */
export const createUserSections: FieldSection[] = [
  {
    title: 'Personal',
    fields: [...personalFields, { name: 'telephone', label: 'Telephone', type: 'tel' }, { name: 'homePage', label: 'Home Page (URL)', type: 'url' }],
  },
  { title: 'Home Address', fields: addressFields('homeAddress') },
  { title: 'Work Address', fields: addressFields('workAddress') },
];

/** UpdateUserRequest: its WorkAddress is sent (and validated) as "address" ([JsonPropertyName("address")]). */
export const editUserSections: FieldSection[] = [
  { title: 'Personal', fields: personalFields },
  { title: 'Address', fields: addressFields('address') },
];

export const emptyValues = (sections: FieldSection[]): FormValues =>
  Object.fromEntries(sections.flatMap((s) => s.fields.map((f) => [f.name, ''])));

/** Builds the nested JSON body from the wire paths; empty values are left out. */
export function toRequestBody(values: FormValues): RequestBody {
  const body: RequestBody = {};
  for (const [path, value] of Object.entries(values)) {
    if (value !== '') {
      setPath(body, path, value);
    }
  }

  return body;
}

/** The edit form values for a user (the work address is edited as "address"). */
export function userToEditValues(user: User): FormValues {
  const address: Partial<Address> = user.workAddress ?? {};
  return {
    gender: user.gender,
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email,
    'address.streetName': address.streetName ?? '',
    'address.streetNumber': address.streetNumber ?? '',
    'address.postalCode': address.postalCode ?? '',
    'address.cityName': address.cityName ?? '',
    'address.country.name': address.country?.name ?? '',
    'address.country.alpha2Code': address.country?.alpha2Code ?? '',
    'address.country.alpha3Code': address.country?.alpha3Code ?? '',
  };
}
