# Verification record

Follow-up checks on October 7, 2026:

- Reproduced the two CI failures locally with PostgreSQL 18: the test history table was implicitly checked in `public`, and the synthetic Stripe event omitted the SDK-required `request` field.
- Explicitly configured the isolated test schema for EF migration history in every test context and added `request: null` to the signed Stripe fixture. Production application behavior and the CI workflow were unchanged.
- Strengthened migration verification to compare all known/applied migrations and run migration application a second time without changes.
- The complete Release suite passed: **21 passed, 0 skipped, 0 failed**, including all three PostgreSQL tests. CI uses PostgreSQL 16; a new remote CI result has not yet been observed.
- Current-file secret scan and whitespace checks passed.

Local restoration checks on October 6, 2026:

- Baseline Release built before edits; the restored solution builds.
- Focused test run: **18 passed, 3 skipped, 0 failed**.
- Skipped tests require `MEALGENIUS_TEST_POSTGRES`: migration/job completion and duplicate handling, grocery checkpoint recovery, and signed Stripe replay processing. CI supplies PostgreSQL; a remote CI result has not been observed.
- Publish succeeded, including prompt/reference/email assets and excluding local/example secret configuration.
- NuGet vulnerability audit reported no known vulnerable direct or transitive packages in either project at the time checked. This is not a general security guarantee.
- Docker is installed but its engine was unavailable, so local PostgreSQL/RabbitMQ end-to-end verification did not run.
- No live OpenAI, Mailgun, Stripe, Blob Storage or Azure deployment call was made.
- Final restore, Release build and publish passed; the Release build reported 229 warnings and zero errors.
- EF generated the full migration SQL offline and reported no pending model changes. This does not establish successful execution against PostgreSQL.
- CI and Compose YAML parsed successfully; local documentation links resolved. The README Mermaid diagram rendered successfully and was visually inspected.
- Publication history sanitation processed 77 commits and preserved the final source tree. Its subsequent scan inspected 568 historical file/blob versions with zero pattern findings. The current tree scan inspected 168 files with zero findings. Original private refs/reflogs were not sanitized.
- No tracked `bin`, `obj`, `node_modules`, `.vs`, local secret configuration or `.csproj.user` artifacts were found in the publication tip.

The tests cover account ownership and removed unsafe routes, paid authorization, input validation, password-reset behavior/session invalidation, finite transient retries, permanent/malformed/truncated AI failures, grocery matching and completion requirements. They do not establish real broker delivery guarantees or full frontend compatibility.

Historical nullable/compiler warnings remain. Full schema validation of AI output, atomic outbox delivery, automatic dead-letter recovery and operational monitoring remain future work. External credential rotation cannot be verified from Git.
