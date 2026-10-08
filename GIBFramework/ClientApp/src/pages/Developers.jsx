import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useSession } from '../session.jsx';
import { Card, PageHeader, Spinner, useLoad } from '../components/ui.jsx';

const METHOD_COLOR = { get: 'primary', post: 'success', put: 'warning', patch: 'info', delete: 'danger' };

const SAMPLE_INVOICE = {
  deliveryDate: '2026-10-08',
  currency: 'TRY',
  customer: { taxId: '10000000146', kind: 'NaturalPerson', regime: 'NotATaxpayer', title: 'Ayşe Yılmaz', neighborhood: 'Caferağa', district: 'Kadıköy', city: 'İstanbul' },
  lines: [{ name: 'Danışmanlık', quantity: 2, unitCode: 'HUR', unitPrice: 1500, vatRate: 20 }],
  notes: ['Teşekkür ederiz.'],
  saveCustomer: true,
};

function samples(origin) {
  const body = JSON.stringify(SAMPLE_INVOICE, null, 2);
  return {
    curl: `curl -X POST "${origin}/api/v1/invoices" \\
  -H "X-Api-Key: gfk_xxxxxxxxxxxx_xxxxxxxx" \\
  -H "Idempotency-Key: 5f0c6a8e-1d2b-4c3d-9e8f-0a1b2c3d4e5f" \\
  -H "Content-Type: application/json" \\
  -d '${JSON.stringify(SAMPLE_INVOICE)}'`,
    csharp: `using System.Net.Http.Json;

var http = new HttpClient { BaseAddress = new Uri("${origin}/") };
http.DefaultRequestHeaders.Add("X-Api-Key", Environment.GetEnvironmentVariable("GIBFRAMEWORK_API_KEY"));

using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/invoices")
{
    Content = JsonContent.Create(new
    {
        currency = "TRY",
        customer = new { taxId = "10000000146", kind = "NaturalPerson", regime = "NotATaxpayer", title = "Ayşe Yılmaz", city = "İstanbul" },
        lines = new[] { new { name = "Danışmanlık", quantity = 2, unitCode = "HUR", unitPrice = 1500, vatRate = 20 } },
    }),
};
request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
var response = await http.SendAsync(request);
response.EnsureSuccessStatusCode();
var invoice = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();`,
    javascript: `const response = await fetch('${origin}/api/v1/invoices', {
  method: 'POST',
  headers: {
    'X-Api-Key': process.env.GIBFRAMEWORK_API_KEY,
    'Idempotency-Key': crypto.randomUUID(),
    'Content-Type': 'application/json',
  },
  body: JSON.stringify(${body.replace(/\n/g, '\n  ')}),
});
if (!response.ok) throw new Error((await response.json()).title);
const invoice = await response.json();`,
    python: `import os, uuid, requests

response = requests.post(
    "${origin}/api/v1/invoices",
    headers={
        "X-Api-Key": os.environ["GIBFRAMEWORK_API_KEY"],
        "Idempotency-Key": str(uuid.uuid4()),
    },
    json=${body.replace(/true/g, 'True').replace(/\n/g, '\n    ')},
    timeout=30,
)
response.raise_for_status()
invoice = response.json()`,
  };
}

function resolve(doc, schema) {
  if (!schema) return null;
  if (schema.$ref) return resolve(doc, doc.components?.schemas?.[schema.$ref.split('/').pop()]);
  if (schema.allOf) return resolve(doc, schema.allOf[0]);
  return schema;
}

function typeOf(doc, schema) {
  const s = resolve(doc, schema) || {};
  if (schema?.$ref) return schema.$ref.split('/').pop();
  if (s.type === 'array') return `${typeOf(doc, s.items)}[]`;
  if (s.enum) return s.enum.join(' | ');
  return [s.type, s.format].filter(Boolean).join(' · ') || 'object';
}

