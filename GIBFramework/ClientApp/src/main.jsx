import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HashRouter } from 'react-router';
import '../vendor/bootstrap/css/bootstrap.min.css';
import '../vendor/bootstrap-icons/bootstrap-icons.min.css';
import './styles.css';
import './ivory.css';
import App from './App.jsx';
import { DialogProvider, ToastProvider } from './components/ui.jsx';
import { SessionProvider } from './session.jsx';
import { applyAppearance, watchSystemTheme } from './theme.js';

applyAppearance();
watchSystemTheme();

document.addEventListener('copy', (event) => {
  const target = event.target;
  const selection = document.getSelection()?.anchorNode;
  const selectedElement = selection instanceof Element ? selection : selection?.parentElement;
  if (target instanceof Element && target.closest('input, textarea, [contenteditable]')) return;
  if (selectedElement?.closest('.user-select-all, [data-copy-value]')) return;
  event.preventDefault();
});

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
