import AppBar from '@mui/material/AppBar';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { UsersPage } from './components/UsersPage';

// Material Design, with the same primary color as the Blazor (MudBlazor) sample.
const theme = createTheme({
  palette: {
    primary: { main: '#594ae2' },
  },
});

export function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="div">
            Demo.React
          </Typography>
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <UsersPage />
      </Container>
    </ThemeProvider>
  );
}