function Operation({ doc, method, path, op }) {
  const [open, setOpen] = useState(false);
  const bodySchema = resolve(doc, op.requestBody?.content?.['application/json']?.schema);
  const required = new Set(bodySchema?.required || []);
  return (
    <div className="list-group-item p-0">
      <button type="button" className="btn w-100 text-start d-flex flex-wrap align-items-center gap-2 px-3 py-2" onClick={() => setOpen(!open)} aria-expanded={open}>
        <span className={`badge text-bg-${METHOD_COLOR[method]} method-badge`}>{method.toUpperCase()}</span>
        <code className="text-body">{path}</code>
        {op.security?.length ? <i className="bi bi-lock text-body-secondary" title="Kimlik gerekir" /> : <i className="bi bi-unlock text-success" title="Herkese açık" />}
        <span className="small text-body-secondary ms-auto">{op.summary || op.operationId}</span>
        <i className={`bi bi-chevron-${open ? 'up' : 'down'} small`} />
      </button>
      {open && (
        <div className="px-3 pb-3">
          {op['x-gibframework-policies'] && <div className="small mb-2"><span className="text-body-secondary">Yetki:</span> {op['x-gibframework-policies'].join(', ')}</div>}
          {op.parameters?.length > 0 && (
            <table className="table table-sm small mb-2">
              <thead><tr><th>Parametre</th><th>Konum</th><th>Tür</th><th>Zorunlu</th></tr></thead>
              <tbody>{op.parameters.map((p) => <tr key={`${p.in}-${p.name}`}><td className="font-monospace">{p.name}</td><td>{p.in}</td><td>{typeOf(doc, p.schema)}</td><td>{p.required ? 'Evet' : '—'}</td></tr>)}</tbody>
            </table>
          )}
          {bodySchema?.properties && (
            <table className="table table-sm small mb-2">
              <thead><tr><th>Gövde alanı</th><th>Tür</th><th>Zorunlu</th></tr></thead>
              <tbody>{Object.entries(bodySchema.properties).map(([name, s]) => <tr key={name}><td className="font-monospace">{name}</td><td>{typeOf(doc, s)}</td><td>{required.has(name) ? 'Evet' : '—'}</td></tr>)}</tbody>
            </table>
          )}
          <div className="small"><span className="text-body-secondary">Yanıtlar:</span> {Object.keys(op.responses || {}).map((c) => <span key={c} className={`badge me-1 text-bg-${c.startsWith('2') ? 'success' : c.startsWith('4') ? 'warning' : 'secondary'}`}>{c}</span>)}</div>
        </div>
      )}
    </div>
  );
}

function Reference() {
  const [q, setQ] = useState('');
  const { data: doc, loading, error } = useLoad(async () => {
    const r = await fetch('/swagger/v1/swagger.json', { cache: 'no-store' });
    if (!r.ok) throw new Error('OpenAPI dokümanı alınamadı.');
    return r.json();
  }, []);

  const groups = useMemo(() => {
    if (!doc) return [];
    const byTag = new Map((doc.tags || []).map((t) => [t.name, []]));
    Object.entries(doc.paths || {}).forEach(([path, ops]) => Object.entries(ops).forEach(([method, op]) => {
      if (!METHOD_COLOR[method]) return;
      const tag = op.tags?.[0] || 'Diğer';
      if (!byTag.has(tag)) byTag.set(tag, []);
      byTag.get(tag).push({ method, path, op });
    }));
    const term = q.trim().toLocaleLowerCase('tr-TR');
    return [...byTag.entries()]
      .map(([tag, items]) => [tag, items.filter((x) => !term || `${x.method} ${x.path} ${x.op.summary || ''} ${tag}`.toLocaleLowerCase('tr-TR').includes(term))])
      .filter(([, items]) => items.length);
  }, [doc, q]);

  if (loading) return <Spinner />;
  if (error) return <div className="alert alert-warning mb-0">{error.message}</div>;
  return (
    <>
      <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
        <input className="form-control" style={{ maxWidth: '24rem' }} placeholder="Uç nokta ara" aria-label="Uç nokta ara" value={q} onChange={(e) => setQ(e.target.value)} />
        <span className="small text-body-secondary">{groups.reduce((s, [, i]) => s + i.length, 0)} uç nokta · OpenAPI {doc.openapi}</span>
      </div>
      {groups.map(([tag, items]) => (
        <div key={tag} className="mb-3">
          <h3 className="h6 fw-semibold mb-2">{tag}</h3>
          <div className="list-group">{items.map((x) => <Operation key={`${x.method}-${x.path}`} doc={doc} {...x} />)}</div>
        </div>
      ))}
    </>
  );
}

