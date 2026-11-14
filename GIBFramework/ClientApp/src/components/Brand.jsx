import upper from '../assets/ivory/g-upper.svg';
import lower from '../assets/ivory/g-lower.svg';
import center from '../assets/ivory/g-center.svg';

export function Brand({ variant = 'compact' }) {
  return (
    <span className={`gib-brand gib-brand--${variant}`} aria-label={variant === 'icon' ? 'GIB Framework' : undefined} role={variant === 'icon' ? 'img' : undefined}>
      <span className="gib-symbol" aria-hidden="true">
        <span className="gib-symbol-vectors">
          <img className="gib-symbol-upper" src={upper} width="103.117" height="72.6505" alt="" />
          <img className="gib-symbol-lower" src={lower} width="103.117" height="59.7609" alt="" />
          <img className="gib-symbol-center" src={center} width="63.8621" height="22.2639" alt="" />
        </span>
      </span>
      {variant !== 'icon' && <span className="gib-brand-text">
        <span className="gib-wordmark"><strong>GIB</strong> Framework</span>
        {variant === 'full' && <span className="gib-brand-caption">Kurumsal e-Belge Yönetimi</span>}
      </span>}
    </span>
  );
}

export const REPO_URL = 'https://github.com/hamzadenizyilmaz/GIB-Framework';

export function LegalStrip({ className = '' }) {
  return (
    <footer className={`legal-strip ${className}`}>
      <div className="legal-strip-inner">
        <nav className="legal-strip-links" aria-label="API dokümantasyonu">
          <a href="/swagger" target="_blank" rel="noopener noreferrer"><i className="bi bi-braces me-1" />Swagger API Dokümantasyonu</a>
          <a href="/redoc" target="_blank" rel="noopener noreferrer"><i className="bi bi-book me-1" />ReDoc API Dokümantasyonu</a>
        </nav>
        <span className="legal-strip-copy">Telif Hakları 2019-2026 - Hamza Deniz Yılmaz</span>
        <a className="legal-strip-repo" href={REPO_URL} target="_blank" rel="noopener noreferrer">
          <i className="bi bi-github me-1" />GitHub Repos: {REPO_URL.replace('https://', '')}
        </a>
      </div>
    </footer>
  );
}
