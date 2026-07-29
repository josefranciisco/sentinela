# SSL Certificates

Place your SSL certificate and key files in this directory:

- `cert.pem` - SSL certificate
- `key.pem` - SSL private key

## Self-signed certificate for development

```bash
# Linux/macOS
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout docker/nginx/ssl/key.pem \
  -out docker/nginx/ssl/cert.pem \
  -subj "/CN=sentinela.local" \
  -addext "subjectAltName=DNS:sentinela.local,DNS:localhost"

# Windows (PowerShell)
openssl req -x509 -nodes -days 365 -newkey rsa:2048 `
  -keyout docker/nginx/ssl/key.pem `
  -out docker/nginx/ssl/cert.pem `
  -subj "/CN=sentinela.local" `
  -addext "subjectAltName=DNS:sentinela.local,DNS:localhost"
```

## Production

Replace with certificates from a trusted CA (Let's Encrypt, etc.) and ensure proper file permissions.
