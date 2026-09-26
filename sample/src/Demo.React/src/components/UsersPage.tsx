import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import LinearProgress from '@mui/material/LinearProgress';
import Paper from '@mui/material/Paper';
import Snackbar from '@mui/material/Snackbar';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { useCallback, useEffect, useState } from 'react';
import { createUser, deleteUser, listUsers, updateUser, type User } from '../api/usersApi';
import {
  createUserSections,
  editUserSections,
  emptyValues,
  toRequestBody,
  userToEditValues,
  type FormValues,
} from '../forms/userFields';
import type { FieldErrors } from '../validationProblem';
import { UserFormDialog } from './UserFormDialog';

type DialogState = { mode: 'create' } | { mode: 'edit'; userId: string } | null;
type Notice = { message: string; severity: 'success' | 'error' } | null;

export function UsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<DialogState>(null);
  const [values, setValues] = useState<FormValues>({});
  const [errors, setErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    try {
      setUsers(await listUsers());
    } catch (error) {
      setNotice({ message: (error as Error).message, severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  function openCreateDialog() {
    setValues(emptyValues(createUserSections));
    setErrors({});
    setDialog({ mode: 'create' });
  }

  function openEditDialog(user: User) {
    setValues(userToEditValues(user));
    setErrors({});
    setDialog({ mode: 'edit', userId: user.id });
  }

  async function submit() {
    if (dialog === null) {
      return;
    }

    setSaving(true);
    const body = toRequestBody(values);
    const result = dialog.mode === 'create' ? await createUser(body) : await updateUser(dialog.userId, body);
    setSaving(false);

    if (result.ok) {
      setDialog(null);
      setNotice({ message: dialog.mode === 'create' ? 'User created successfully!' : 'User updated successfully!', severity: 'success' });
      await loadUsers();
    } else if (Object.keys(result.errors).length > 0) {
      // The error keys are the field names (wire paths): hand them to the dialog as they are.
      setErrors(result.errors);
    } else {
      setNotice({ message: result.message, severity: 'error' });
    }
  }

  async function remove(user: User) {
    const result = await deleteUser(user.id);
    setNotice(result.ok ? { message: 'User deleted successfully!', severity: 'success' } : { message: result.message, severity: 'error' });
    await loadUsers();
  }

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h4" component="h1">
          Users
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>
          Create User
        </Button>
      </Box>

      <TableContainer component={Paper} elevation={2}>
        {loading && <LinearProgress />}
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Gender</TableCell>
              <TableCell>Telephone</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((user) => (
              <TableRow key={user.id} hover data-user-id={user.id}>
                <TableCell data-label="Name">{`${user.firstName} ${user.lastName}`}</TableCell>
                <TableCell data-label="Email">{user.email}</TableCell>
                <TableCell data-label="Gender">{user.gender}</TableCell>
                <TableCell data-label="Telephone">{user.telephone}</TableCell>
                <TableCell align="right">
                  <Tooltip title="Edit">
                    <IconButton aria-label="Edit" size="small" color="primary" onClick={() => openEditDialog(user)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton aria-label="Delete" size="small" color="error" onClick={() => void remove(user)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {!loading && users.length === 0 && (
          <Typography sx={{ p: 2 }} color="text.secondary">
            No users found. Create one to get started!
          </Typography>
        )}
      </TableContainer>

      <UserFormDialog
        open={dialog !== null}
        title={dialog?.mode === 'edit' ? 'Edit User' : 'Create User'}
        submitLabel={dialog?.mode === 'edit' ? 'Update' : 'Create'}
        sections={dialog?.mode === 'edit' ? editUserSections : createUserSections}
        values={values}
        errors={errors}
        saving={saving}
        onChange={(name, value) => setValues((current) => ({ ...current, [name]: value }))}
        onSubmit={() => void submit()}
        onClose={() => setDialog(null)}
      />

      {/* Bottom-center: a bottom-right snackbar would cover the Edit/Delete buttons of the last table rows. */}
      <Snackbar open={notice !== null} autoHideDuration={4000} onClose={() => setNotice(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        {notice ? (
          <Alert severity={notice.severity} variant="filled" onClose={() => setNotice(null)}>
            {notice.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
}
