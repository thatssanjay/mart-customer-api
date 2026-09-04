# Order wallet credit API

`POST /api/v1/wallet-engine/orders/{orderId}/credit` has no request body or financial query parameters.
Customer tokens can process only orders owned by their authenticated customer ID. Staff use the existing
authenticated internal-user store assignment; the service checks the order's franchise/store against it.
Ownership/scope is checked before both posting and idempotent replay. Store and financial data always come from the order.
Success is the existing plain JSON response convention; failures use existing ProblemDetails middleware.

## Rules and findings

- Fully **Wallet** paid orders qualify: Wallet is the persisted payment mode for App payments.
  **APP** remains accepted as an alias (case insensitive, trimmed). Payments including Cash/UPI/Card/POS fail.
- The current schema has `FinalPayableAmount`, not `FinalPaidAmount`, and no payment success-status column.
  The API requires `OrderStatus = Paid`, positive persisted payment amounts, settlement references,
  no future payment timestamps, and a payment total exactly equal to `FinalPayableAmount`.
  That reconciled amount is returned as `paidAmount`. It does not independently query a payment gateway.
  Wallet/App settlement writers must establish these authoritative records before calling this endpoint.
- Every active configured wallet receives `Round(paidAmount * PointPercentage / 100)`.
  ConversionRate is **not selected into calculation inputs and never participates in earning**.
  Parent CashbackPercentage and WalletType.OnePointValue do not participate either.
- Money uses decimal and the existing two-decimal AwayFromZero rounding policy.
- Store settings are selected as of the order timestamp, following existing checkout reward conventions.
  Multiple applicable parent settings fail. Missing configuration/active wallet allocations fail.
- Minimum purchase applies to base rewards. Existing subscription eligibility/formula remains independent:
  the most recent ACTIVE subscription valid on the order date contributes
  `Round(paidAmount * ExtraPointPercentage / 100)` to its configured wallet, with subscription expiry.
  It creates a distinct SUBSCRIPTION_BONUS transaction/component even when targeting a base-reward wallet.
- **Cap policy still requires business confirmation:** a positive MaximumCashbackPerOrder caps the combined
  base reward, proportionally allocated across wallets. Residual cents use largest fractional shares,
  tied by wallet/configuration order. Null/zero means no cap. Subscription bonus is outside this cap.
- Child IsNoExpiry gives a non-expiring bucket; otherwise child StartDate/EndDate determine applicability
  and EndDate is the exact UTC bucket expiry. Parent CashbackValidityDays is not substituted for child expiry.
- MART_WALLET is customer/type/store scoped. Every other wallet is customer/type scoped with NULL StoreId.
  Existing inactive wallets fail; they are never replaced by duplicate wallets.
- Orders already credited by legacy checkout (order reward totals or ORDER credit ledger entries) are rejected
  if no completed engine operation exists. Legacy credits are not silently converted to engine replays.
  The legacy checkout stores app payments as Wallet; this endpoint consumes those already-paid orders
  and does not introduce or change settlement/checkout APIs.
- A minimum-purchase/zero-allocation outcome still completes an idempotent NO_REWARD operation.
  Replay uses persisted transaction balances/amounts and does not recalculate using changed configuration.
  Response wallet rows correspond to components; a base reward and bonus may repeat a wallet ID.

## Transaction and database protection

The engine owns one Serializable EF Core transaction covering source/configuration/subscription reads,
wallet resolution/creation, CustomerWallet balance and TotalCredit changes, WalletTransaction,
WalletBalanceBucket, WalletOperation and WalletOperationComponent inserts/completion.
All intermediate SaveChanges stay inside it. Failure rolls back all postings and clears tracked state.
SQL Server deadlocks and known identity conflicts retry the entire operation with fresh state, at most three attempts.

Existing operation business-key uniqueness and canonical-wallet uniqueness remain in force.
The additional `UQ_WalletOperation_Order(OperationKind, CustomerOrderId)` protects order identity even if
an alternate business key is attempted. The database test verifies this separately from HTTP/service checks.

**Manual database deployment is required.** Nothing was applied to application databases and no migrations
were created or executed. Deploy the existing Phase 2 foundation tables, indexes and constraints described in
`WalletEnginePhase2.md`. The manual `Mart.Customer.Persistence/Scripts/WalletEngineFoundation.sql` now creates
the missing operation/component tables and required engine indexes/constraints, including order uniqueness.
For installations that already have the foundation, `OrderWalletCreditIdempotency.sql` remains an index-only option.
The script fails if foundation tables are absent or duplicate order operations exist; it does not alter history.
CashbackSettings.CashbackSettingId/StoreId and CashbackSettingWallet.CashbackSettingId must use BIGINT,
matching the adjacent Mart application's existing identifier alignment. Child WalletTypeID and Id remain INT.
The new child EF mapping targets the existing mart.CashbackSettingWallet table; it does not create a new table in production.

## Files changed by this feature

