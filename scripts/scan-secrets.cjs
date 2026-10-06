const fs = require('fs');
const cp = require('child_process');
const {findings} = require('./secret-patterns.cjs');
const git = (...args) => cp.execFileSync('git', args, {maxBuffer: 128 * 1024 * 1024});
const historyAt = process.argv.indexOf('--history');
let entries;
if (historyAt >= 0) {
  const ref = process.argv[historyAt + 1];
  if (!ref) throw new Error('Specify a history reference.');
  entries = git('rev-list', '--objects', ref).toString().trim().split('\n')
    .map(row => { const i = row.indexOf(' '); return [row.slice(i + 1), row.slice(0, i)]; })
    .filter(([path, hash]) => hash.length === 40);
  const types = cp.execFileSync('git', ['cat-file', '--batch-check=%(objectname) %(objecttype)'], {
    input: entries.map(([, hash]) => hash).join('\n') + '\n', maxBuffer: 128 * 1024 * 1024
  }).toString().trim().split('\n').map(line => line.split(' ')[1]);
  if (types.some(type => !['blob', 'tree', 'commit', 'tag'].includes(type)))
    throw new Error('History contains an unreadable object; scan cannot establish a result.');
  entries = entries.filter((_, index) => types[index] === 'blob');
} else {
  entries = git('ls-files', '--cached', '--others', '--exclude-standard', '-z').toString().split('\0')
    .filter(Boolean).map(path => [path, null]);
}
let checked = 0, failures = 0;
for (const [path, hash] of entries) {
  // Ignore directories; inspect all blobs, including binary byte sequences for known credential signatures.
  if (!hash && (!fs.existsSync(path) || !fs.statSync(path).isFile())) continue;
  const data = hash ? git('cat-file', 'blob', hash) : fs.readFileSync(path);
  checked++;
  for (const item of findings(data.toString('utf8'))) {
    console.error(path + ':' + item.line + ': ' + item.kind + (hash ? ' [blob ' + hash.slice(0, 8) + ']' : ''));
    failures++;
  }
}
console.log('Inspected ' + checked + ' file/blob versions; findings: ' + failures + '. Values are never printed.');
process.exitCode = failures ? 1 : 0;