const ORDER_SCHEMA = `{
  "event": "order.paid",
  "externalId": "SIPARIS-1001",
  "orderNumber": "1001",
  "currency": "TRY",
  "pricesIncludeTax": false,
  "customer": {
    "taxId": "11111111111",
    "name": "Ayşe Yılmaz",
    "company": "",
    "taxOffice": "",
    "email": "ayse@example.com",
    "phone": "05321234567",
    "address": "Atatürk Cad. No:1",
    "district": "Kadıköy",
    "city": "İstanbul",
    "postalCode": "34710",
    "country": "TR"
  },
  "lines": [
    { "name": "Web hosting (1 yıl)", "quantity": 1, "unitPrice": 1000, "vatRate": 20, "discount": 0, "unitCode": "C62" }
  ],
  "notes": ["Ödeme: kredi kartı"]
}`;

const WEBHOOK_SAMPLE = `POST https://sizin-uygulamaniz.com/gibframework
Content-Type: application/json
X-GibFramework-Event: invoice.issued
X-GibFramework-Delivery: 0199c2f4-…
X-GibFramework-Timestamp: 1791460000
X-GibFramework-Signature: sha256=<HMAC_SHA256(imza_anahtari, timestamp + "." + govde)>

{
  "id": "…",
  "event": "invoice.issued",
  "createdAt": "2026-10-08T11:30:00Z",
  "tenantId": "…",
  "data": {
    "invoiceId": "…",
    "documentNumber": "DEA2026000000012",
    "documentType": "EArsiv",
    "status": "Sent",
    "issueDate": "2026-10-08",
    "currency": "TRY",
    "payableAmount": 1200.00,
    "ettn": "…",
    "orderNumber": "1001",
    "externalOrderId": "SIPARIS-1001",
    "customer": { "taxId": "11111111111", "title": "Ayşe Yılmaz", "email": "ayse@example.com" },
    "links": { "view": "https://…/f/…", "panel": "https://…/#/invoices/…" }
  }
}`;

const VERIFY_SAMPLES = {
  php: `$body = file_get_contents('php://input');
$ts = $_SERVER['HTTP_X_GIBFRAMEWORK_TIMESTAMP'] ?? '';
$expected = 'sha256=' . hash_hmac('sha256', $ts . '.' . $body, $secret);
if (!hash_equals($expected, $_SERVER['HTTP_X_GIBFRAMEWORK_SIGNATURE'] ?? '') || abs(time() - (int) $ts) > 300) {
    http_response_code(401);
    exit;
}
$event = json_decode($body, true);`,
  node: `import crypto from 'node:crypto';

app.post('/gibframework', express.raw({ type: 'application/json' }), (req, res) => {
  const ts = req.get('X-GibFramework-Timestamp');
  const expected = 'sha256=' + crypto.createHmac('sha256', secret).update(\`\${ts}.\${req.body}\`).digest('hex');
  const given = req.get('X-GibFramework-Signature') || '';
  if (expected.length !== given.length || !crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(given))) return res.sendStatus(401);
  const event = JSON.parse(req.body);
  res.sendStatus(200);
});`,
  csharp: `app.MapPost("/gibframework", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var ts = request.Headers["X-GibFramework-Timestamp"].ToString();
    var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}.{body}"));
    var expected = "sha256=" + Convert.ToHexStringLower(hash);
    var given = request.Headers["X-GibFramework-Signature"].ToString();
    return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given))
        ? Results.Ok()
        : Results.Unauthorized();
});`,
};

