# Frontend compatibility notes

The historical frontend source is absent. These changes intentionally close unsafe account paths and require frontend verification before reconnecting a deployed client.

- Send `X-MealGenius-Client: web` with mutations and include credentials when using the authentication cookie. Stripe webhooks use signature verification instead.
- `ConfirmEmail` requires the Identity token and user ID. A passwordless confirmed account receives a setup token.
- `SetupPassword` and `SetupAccount` require `UserId`, `Token` and `Password`. They only establish a password for a confirmed passwordless account. Setting a username is no longer an anonymous side effect.
- `ResetPassword` requires `UserId`, `Token` and `Password` as defined by the controller request model. It validates an Identity reset token; it never removes a password first.
- `ConfirmAccess` consumes a hashed, expiring payment-access receipt once and checks entitlement.
- Login requires a confirmed account and applies lockout. Resetting a password invalidates old JWT sessions.
- Anonymous deletion, confirmation by email alone, old direct account mutation, unfinished payment endpoints and destructive regeneration endpoints were removed.
- Paid generation is available through `ExecuteTask`; completed or already ongoing tasks are not blindly regenerated. Status reports persisted state/versions rather than invented completion estimates.

Controller attributes and request records in `Controllers/AuthController.cs`, `MainAPI.cs`, `StripeController.cs`, `StatusController.cs` and `MealplanController.cs` are authoritative. Development Swagger provides the executable route/schema reference. No compatibility claim is made for an unavailable frontend.
