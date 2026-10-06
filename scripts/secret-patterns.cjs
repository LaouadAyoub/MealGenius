// Pattern checks are a publication guardrail, not proof of credential revocation.
const patterns = [
  ['OpenAI key', /\bsk-(?:proj-)?[A-Za-z0-9_-]{25,}/g],
  ['Google OAuth secret', /GOCSPX-[A-Za-z0-9_-]+/g],
  ['Stripe secret', /\b(?:sk|rk)_(?:live|test)_[A-Za-z0-9]+/g],
  ['Stripe webhook secret', /whsec_[A-Za-z0-9]+/g],
  ['Azure storage key', /AccountKey=[^;\s"'<>]+/g],
  ['Service Bus credential', /SharedAccessKey=[^;\s"'<>]+/g],
  ['Database password', /Password=[^;\s"'<>]+/gi],
  ['Private key', /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/g],
  ['GitHub token', /\b(?:ghp_|github_pat_)[A-Za-z0-9_]{20,}/g],
  ['Literal credential', /(?:["']?(?:ApiKey|ClientSecret|JwtKey|UnsplashAccessKey|PixabayApiKey|Key|Password)["']?)\s*[:=]\s*["'][^"'\r\n]{8,}["']/gi],
  ['Seed password', /\.CreateAsync\(user,\s*"[^"]+"\)/g]
];
function placeholder(text) {
  if (/^(?:AccountKey|SharedAccessKey|Password)=\[\^/.test(text)) return true; // Regex source, not a value.
  return /REPLACE_|REDACTED|YOUR_|example|synthetic|ci-ephemeral-only|test-value|Password=\\|Password=\$|Password=\{/i.test(text);
}
function findings(text) {
  const result = [];
  for (const [kind, regex] of patterns) {
    regex.lastIndex = 0;
    for (const match of text.matchAll(regex)) {
      if (!placeholder(match[0])) result.push({kind, line: text.slice(0, match.index).split('\n').length});
    }
  }
  return result;
}
function sanitize(text) {
  for (const [, regex] of patterns) {
    regex.lastIndex = 0;
    text = text.replace(regex, match => {
      if (placeholder(match)) return match;
      if (match.startsWith('.CreateAsync')) return '.CreateAsync(user, "REDACTED")';
      if (/^[\w]+=/i.test(match)) return match.split('=')[0] + '=REDACTED';
      if (/[:=]\s*["']/.test(match)) return match.replace(/([:=]\s*["'])[^"']+(["'])$/, '$1REDACTED$2');
      return 'REDACTED';
    });
  }
  return text.replace(/\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b/gi, 'redacted@example.invalid');
}
module.exports = {findings, sanitize};
