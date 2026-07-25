const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 4000;
const API_HOST = process.env.API_HOST || 'backend';
const API_PORT = process.env.API_PORT || '5030';
const browserDir = path.join(__dirname, 'browser');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};

function serveFile(res, filePath) {
  const ext = path.extname(filePath);
  const contentType = MIME[ext] || 'application/octet-stream';
  try {
    const content = fs.readFileSync(filePath);
    res.writeHead(200, { 'Content-Type': contentType, 'Cache-Control': 'public, max-age=31536000' });
    res.end(content);
    return true;
  } catch {
    return false;
  }
}

const server = http.createServer((req, res) => {
  // Proxy API requests to backend
  if (req.url.startsWith('/KnowledgeVault')) {
    const apiPath = req.url.replace(/^\/KnowledgeVault/, '');
    const options = {
      hostname: API_HOST,
      port: API_PORT,
      path: apiPath,
      method: req.method,
      headers: { ...req.headers, host: `${API_HOST}:${API_PORT}` },
    };

    const proxyReq = http.request(options, (proxyRes) => {
      res.writeHead(proxyRes.statusCode, proxyRes.headers);
      proxyRes.pipe(res);
    });

    proxyReq.on('error', (err) => {
      res.writeHead(502);
      res.end('Bad Gateway: ' + err.message);
    });

    req.pipe(proxyReq);
    return;
  }

  // Serve static files
  const urlPath = req.url === '/' ? '/index.csr.html' : req.url;
  const filePath = path.join(browserDir, urlPath);

  if (serveFile(res, filePath)) return;

  // SPA fallback for non-file routes
  if (!path.extname(urlPath)) {
    serveFile(res, path.join(browserDir, 'index.csr.html'));
    return;
  }

  res.writeHead(404);
  res.end('Not Found');
});

server.listen(PORT, () => {
  console.log(`Server running on port ${PORT}, API proxy -> http://${API_HOST}:${API_PORT}`);
});
