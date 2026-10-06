# Architecture and recovery

## Dependency direction

`Program` builds the host; `Startup` registers controllers, Identity/JWT, EF Core, rate limiting, mail, HTTP clients, storage and the RabbitMQ hosted service. Controllers call scoped application services. Generation services use `UserDbContext`, prompt assets, OpenAI and Blob Storage. Entities and persistence mappings share `DataAccess/UserDbContext.cs`; this is not a separately isolated domain layer.

`RabbitMQService`, generation cancellation context and the OpenAI concurrency limiter are singletons. Database contexts and generation services are scoped. The consumer creates a scope per delivery. Concurrent grocery/image stages create separate scopes to avoid sharing an EF context across parallel operations.

## Onboarding and accounts

`Controllers/MainAPI.cs` validates questionnaire input before creating an account, input record and task. Existing accounts receive a generic response instead of allowing anonymous replacement. Confirmation email uses Identity tokens. `AuthController` validates ownership tokens before password setup/reset; access links use stored SHA-256 token hashes and conditional deletion to consume a receipt once. Login uses Identity password checking with lockout.

JWTs contain an Identity security stamp and paid claim. Validation reloads the user, checks confirmation/stamp and rejects stale paid entitlement. Mutation requests need the custom client header; exact-origin CORS and SameSite cookies enforce the browser boundary. This is not a substitute for matching the frontend hosting/cookie topology.

## Payment to background work

`StripeController` verifies the signature and paid checkout details, then takes a PostgreSQL advisory lock for the event ID. A persisted processed-event record skips later replays. Payment entitlement is saved before enqueueing. A payment access email follows; the processed receipt is saved last.

This order allows retry after queue/mail failure but leaves crash windows: publication or email can succeed before the receipt is saved. Repeated delivery can therefore repeat effects. There is no atomic database/broker/mail transaction. Task-level locking and completed-task detection limit duplicate generation, but do not promise exactly-once external calls.

## Generation and checkpoints

`ExecuteTaskService` checks paid ownership and publishes a minimal task/user message. The durable generation queue has a failed queue as its dead-letter destination. Publisher confirms establish broker acceptance, not task completion.

`GenerationJobProcessor` acquires a session advisory lock using a dedicated, non-pooled PostgreSQL connection. It validates task ownership/payment, skips completed tasks, sets `Ongoing`, then runs dashboard → meals → concurrent groceries/images. The deadline and host cancellation flow into external calls. Required documents, grocery version, image status and meal URLs are checked before `Completed`; exceptions set `Failed`.

Grocery JSON is saved before enrichment, so a retry can finish an incomplete enrichment stage. Image batches save successful URLs even if another image fails. Deterministic blob names reduce duplicate objects. These checkpoints are useful recovery aids, not a durable workflow engine; prompt/domain validation is still limited.

The consumer prefetches one delivery and acknowledges only successful processing. Failure dead-letters the message; cancellation during host shutdown requeues it. A process crash before acknowledgement permits redelivery. Real RabbitMQ restart/channel failure/shutdown behavior still needs integration testing.

## Results and operations

User-scoped endpoints retrieve the latest task/results; status/version fields support polling. JSONB holds nested generated documents while relational foreign keys retain ownership and task relationships. Blob URLs carry image references.

NLog writes to console. The restoration removes full prompt/entity logging and timing-file writes. Errors use generic problem responses; external failure logs avoid response bodies and credentials. Distributed tracing, dead-letter alerts, cost metrics and automatic failed-job replay are not implemented.

App Service hosts both API and consumer. Multi-instance deployment shares the broker and database; task locks serialize identical jobs but are not a replacement for an outbox. Blob containers, PostgreSQL, RabbitMQ, email, Stripe and OpenAI accounts must be provisioned separately.
