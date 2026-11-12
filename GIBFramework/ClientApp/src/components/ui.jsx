import { createContext, useCallback, useContext, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router';
import { ApiError } from '../api.js';
import { DOC_TYPES, STATUS } from '../format.js';

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [items, setItems] = useState([]);
  const remove = useCallback((id) => setItems((list) => list.filter((t) => t.id !== id)), []);
  const push = useCallback((message, variant = 'success') => {
    const id = crypto.randomUUID();
    setItems((list) => [...list, { id, message, variant }]);
    setTimeout(() => remove(id), variant === 'danger' ? 8000 : 4000);
  }, [remove]);

  return (
    <ToastContext.Provider value={push}>
      {children}
      <div className="toast-container position-fixed bottom-0 end-0 p-3">
        {items.map((t) => (
          <div key={t.id} className={`toast show align-items-center text-bg-${t.variant} border-0`} role={t.variant === 'danger' ? 'alert' : 'status'}>
            <div className="d-flex">
              <div className="toast-body">{t.message}</div>
              <button type="button" className="btn-close btn-close-white me-2 m-auto" aria-label="Kapat" onClick={() => remove(t.id)} />
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const push = useContext(ToastContext);
  const toast = useCallback((message, variant) => push(message, variant), [push]);
  toast.error = (error) => {
    if (error instanceof ApiError) {
      const details = error.details?.length ? `: ${error.details.join('; ')}` : '';
      push(`${error.message}${details}`, 'danger');
    } else {
      push(String(error?.message || error), 'danger');
    }
  };
  return toast;
}

const openModals = [];
const focusable = 'a[href], button, input, select, textarea, [tabindex]';

export function Modal({ title, size, onClose, children, footer, scrollable }) {
  const titleId = useId();
  const dialogRef = useRef(null);
  const backdropRef = useRef(null);
  const openerRef = useRef(document.activeElement);
  const closeRef = useRef(onClose);
  useLayoutEffect(() => { closeRef.current = onClose; }, [onClose]);
  useLayoutEffect(() => {
    const dialog = dialogRef.current;
    const opener = openerRef.current;
    const appRoot = document.getElementById('root');
    const previous = openModals.at(-1);
    if (previous) {
      previous.inert = true;
      previous.removeAttribute('aria-modal');
    }
    openModals.push(dialog);
    const depth = openModals.length - 1;
    dialog.style.zIndex = 1055 + depth * 20;
    backdropRef.current.style.zIndex = 1050 + depth * 20;
    const controls = () => [...dialog.querySelectorAll(focusable)].filter(
      (element) => !element.matches(':disabled') && element.tabIndex >= 0 && element.getClientRects().length);
    const first = controls().find((element) => element.matches('input,select,textarea')) || controls()[0] || dialog;
    first.focus();
    if (appRoot) appRoot.inert = true;
    const onKey = (e) => {
      if (openModals.at(-1) !== dialog || e.defaultPrevented) return;
      if (e.key === 'Escape' && closeRef.current) {
        e.preventDefault();
        closeRef.current();
      }
      if (e.key !== 'Tab') return;
      const elements = controls();
      const current = elements.indexOf(document.activeElement);
      if (current < 0 || (e.shiftKey ? current === 0 : current === elements.length - 1)) {
        e.preventDefault();
        (e.shiftKey ? elements.at(-1) || dialog : elements[0] || dialog).focus();
      }
    };
    document.addEventListener('keydown', onKey);
    document.body.classList.add('modal-open');
    return () => {
      document.removeEventListener('keydown', onKey);
      const wasTop = openModals.at(-1) === dialog;
      openModals.splice(openModals.indexOf(dialog), 1);
      const top = openModals.at(-1);
      if (!top) {
        document.body.classList.remove('modal-open');
        if (appRoot) appRoot.inert = false;
      }
      if (wasTop) {
        if (top) {
          top.inert = false;
          top.setAttribute('aria-modal', 'true');
        }
        if (opener?.isConnected && (!top || top.contains(opener))) opener.focus();
        else top?.focus();
      }
    };
  }, []);

  return createPortal(
    <>
      <div ref={dialogRef} className="modal d-block" tabIndex={-1} role="dialog" aria-modal="true" aria-labelledby={titleId} onMouseDown={(e) => e.target === e.currentTarget && openModals.at(-1) === e.currentTarget && onClose?.()}>
        <div className={`modal-dialog modal-dialog-centered ${size ? `modal-${size}` : ''} ${scrollable ? 'modal-dialog-scrollable' : ''}`}>
          <div className="modal-content shadow">
            <div className="modal-header">
              <h1 className="modal-title fs-5" id={titleId}>{title}</h1>
              {onClose && <button type="button" className="btn-close" aria-label="Kapat" onClick={onClose} />}
            </div>
            {children}
            {footer}
          </div>
        </div>
      </div>
      <div ref={backdropRef} className="modal-backdrop show" />
    </>, document.body
  );
}

function Field({ field, value, onChange }) {
  const id = useId();
  const common = { id, name: field.name, required: field.required, autoComplete: field.autocomplete || 'off' };
  let control;
  if (field.type === 'select') {
    control = (
      <select className="form-select" {...common} value={value ?? ''} onChange={(e) => onChange(e.target.value)}>
        {(field.options || []).map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    );
  } else if (field.type === 'textarea') {
    control = <textarea className="form-control" {...common} rows={field.rows || 3} minLength={field.min} value={value ?? ''} onChange={(e) => onChange(e.target.value)} />;
  } else if (field.type === 'checkboxes') {
    const selected = value || [];
    control = (
      <div className="row row-cols-2 g-1">
        {(field.options || []).map((o) => (
          <div className="col" key={o.value}>
            <div className="form-check">
              <input className="form-check-input" type="checkbox" id={`${id}-${o.value}`} checked={selected.includes(o.value)}
                onChange={(e) => onChange(e.target.checked ? [...selected, o.value] : selected.filter((v) => v !== o.value))} />
              <label className="form-check-label small" htmlFor={`${id}-${o.value}`}>{o.label}</label>
            </div>
          </div>
        ))}
      </div>
    );
  } else {
    control = (
      <input className="form-control" type={field.type || 'text'} {...common} minLength={field.min} pattern={field.pattern} step={field.step} min={field.type === 'number' ? field.minValue : undefined}
        inputMode={field.inputmode} value={value ?? ''} onChange={(e) => onChange(e.target.value)} />
    );
  }

  return (
    <div className="mb-3">
      <label className="form-label" htmlFor={id}>{field.label}</label>
      {control}
      {field.help && <div className="form-text">{field.help}</div>}
      <div className="invalid-feedback">{field.invalid || 'Bu alan zorunludur.'}</div>
    </div>
  );
}

function FormDialog({ options, onDone }) {
  const { title, fields = [], submitText = 'Kaydet', submitVariant = 'primary', body, hideCancel, size } = options;
  const [values, setValues] = useState(() => Object.fromEntries(fields.map((f) => [f.name, f.value ?? (f.type === 'checkboxes' ? [] : '')])));
  const [validated, setValidated] = useState(false);
  const formRef = useRef(null);

  const submit = (e) => {
    e.preventDefault();
    if (!formRef.current.checkValidity()) {
      setValidated(true);
      return;
    }
    onDone(values);
  };

  return (
    <Modal title={title} size={size} onClose={() => onDone(null)}>
      <form ref={formRef} noValidate onSubmit={submit} className={validated ? 'was-validated' : ''}>
        <div className="modal-body">
          {body}
          {fields.map((f) => (
            <Field key={f.name} field={f} value={values[f.name]} onChange={(v) => setValues((s) => ({ ...s, [f.name]: v }))} />
          ))}
        </div>
        <div className="modal-footer">
          {!hideCancel && <button type="button" className="btn btn-outline-secondary" onClick={() => onDone(null)}>Vazgeç</button>}
          <button type="submit" className={`btn btn-${submitVariant}`}>{submitText}</button>
        </div>
      </form>
    </Modal>
  );
}

const DialogContext = createContext(null);
const DialogOpenContext = createContext(false);

export function DialogProvider({ children }) {
  const [dialogs, setDialogs] = useState([]);

  const form = useCallback((options) => new Promise((resolve) => {
    setDialogs((current) => [...current, { kind: 'form', id: crypto.randomUUID(), options, resolve }]);
  }), []);

  const show = useCallback((title, content, size = 'lg') => new Promise((resolve) => {
    setDialogs((current) => [...current, { kind: 'content', id: crypto.randomUUID(), options: { title, content, size }, resolve }]);
  }), []);

  const confirm = useCallback(async (title, message, submitText = 'Onayla', submitVariant = 'primary') =>
    (await form({ title, body: <p className="mb-0">{message}</p>, submitText, submitVariant })) !== null, [form]);

  const close = (dialog, result) => {
    setDialogs((current) => current.filter((item) => item !== dialog));
    dialog.resolve(result);
  };

  return (
    <DialogContext.Provider value={{ form, confirm, show }}>
      <DialogOpenContext.Provider value={dialogs.length > 0}>{children}</DialogOpenContext.Provider>
      {dialogs.map((dialog) => dialog.kind === 'form' ? (
        <FormDialog key={dialog.id} options={dialog.options} onDone={(result) => close(dialog, result)} />
      ) : (
        <Modal key={dialog.id} title={dialog.options.title} size={dialog.options.size} scrollable onClose={() => close(dialog, null)}>
          <div className="modal-body">{dialog.options.content}</div>
        </Modal>
      ))}
    </DialogContext.Provider>
  );
}

export const useDialogs = () => useContext(DialogContext);

export function BusyButton({ onClick, children, className = 'btn btn-primary', disabled, type = 'button', title }) {
  const [busy, setBusy] = useState(false);
  const dialogOpen = useContext(DialogOpenContext);
  const toast = useToast();
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const handle = async (e) => {
    if (!onClick) return;
    setBusy(true);
    try {
      await onClick(e);
    } catch (error) {
      toast.error(error);
    } finally {
      if (mounted.current) setBusy(false);
    }
  };

  return (
    <button type={type} className={className} disabled={disabled || busy} onClick={type === 'submit' ? undefined : handle} title={title}>
      {busy && !dialogOpen && <span className="spinner-border spinner-border-sm me-1" aria-hidden="true" />}
      {children}
    </button>
  );
}

export function useLoad(loader, deps) {
  const [state, setState] = useState({ loading: true, data: null, error: null });
  const request = useRef(0);
  const run = useCallback(async () => {
    const id = ++request.current;
    try {
      const data = await loader();
      if (id !== request.current) return null;
      setState({ loading: false, data, error: null });
      return data;
    } catch (error) {
      if (id !== request.current) return null;
      setState({ loading: false, data: null, error });
      return null;
    }
  }, deps);
  useLayoutEffect(() => {
    setState({ loading: true, data: null, error: null });
    run();
    return () => { ++request.current; };
  }, [run]);
  return { ...state, reload: run };
}

export const Spinner = ({ text = 'Yükleniyor…' }) => (
  <div className="d-flex align-items-center gap-2 text-body-secondary py-4">
    <div className="spinner-border spinner-border-sm" role="status" />
    <span>{text}</span>
  </div>
);

export const Empty = ({ icon, children }) => (
  <div className="text-center text-body-secondary py-5">
    <i className={`bi bi-${icon} fs-1 d-block mb-2`} />
    {children}
  </div>
);

export function ErrorAlert({ error }) {
  if (!error) return null;
  return (
    <div className="alert alert-danger" role="alert">
      <div className="fw-semibold"><i className="bi bi-exclamation-octagon me-1" />{error.message}</div>
      {error.code && <div className="small text-body-secondary">Kod: {error.code}</div>}
      {error.details?.length > 0 && <ul className="mb-0 mt-2 small">{error.details.map((d) => <li key={d}>{d}</li>)}</ul>}
    </div>
  );
}

export function PageHeader({ icon, title, subtitle, breadcrumb, actions }) {
  return (
    <div className="page-header mb-4">
      {breadcrumb && (
        <nav aria-label="breadcrumb">
          <ol className="breadcrumb small mb-1">
            {breadcrumb.map(([label, to]) => to
              ? <li key={label} className="breadcrumb-item"><Link to={to}>{label}</Link></li>
              : <li key={label} className="breadcrumb-item active" aria-current="page">{label}</li>)}
          </ol>
        </nav>
      )}
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
        <div>
          <h1 className="h4 mb-0 fw-semibold">{icon && <i className={`bi bi-${icon} me-2 text-primary`} />}{title}</h1>
          {subtitle && <div className="text-body-secondary small mt-1">{subtitle}</div>}
        </div>
        {actions && <div className="d-flex flex-wrap gap-2">{actions}</div>}
      </div>
    </div>
  );
}

export function Card({ title, icon, actions, children, className = '', bodyClass = 'card-body', footer }) {
  return (
    <div className={`card shadow-sm ${className}`}>
      {(title || actions) && (
        <div className="card-header bg-body d-flex justify-content-between align-items-center gap-2">
          <span className="fw-semibold">{icon && <i className={`bi bi-${icon} me-2 text-body-secondary`} />}{title}</span>
          {actions}
        </div>
      )}
      {bodyClass ? <div className={bodyClass}>{children}</div> : children}
      {footer && <div className="card-footer bg-body">{footer}</div>}
    </div>
  );
}

export function StatusBadge({ status }) {
  const [label, color] = STATUS[status] || [status, 'secondary'];
  return <span className={`badge text-bg-${color}`}>{label}</span>;
}

export function DocTypeBadge({ type }) {
  const color = { EFatura: 'primary', EArsiv: 'info', PaperInvoice: 'secondary', Undetermined: 'danger' }[type] || 'secondary';
  return <span className={`badge rounded-pill text-bg-${color}`}>{DOC_TYPES[type] || type}</span>;
}

export function SeverityBadge({ severity }) {
  const color = { Error: 'danger', Warning: 'warning', Info: 'info' }[severity] || 'secondary';
  const label = { Error: 'Hata', Warning: 'Uyarı', Info: 'Bilgi' }[severity] || severity;
  return <span className={`badge text-bg-${color}`}>{label}</span>;
}
