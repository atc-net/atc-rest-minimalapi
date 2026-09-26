import { describe, expect, it } from 'vitest';
import type { User } from '../api/usersApi';
import { createUserSections, editUserSections, toRequestBody, userToEditValues } from './userFields';

const fieldNames = (sections: typeof createUserSections) => sections.flatMap((s) => s.fields.map((f) => f.name));

describe('user form fields', () => {
  it('names every create field by its wire path (the ValidationProblem error key)', () => {
    expect(fieldNames(createUserSections)).toEqual([
      'gender', 'firstName', 'lastName', 'email', 'telephone', 'homePage',
      'homeAddress.streetName', 'homeAddress.streetNumber', 'homeAddress.postalCode', 'homeAddress.cityName',
      'homeAddress.country.name', 'homeAddress.country.alpha2Code', 'homeAddress.country.alpha3Code',
      'workAddress.streetName', 'workAddress.streetNumber', 'workAddress.postalCode', 'workAddress.cityName',
      'workAddress.country.name', 'workAddress.country.alpha2Code', 'workAddress.country.alpha3Code',
    ]);
  });

  it('uses "address" for the edit form, because UpdateUserRequest renames WorkAddress with [JsonPropertyName("address")]', () => {
    expect(fieldNames(editUserSections)).toEqual([
      'gender', 'firstName', 'lastName', 'email',
      'address.streetName', 'address.streetNumber', 'address.postalCode', 'address.cityName',
      'address.country.name', 'address.country.alpha2Code', 'address.country.alpha3Code',
    ]);
  });
});

describe('toRequestBody', () => {
  it('builds the nested JSON body from wire paths and leaves out empty values', () => {
    expect(toRequestBody({ firstName: 'Ada', lastName: '', 'workAddress.cityName': 'Aarhus', 'workAddress.country.alpha2Code': 'DK' }))
      .toEqual({ firstName: 'Ada', workAddress: { cityName: 'Aarhus', country: { alpha2Code: 'DK' } } });
  });
});

describe('userToEditValues', () => {
  it('maps a user to the edit form values (work address -> "address.*"), using empty strings for missing values', () => {
    const user: User = {
      id: '1', gender: 'Female', firstName: 'Ada', lastName: 'Tester', email: 'ada@example.com',
      telephone: null, homePage: null, homeAddress: null,
      workAddress: { streetName: 'Main St', streetNumber: null, postalCode: null, cityName: 'Aarhus', country: { name: null, alpha2Code: 'DK', alpha3Code: null } },
    };

    expect(userToEditValues(user)).toEqual({
      gender: 'Female', firstName: 'Ada', lastName: 'Tester', email: 'ada@example.com',
      'address.streetName': 'Main St', 'address.streetNumber': '', 'address.postalCode': '', 'address.cityName': 'Aarhus',
      'address.country.name': '', 'address.country.alpha2Code': 'DK', 'address.country.alpha3Code': '',
    });
  });
});