function Integrations() {
  const [lang, setLang] = useState('php');
  return (
    <div className="row g-4">
      <div className="col-xl-6">
        <h2 className="h6 fw-semibold">Gelen sipariş (otomatik faturalama)</h2>
        <table className="table table-sm small">
          <tbody>
            <tr><th className="w-25">Adres</th><td className="font-monospace">POST /api/v1/hooks/{'{entegrasyonId}'}</td></tr>
            <tr><th>İmza</th><td><code>X-GibFramework-Signature: sha256=HMAC_SHA256(imza anahtarı, gövde)</code></td></tr>
            <tr><th>Mükerrer koruma</th><td>Aynı <code>externalId</code> ikinci kez fatura oluşturmaz.</td></tr>
            <tr><th>TCKN / VKN</th><td>Geçersiz veya boşsa <code>11111111111</code> (nihai tüketici) kullanılır.</td></tr>
            <tr><th>KDV</th><td>0, 1, 10, 20 (eski oranlar 8 ve 18 kabul edilir)</td></tr>
            <tr><th>Yanıt</th><td><code>201</code> oluşturuldu · <code>200</code> mevcut / yok sayıldı · <code>401</code> imza · <code>422</code> doğrulama</td></tr>
          </tbody>
        </table>
        <pre className="code-block code-scroll">{ORDER_SCHEMA}</pre>
      </div>
      <div className="col-xl-6">
        <h2 className="h6 fw-semibold">Fatura olayları (giden webhook)</h2>
        <pre className="code-block code-scroll">{WEBHOOK_SAMPLE}</pre>
        <div className="d-flex gap-2 my-2">
          {[['php', 'PHP'], ['node', 'Node.js'], ['csharp', 'C#']].map(([k, l]) => (
            <button key={k} type="button" className={`btn btn-sm btn-pill ${lang === k ? 'btn-primary' : 'btn-outline-secondary'}`} onClick={() => setLang(k)}>{l}</button>
          ))}
        </div>
        <pre className="code-block code-scroll">{VERIFY_SAMPLES[lang]}</pre>
      </div>
    </div>
  );
}

