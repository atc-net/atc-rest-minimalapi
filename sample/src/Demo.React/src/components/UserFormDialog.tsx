import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { FieldSection, FormValues } from '../forms/userFields';
import type { FieldErrors } from '../validationProblem';

export interface UserFormDialogProps {
  open: boolean;
  title: string;
  submitLabel: string;
  sections: FieldSection[];
  values: FormValues;
  /** Field errors keyed by wire path, straight from the API's ValidationProblem. */
  errors: FieldErrors;
  saving: boolean;
  onChange: (name: string, value: string) => void;
  onSubmit: () => void;
  onClose: () => void;
}

/**
 * A create/edit dialog. Each input's name is its wire path, so errors[name] is shown as that field's helper text:
 * no mapping between API error keys and form fields is needed.
 */
export function UserFormDialog({ open, title, submitLabel, sections, values, errors, saving, onChange, onSubmit, onClose }: UserFormDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent dividers>
        {sections.map((section) => (
          <Box key={section.title} component="section" sx={{ mb: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 1.5, fontWeight: 600 }}>
              {section.title}
            </Typography>
            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
              {section.fields.map((field) => {
                const fieldErrors = errors[field.name];
                return (
                  <TextField
                    key={field.name}
                    id={`field-${field.name}`}
                    name={field.name}
                    label={field.label}
                    type={field.options ? undefined : (field.type ?? 'text')}
                    select={field.options !== undefined}
                    value={values[field.name] ?? ''}
                    onChange={(event) => onChange(field.name, event.target.value)}
                    error={fieldErrors !== undefined}
                    helperText={fieldErrors?.map((message) => (
                      // One message per line; each is its own element (no HTML strings from the server).
                      <Box key={message} component="span" data-error-line sx={{ display: 'block' }}>
                        {message}
                      </Box>
                    ))}
                    size="small"
                    fullWidth
                  >
                    {field.options && [
                      <MenuItem key="" value="">
                        <em>(not set)</em>
                      </MenuItem>,
                      ...field.options.map((option) => (
                        <MenuItem key={option} value={option}>
                          {option}
                        </MenuItem>
                      )),
                    ]}
                  </TextField>
                );
              })}
            </Box>
          </Box>
        ))}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={onSubmit} disabled={saving}>
          {submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
