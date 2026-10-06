# Historical baseline and restoration

The working baseline was private `origin/Release`, originally commit `4b87d3170c76df0c0fad86894d68df3601e0dd30` (May 12, 2024, v1.1.3). It was later than private `master` (`1b773cc`, April 26, 2024). Available history begins in June 2023.

The original Release application built before editing. It already contained the controller/service organization, PostgreSQL/Identity persistence, prompt-based generation, RabbitMQ background processing, Blob Storage integration, Stripe/email integration and Azure deployment workflow. The solution also named missing sibling experiments; those references were removed, not recreated.

RabbitMQ is demonstrated by source/history. There is no evidence here of a RabbitMQ-to-Azure-Service-Bus migration. Historical bundled frontend assets support a separate React-based frontend, but do not establish Next.js. Generated frontend bundles/source maps are not retained in the publication history.

Restoration changes include account ownership checks, configuration redaction, manual queue acknowledgements and failed delivery handling, task completion/recovery, bounded current OpenAI transport, Stripe event receipts, focused tests and documentation. These changes are explicitly modern maintenance rather than retroactive claims about the original product.

The publication-history sanitizer preserves commit relationships, dates and author names while redacting credential literals, email addresses and sensitive/generated files. Commit hashes necessarily change. The final sanitized source tree is preserved exactly. Original private refs and reflogs remain available locally and must never be pushed to the public destination.
