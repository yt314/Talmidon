export enum Gender {
  Male = 0,
  Female = 1
}

export const GENDER_OPTIONS: { label: string; value: Gender }[] = [
  { label: 'זכר', value: Gender.Male },
  { label: 'נקבה', value: Gender.Female }
];
