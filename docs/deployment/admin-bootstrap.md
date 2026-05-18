# Admin bootstrap

The SuperAdmin bootstrap seeder is a one-time, environment-gated startup service. It only runs when `SeedAdmin:Enabled` is `true`, creates a SuperAdmin only when none already exists, and logs/continues without crashing the app if configuration or creation fails.

## Configure

Set these values for the production App Service/Key Vault:

- Key Vault secret `SeedAdmin--Email`: Chris's bootstrap admin email.
- Key Vault secret `SeedAdmin--Password`: a temporary bootstrap password.
- App Service setting `SeedAdmin__Enabled`: `true` to run the seeder.

The password must satisfy the Identity policy: at least 12 characters, one digit, one uppercase letter, and one non-alphanumeric character.

## Disable after first login

After logging in as SuperAdmin and bootstrapping tenants, set `SeedAdmin__Enabled` to `false`. Optionally clear or replace the `SeedAdmin--Password` secret so the bootstrap password is no longer available.