export function Developers() {
  const session = useSession();
  const [tab, setTab] = useState('start');
  const [lang, setLang] = useState('curl');
  const origin = window.location.origin;
  const code = samples(origin);

  return (
    <>
      <PageHeader icon="code-square" title="Geliştirici dokümanı" subtitle="Üçüncü taraf uygulama entegrasyonu"
        actions={(
          <>
            <a className="btn btn-outline-primary btn-pill px-3" href="/swagger" target="_blank" rel="noopener noreferrer"><i className="bi bi-braces me-1" />Swagger</a>
            <a className="btn btn-outline-primary btn-pill px-3" href="/redoc" target="_blank" rel="noopener noreferrer"><i className="bi bi-book me-1" />ReDoc</a>
            <a className="btn btn-outline-secondary btn-pill px-3" href="/swagger/v1/swagger.json" target="_blank" rel="noopener noreferrer"><i className="bi bi-filetype-json me-1" />OpenAPI</a>
            {session.can('ApiKeyManage') && <Link className="btn btn-primary btn-pill px-3" to="/settings/api-keys"><i className="bi bi-key me-1" />API anahtarları</Link>}
          </>
        )} />
      <Card bodyClass="">
        <div className="card-header bg-body">
          <ul className="nav nav-tabs card-header-tabs">
            {[['start', 'Başlangıç'], ['samples', 'Örnek kodlar'], ['integrations', 'Entegrasyon & webhook'], ['reference', 'Uç nokta referansı']].map(([k, l]) => (
              <li className="nav-item" key={k}><button type="button" className={`nav-link ${tab === k ? 'active' : ''}`} onClick={() => setTab(k)}>{l}</button></li>
            ))}
          </ul>
        </div>
        <div className="card-body">
          {tab === 'start' && (
            <div className="row g-4">
              <div className="col-lg-6">
                <h2 className="h6 fw-semibold">Bağlantı</h2>
                <table className="table table-sm small">
                  <tbody>
                    <tr><th className="w-25">Temel adres</th><td className="font-monospace">{origin}/api/v1</td></tr>
                    <tr><th>Biçim</th><td>JSON (UTF-8), tarih: ISO 8601, saat dilimi: Europe/Istanbul</td></tr>
                    <tr><th>Kimlik</th><td><code>X-Api-Key: gfk_…</code> veya <code>Authorization: Bearer …</code></td></tr>
                    <tr><th>Mükerrer koruma</th><td><code>Idempotency-Key: &lt;uuid&gt;</code> (fatura oluşturma)</td></tr>
                    <tr><th>İzleme</th><td><code>X-Correlation-Id</code> (yanıtta döner)</td></tr>
                    <tr><th>Sayfalama</th><td><code>GET /invoices/page?take=50&amp;skip=0</code>, yanıt: <code>{'{ items, total }'}</code></td></tr>
                  </tbody>
                </table>
              </div>
              <div className="col-lg-6">
                <h2 className="h6 fw-semibold">Hata yanıtları (RFC 7807)</h2>
                <table className="table table-sm small">
                  <tbody>
                    <tr><th className="w-25">400</th><td>Alan doğrulama hatası (<code>errors</code>)</td></tr>
                    <tr><th>401</th><td>Anahtar/token yok, geçersiz, iptal edilmiş</td></tr>
                    <tr><th>403</th><td>Yetki yok</td></tr>
                    <tr><th>404</th><td>Kayıt bulunamadı</td></tr>
                    <tr><th>409</th><td>Çakışma / mükerrer kayıt</td></tr>
                    <tr><th>422</th><td>İş kuralı ihlali (<code>code</code>, <code>details</code>)</td></tr>
                    <tr><th>429</th><td>İstek sınırı</td></tr>
                    <tr><th>502</th><td>GİB portalı hatası</td></tr>
                  </tbody>
                </table>
              </div>
              <div className="col-12">
                <h2 className="h6 fw-semibold">Fatura akışı</h2>
                <div className="d-flex flex-wrap align-items-center gap-2 small">
                  {['POST /invoices', 'POST /invoices/{id}/submit', 'POST /invoices/{id}/approve', 'POST /invoices/{id}/sign', 'POST /invoices/{id}/transmit', 'GET /invoices/{id}'].map((s, i, a) => (
                    <span key={s} className="d-inline-flex align-items-center gap-2"><code className="px-2 py-1 bg-body-tertiary rounded">{s}</code>{i < a.length - 1 && <i className="bi bi-arrow-right text-body-secondary" />}</span>
                  ))}
                </div>
              </div>
            </div>
          )}
          {tab === 'samples' && (
            <>
              <div className="btn-group mb-3" role="group" aria-label="Dil">
                {[['curl', 'cURL'], ['csharp', 'C#'], ['javascript', 'JavaScript'], ['python', 'Python']].map(([k, l]) => (
                  <button key={k} type="button" className={`btn btn-sm ${lang === k ? 'btn-primary' : 'btn-outline-primary'}`} onClick={() => setLang(k)}>{l}</button>
                ))}
              </div>
              <pre className="code-block">{code[lang]}</pre>
            </>
          )}
          {tab === 'integrations' && <Integrations />}
          {tab === 'reference' && <Reference />}
        </div>
      </Card>
    </>
  );
}
