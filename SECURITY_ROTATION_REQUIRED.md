# Manual credential rotation required

**SAFE TO MAKE PUBLIC: NO.** Removing committed values does not revoke them. Every real credential previously committed must be treated as compromised: **THIS SECRET MUST BE ROTATED.** Do not paste old or replacement values into issues, commits or this document.

| Service / credential | Required external action | Historical source locations |
| --- | --- | --- |
| OpenAI | Revoke exposed keys, create scoped replacement and review usage | `Controllers/MainAPI.cs`, historical compose files |
| Azure Storage | Rotate exposed account keys, update consumers, invalidate dependent access where needed | `appsettings.json` |
| PostgreSQL / Azure database | Change exposed database passwords and review network access | `Startup.cs` comments and historical configuration |
| JWT | Replace signing key; invalidate all previously issued tokens | `appsettings.json` |
| Stripe | Replace exposed webhook signing secrets; review configured endpoints | `appsettings.json`, historical payment/controller code |
| Mailgun | Revoke/rotate API key and review sending activity | `appsettings.json` |
| Google OAuth | Rotate exposed client secret or delete unused OAuth client | `Startup.cs` comments |
| Unsplash / Pixabay | Revoke/rotate exposed application credentials | deleted `Services/ImageService.cs` |
| Custom API authentication | Revoke the old shared API key wherever it was accepted | configuration and deleted API-key middleware |
| Seed/local accounts | Change any real account password matching historical hard-coded values; remove unused seed accounts | deleted `DataAccess/UserDbContextSeeder.cs`, historical local configuration |

Also review RabbitMQ accounts, GitHub Actions secrets and Azure publish profiles. Replace any credentials shared with the historical local/deployment configuration. This review does not claim that every such credential was committed.

Record completion privately in your password manager/provider audit trail. Then inject replacements through local ignored configuration or deployment secret settings. Rewriting Git history cannot establish that credentials are revoked.

The original private branches, remote-tracking refs, reflogs and old remote repository can still contain sensitive data. Publish only the sanitized branch into a new empty repository after completing [the checklist](docs/publication.md). Do not change the old repository's visibility.
