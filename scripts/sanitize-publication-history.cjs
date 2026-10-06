// Explicitly rewrites only a clean portfolio/* branch; original private refs are left untouched.
// The tip tree must already be sanitized and is preserved byte-for-byte.
const cp = require('child_process'), fs = require('fs'), os = require('os'), path = require('path');
const {sanitize, findings} = require('./secret-patterns.cjs');
const git = (args, input, env = {}) => cp.execFileSync('git', args, {
  input, env: {...process.env, ...env}, maxBuffer: 128 * 1024 * 1024
});
const branch = process.argv[2];
if (!branch?.startsWith('portfolio/') || git(['branch', '--show-current']).toString().trim() !== branch)
  throw new Error('Run on the explicitly named portfolio/* branch.');
if (git(['status', '--porcelain']).length) throw new Error('Commit the sanitized work first.');
const ref = 'refs/heads/' + branch, tip = git(['rev-parse', ref]).toString().trim();
cp.execFileSync(process.execPath, ['scripts/scan-secrets.cjs'], {stdio: 'inherit'});
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'mealgenius-history-'));
const indexEnv = {GIT_INDEX_FILE: path.join(temp, 'index')};
const commits = git(['rev-list', '--reverse', '--topo-order', tip]).toString().trim().split('\n');
const mapped = new Map(), blobs = new Map();
const remove = p => /^(?:bin|obj|\.vs|wwwroot)\//i.test(p) || /\.user$|\.log$|\.pfx$|\.p12$|\.pem$|\.key$/i.test(p)
  || /^(?:ServiceTaskTimings\.txt|MealPlan\.json|JsonFiles\/(?:Dashboard|groceryList|Mealplan_Images|Mealplan_no_images)\.json)$/i.test(p);
for (const [number, commit] of commits.entries()) {
  let tree = git(['rev-parse', commit + '^{tree}']).toString().trim();
  if (commit !== tip) {
    git(['read-tree', commit], undefined, indexEnv);
    const updates = [];
    for (const record of git(['ls-tree', '-rz', '--full-tree', commit]).toString().split('\0').filter(Boolean)) {
      const tab = record.indexOf('\t'), filename = record.slice(tab + 1);
      const [mode, type, hash] = record.slice(0, tab).split(' ');
      if (remove(filename)) { updates.push('0 ' + '0'.repeat(40) + '\t' + filename + '\0'); continue; }
      if (type !== 'blob') continue;
      if (!blobs.has(hash)) {
        const data = git(['cat-file', 'blob', hash]);
        if (!data.includes(0)) {
          const original = data.toString('utf8'), clean = sanitize(original);
          blobs.set(hash, clean === original ? hash : git(['hash-object', '-w', '--stdin'], clean).toString().trim());
        } else {
          if (findings(data.toString('utf8')).length) throw new Error('Credential pattern in binary: ' + filename);
          blobs.set(hash, hash);
        }
      }
      if (blobs.get(hash) !== hash) updates.push(mode + ' ' + blobs.get(hash) + '\t' + filename + '\0');
    }
    if (updates.length) git(['update-index', '-z', '--index-info'], updates.join(''), indexEnv);
    tree = git(['write-tree'], undefined, indexEnv).toString().trim();
  }
  const raw = git(['cat-file', 'commit', commit]).toString();
  const split = raw.indexOf('\n\n'), headers = raw.slice(0, split).split('\n');
  const identity = kind => {
    const value = headers.find(l => l.startsWith(kind + ' '));
    const match = value.match(/^[^ ]+ (.*) <([^>]*)> ([0-9]+ [+-][0-9]+)/);
    return {name: match[1], date: match[3]};
  };
  const author = identity('author'), committer = identity('committer');
  const parents = headers.filter(l => l.startsWith('parent ')).map(l => l.slice(7));
  const args = ['commit-tree', tree, ...parents.flatMap(p => ['-p', mapped.get(p)])];
  const rewritten = git(args, sanitize(raw.slice(split + 2)), {
    GIT_AUTHOR_NAME: author.name, GIT_AUTHOR_EMAIL: 'historical-author@example.invalid', GIT_AUTHOR_DATE: author.date,
    GIT_COMMITTER_NAME: committer.name, GIT_COMMITTER_EMAIL: 'historical-author@example.invalid', GIT_COMMITTER_DATE: committer.date
  }).toString().trim();
  mapped.set(commit, rewritten);
  if ((number + 1) % 10 === 0) console.log('Sanitized ' + (number + 1) + '/' + commits.length + ' commits.');
}
const cleanTip = mapped.get(tip);
cp.execFileSync(process.execPath, ['scripts/scan-secrets.cjs', '--history', cleanTip], {stdio: 'inherit'});
if (git(['rev-parse', cleanTip + '^{tree}']).toString().trim() !== git(['rev-parse', tip + '^{tree}']).toString().trim())
  throw new Error('Tip tree changed unexpectedly.');
git(['update-ref', ref, cleanTip, tip]);
console.log('Publication branch sanitized: ' + commits.length + ' commits. Original refs and reflogs remain PRIVATE.');
