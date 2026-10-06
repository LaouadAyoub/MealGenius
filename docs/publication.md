# Public repository safety checklist

**SAFE TO MAKE PUBLIC: NO until external credential rotation is complete.**

- [ ] Complete every applicable action in [SECURITY_ROTATION_REQUIRED.md](../SECURITY_ROTATION_REQUIRED.md), including invalidating old JWTs.
- [ ] Review provider usage/access logs and remove unused old accounts/resources.
- [ ] Run `node scripts/scan-secrets.cjs` and `node scripts/scan-secrets.cjs --history HEAD` on the publication branch. Pattern scanning is a guardrail, not proof that arbitrary credentials cannot remain.
- [ ] Review the candidate diff, metadata, email templates and reference data for information you do not want published.
- [ ] Run restore/build/tests and inspect the documented integration-test limitations.
- [ ] Choose a source license and verify rights to any subsequently added screenshots/assets.
- [ ] Create a **new empty** GitHub repository. Push only the sanitized `portfolio/public-release` branch to its `main` branch after approval to publish.
- [ ] Do not use `--all`, `--mirror` or push old tags/branches. Do not merge the original private history back into the sanitized branch.
- [ ] Keep the original remote repository private. Changing its visibility would expose history outside the sanitized branch.
- [ ] Configure deployment secrets and protect the production environment only when a deployment is intended.

History sanitation does not rotate credentials or remove private originals/reflogs from this checkout. Avoid distributing a ZIP of `.git` or the entire local workspace. The sanitation script is intentionally restricted to an explicitly named clean `portfolio/*` branch and validates the rewritten history before moving that branch.