| File | Purpose |
| --- | --- |
| Mart.Customer.Api/Controllers/V1/WalletEngineController.cs | Thin authenticated OrderId-only endpoint |
| Mart.Customer.Application/Wallets/Engine/WalletEngineService.cs | Order validation, calculation, replay and transactional orchestration |
| Mart.Customer.Application/Wallets/Engine/OrderWalletModels.cs | Server access, source/configuration projections and response contracts |
| Mart.Customer.Application/Abstractions/Data/IOrderWalletSourceRepository.cs | Existing EF repository-style boundary for authoritative inputs |
| Mart.Customer.Application/Cashback/Dtos/CashbackSettingDto.cs | Align store ID with BIGINT |
| Mart.Customer.Domain/Cashback/CashbackConfiguration.cs | Align cashback/store identifiers with existing Mart schema |
| Mart.Customer.Domain/Cashback/CashbackSettingWallet.cs | Existing child configuration entity |
| Mart.Customer.Persistence/Configurations/CashbackSettingWalletConfiguration.cs | Child EF mapping, relationships and configuration uniqueness |
| Mart.Customer.Persistence/Configurations/WalletOperationConfiguration.cs | Unique order-operation index |
| Mart.Customer.Persistence/Repositories/OrderWalletSourceRepository.cs | Async no-tracking EF projections of order/payment/configuration |
| Mart.Customer.Persistence/Repositories/WalletPostingGuard.cs | Recognize order-index race for complete transaction retry |
| Mart.Customer.Persistence/ApplicationDbContext.cs | Child configuration DbSet |
| Mart.Customer.Persistence/DependencyInjection.cs | Register source repository |
| Mart.Customer.Persistence/Scripts/OrderWalletCreditIdempotency.sql | Manual order-index deployment with duplicate preflight |
| Mart.Customer.Tests/Wallets/OrderWalletCreditTests.cs | Relational calculation, eligibility, scope, replay, bonus, cap, expiry, rollback and SQL Server concurrency tests |
| Mart.Customer.Tests/Wallets/OrderWalletCreditEndpointTests.cs | Actual route/body/access integration tests |
| Mart.Customer.Tests/Orders/OrderCheckoutTests.cs | Update existing seed to use BIGINT store ID |
| docs/OrderWalletCredit.md | Contract, assumptions, database deployment and feature manifest |

Pre-existing unrelated working-tree edits were preserved. React and stored procedures were not changed.

## Verification

Commands:

```powershell
dotnet build Mart.Customer.Api.slnx --no-restore
$env:MART_WALLET_LOCALDB_TESTS = '1'
dotnet test Mart.Customer.Tests/Mart.Customer.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Wallets|FullyQualifiedName~OrderCheckout|FullyQualifiedName~Subscriptions|FullyQualifiedName~Cashback'
```

Build succeeded with zero warnings/errors. The relevant suite passed **188 tests, zero failures/skips**,
including the concurrent OrderId request and direct order-index uniqueness tests on SQL Server.
SQL Server tests use uniquely named disposable LocalDB databases
created/deleted by the existing test fixture, never the application's configured database or EF migrations.

## Customer-token 403 correction

The request logged at 2026-09-04 04:57:34 +05:30 returned 403 because the original controller
rejected all customer tokens. Customer calls now carry the authenticated customer ID into the service,
which checks ownership before payment validation, posting or replay. Staff scope validation remains intact.
Customer and store IDs are still not bound from the caller's body or query string.

Changed for this correction: WalletEngineController.cs (derive customer access), OrderWalletModels.cs
(optional authenticated CustomerId), WalletEngineService.cs (validate owner or staff scope),
OrderWalletCreditEndpointTests.cs (customer context), OrderWalletCreditTests.cs (owner success/replay and
cross-customer denial before and after posting), and this document. No schema or transaction behavior changes.

IIS Express/Visual Studio locked the default output DLLs during verification. The corrected solution built
successfully with `dotnet build Mart.Customer.Api.slnx --no-restore -p:OutputPath=bin/WalletAuthFix/`.
The OrderWalletCredit and WalletEngine test suites passed 53 tests with zero failures/skips using that
output folder, with `MART_WALLET_LOCALDB_TESTS=1` (including SQL Server concurrency tests).
Stop debugging, rebuild and restart the normal API instance to load the corrected assemblies.

## Wallet payment-mode correction

WalletEngineService.cs now accepts the persisted `Wallet` payment mode as App payment, retaining `APP`
as an alias. Comparison is case insensitive and trims whitespace. The existing paid-state, settlement
total/reference, ownership, active-configuration and prior-credit checks still apply.
OrderWalletCreditTests.cs now uses Wallet payments for its default fixtures (including concurrency,
rollback and subscription tests), adds mode/alias allocation and replay cases, and rejects unpaid or
partially settled Wallet orders. This document records the corrected mode semantics.
The endpoint, formula, transactional tables and database schema are unchanged; no migration is required
for this correction. Existing unfulfilled deployment prerequisites above still apply.
Verification: isolated-output solution build succeeded with zero warnings/errors; the OrderWalletCredit
and WalletEngine suites passed 59 tests with zero failures/skips, including SQL Server concurrency.

## Missing WalletOperation table (SQL error 208)

The API log confirmed `Invalid object name 'Wallet.WalletOperation'` at the idempotency lookup.
This is a missing database deployment, not a payment or CORS failure. Run
`Mart.Customer.Persistence/Scripts/WalletEngineFoundation.sql` manually in the database configured
for this API. The script creates missing WalletOperation and WalletOperationComponent tables, their
foreign keys and unique indexes, and the foundation's bucket constraints/indexes in one transaction.
It preserves financial rows and rolls back on conflicting historical data. It may be rerun.
The original base wallet tables and store-scope schema are prerequisites.

Files changed: WalletEngineFoundation.sql (new deployable foundation), OrderWalletCreditIdempotency.sql
(align nullable order filtering with EF), WalletEngineSqlServerTests.cs (missing-table deployment and
rerun/post/replay verification), and this document. No endpoint, formula or application transaction
behavior changed. No migration was generated or run; no application database was modified by the agent.
Verification: build succeeded with zero warnings/errors; 36 selected order-credit/SQL Server tests passed,
including deploying into a disposable database with both engine tables removed, posting successfully,
rerunning the script, and verifying idempotent replay without lost or duplicated ledger entries.
