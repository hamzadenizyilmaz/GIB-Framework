import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HashRouter } from 'react-router';
import '../vendor/bootstrap/css/bootstrap.min.css';
import '../vendor/bootstrap-icons/bootstrap-icons.min.css';
import './styles.css';
import App from './App.jsx';
import { DialogProvider, ToastProvider } from './components/ui.jsx';
import { SessionProvider } from './session.jsx';
import { applyAppearance, watchSystemTheme } from './theme.js';

applyAppearance();
watchSystemTheme();

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <HashRouter>
      <ToastProvider>
        <SessionProvider>
          <DialogProvider>
            <App />
          </DialogProvider>
        </SessionProvider>
      </ToastProvider>
    </HashRouter>
  </StrictMode>,
);
