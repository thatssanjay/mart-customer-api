# Core Wallet Engine — Phase 2 foundation

## Scope

An internal, already-calculated credit posting service. There is no public engine endpoint,
calculator implementation, checkout/Billing integration, payment-channel change, subscription
fee posting, reversal, refund clawback, or expiry job. `IWalletCalculationService` deliberately
has no implementation or DI registration until financial rules are confirmed.

`WalletPostingRequest` is trusted application input, not an API binding model. The future
payment integration must verify customer/store access, order/payment relationships,
settlement and APP eligibility before constructing it. This foundation validates source
identity shape and replay consistency; it does not assert that a referenced order was paid.

## Posting contract and transaction

Resolve `IWalletEngineService` from the existing application DI scope and call `PostAsync`.
Only `SALE_REWARD` is executable in this phase. The deterministic identity is
`(OperationKind, BusinessKey)`, with `BusinessKey = SALE_REWARD:{CustomerOrderId}`.
Calculation version, amounts and configuration IDs are deliberately excluded from identity.
Reference IDs and audit user are provided by trusted internal callers. No client balance
before/after values are accepted.

The service owns one `IUnitOfWork.ExecuteInTransactionAsync` Serializable transaction:

1. Validate identity and check for a completed operation.
2. On replay verify customer/store/order/payment identity and load persisted result.
3. On first posting validate allocation precision, type, snapshot and canonical component uniqueness.
4. Normalize global scopes and sort by wallet type, store, component and allocation key.
5. Insert the operation, resolve/create each wallet, then post each positive allocation.
6. Post CREDIT ledger entry, exactly one linked bucket and a component snapshot;
   apply `CustomerWallet.Credit` to update CurrentBalance and TotalCredit.
7. Complete and save the operation, then commit all changes together.

Intermediate SaveChanges calls obtain generated IDs and remain inside the same transaction.
Failure rolls back operations, newly created wallets, ledger, buckets, snapshots and balances.
The engine clears tracked state after rollback. There are no external side effects inside this boundary.

`PROCESSING` exists only within an in-flight transaction; successful commits contain
`COMPLETED` with `CREDITED` or `NO_REWARD`. Failed operations are rolled back, not saved as
misleading FAILED rows. Empty/zero-only allocations create a completed NO_REWARD operation,
without wallets, transactions, buckets or components. A later changed amount cannot earn again.

Negative or overprecision allocations are rejected. Ledger posting does not round. The
central `WalletRoundingPolicy.RoundAmount` preserves decimal, scale 2, AwayFromZero; it is
available for future calculations and precision validation only.

## Canonical wallets and concurrency

`CustomerWalletResolver` loads WalletType internally and uses `WalletTypeCodes.MartWallet`.
MART_WALLET requires a positive store; all other types normalize to null. Existing inactive
wallets fail with DomainException and never cause a replacement wallet. The existing
unfiltered unique customer/type/store index is unchanged.

The new engine requires a clean relational DbContext and owns its outer transaction.
It rejects ambient EF/System.Transactions transactions, unsaved caller changes, and a
provider strategy configured for implicit retries. It clears unmodified tracked entities
on entry to avoid stale wallet balances. Resolver/ledger require a Serializable transaction;
the ledger also requires entities tracked by that context. These lower-level services are
for the engine's posting boundary, not standalone application mutation entry points.

Retries are bounded to three complete attempts. Only SQL Server deadlock 1205 and unique-key
violations naming the operation identity/number, customer-wallet identity or transaction-number
indexes are retryable. EF exception wrappers are inspected for the underlying SqlException.
Component/bucket duplicate constraints and unrelated DbUpdateException failures are not swallowed.
Each retry follows rollback and ChangeTracker.Clear; it reloads operation and wallet state.
No distributed locks or partial-posting retries are introduced. Existing UnitOfWork is unchanged.

Connection/commit-ambiguity failures are surfaced rather than blindly retried. A new caller
attempt with the same business key reads any completed operation and returns its persisted
result. Caller cancellation is not retried. After retry exhaustion, the operation fails cleanly.
Future joining with checkout requires an explicit transaction-ownership design; it is currently rejected.

## Audit and replay

WalletOperation is a business operation record, not a second ledger.
WalletOperationComponent links one operation to one ledger credit, with unique identity:

`WalletOperationId + CustomerWalletId + ComponentCode + AllocationKey`.

BASE_REWARD and SUBSCRIPTION_BONUS can target the same wallet without colliding. AllocationKey
defaults to DEFAULT; multiple base allocations require distinct stable uppercase ASCII keys.
Operation/component keys are validated before writing. Snapshot metadata is a versioned
immutable JSON value containing optional configuration IDs, percentage/rate, calculation
base/rule, before-conversion/cap values, rounded amount, subscription data, conversion rule,
expiry rule and rounding policy. Source identities, effective timestamp and calculation version
live on the operation; wallet identity and actual expiry live on the component. RoundedAmount
is set to the actual posted amount, and a contradictory supplied amount is rejected.
No calculation semantics are inferred from these fields. Null means not supplied.

Replay projects recorded operation/component/transaction values, never current configuration,
wallet balance or cumulative totals. Later credits cannot change an earlier result.
StoreId and WalletTypeId are snapshotted on the component; expiry is not reconstructed from
mutable bucket state. `CustomerWalletTransaction` is neither introduced nor written.

DbContext SaveChanges guards reject ledger/component updates/deletes and changes/deletion
of completed operations. These protect tracked EF writes; they do not claim protection against
privileged external SQL or ExecuteUpdate/Delete bypasses. No such engine paths are introduced.
Database grants for external writers remain a deployment/security responsibility.

## Migration-ready schema; nothing applied to application databases

No migrations were generated or applied. Before deployment, separately review/apply:

- New Wallet.WalletOperation table; unique OperationNumber and OperationKind/BusinessKey;
  order/payment lookup indexes; status/outcome/completion consistency check.
- New Wallet.WalletOperationComponent table; restrictive FKs to operation, wallet and ledger;
  unique operation/wallet/component/allocation key; unique WalletTransactionId (one-to-one);
  component snapshot JSON and expiry/scope snapshot columns. The composite identity index
  also supports operation lookups, avoiding a redundant operation-only index.
- WalletBalanceBucket unique SourceTransactionId and CustomerWalletId/ExpiryDate index;
  checks OriginalAmount > 0 and 0 <= AvailableAmount <= OriginalAmount.
- Keep existing wallet decimal(18,2) columns and customer-wallet unique index unchanged.
- Preflight historical duplicate buckets and invalid amounts before enabling new constraints;
  do not silently delete or merge financial data. Confirm the previously supplied wallet-scope
  index/schema is deployed. Existing application database state was not altered by this phase.

The SQL Server integration tests create/drop uniquely named `MartWalletFoundationTests_*`
databases on `(localdb)\MSSQLLocalDB` using EnsureCreated, not migrations. Cleanup verifies
the exact owned database and server. They never read application connection settings.

## Existing credit API security TODO — before Phase 3 integration

`POST /api/wallets/credit` still accepts caller-selected customer, wallet type, amount,
reference and expiry under broad authenticated access. No unambiguous dedicated posting
role was found, so this phase does not change its authorization or contract.

It must not remain an unrestricted alternative sales reward path: it bypasses sale eligibility,
APP-payment verification, configuration/subscription calculations and engine idempotency.
Before integrating the engine, agree on authorized internal posting roles/capabilities, restrict
this endpoint accordingly and distinguish approved manual credits from automated sale rewards.
Do not expose WalletPostingRequest as a workaround public API.

Existing checkout reward formulas, customer-controlled PAID flow, cashback identifier/model
drift, refund totals semantics and expiry-summary behavior remain unchanged and need the
previous discovery report's follow-up work. Existing refund is a redemption restoration,
not a sale reward reversal.

## Tests and commands

Foundation tests use the real EF repositories, UnitOfWork and services with SQLite, including
scope resolution, existing/inactive wallets, exact credit invariants, separate components,
zero/invalid amounts, clean/ambient context guards, immutable history, persisted replay,
duplicate identities and rollback after the first saved allocation. Rounding tests use decimal.

Opt-in SQL Server tests exercise concurrent same-order replay, different orders concurrently
creating/updating canonical wallets, multi-allocation rollback, and actual operation/component/
bucket/customer-wallet unique constraints. SQL Server test execution is required to substantiate
SQL Server concurrency; EF InMemory tests are not used for that claim.

```powershell
dotnet build Mart.Customer.Api.slnx --no-restore
$env:MART_WALLET_LOCALDB_TESTS = '1'
dotnet test Mart.Customer.Tests/Mart.Customer.Tests.csproj --no-restore
```

Without opt-in (or off Windows), three LocalDB tests explicitly skip.

## Phase 3 decisions still required

- PointPercentage base, parent CashbackPercentage participation and allocation totals.
- ConversionRate units/direction and zero/null behavior.
- Subscription bonus formula and destination/overlap policy.
- Cap order and rounding residual distribution.
- Child validity versus bucket expiry, timezone and boundary behavior.
- Split-payment reward eligibility and authoritative APP settlement evidence.
- Authorized callers and restrictions on the existing credit endpoint.
- Historical reward transition and schema/index deployment approval.

No unresolved formula is implemented to satisfy a foundation test.

## Phase 2 file manifest

Created (paths relative to the solution root):

| File | Purpose |
| --- | --- |
| Mart.Customer.Domain/Wallets/WalletEngineCodes.cs | Controlled wallet scope, operation, status, outcome and component constants |
| Mart.Customer.Domain/Wallets/WalletOperation.cs | Business identity, audit fields and transactional completion |
| Mart.Customer.Domain/Wallets/WalletOperationComponent.cs | Immutable component/ledger association and snapshot |
| Mart.Customer.Domain/Wallets/WalletCalculationSnapshot.cs | Versioned immutable calculation metadata |
| Mart.Customer.Application/Abstractions/Data/IWalletOperationRepository.cs | Operation persistence and posting-guard contracts |
| Mart.Customer.Application/Wallets/Engine/WalletEngineModels.cs | Internal posting, calculation and persisted-result contracts; calculator interface only |
| Mart.Customer.Application/Wallets/Engine/WalletPostingRequestValidator.cs | FluentValidation identity checks and component validation |
| Mart.Customer.Application/Wallets/Engine/WalletRoundingPolicy.cs | Central decimal scale-2 AwayFromZero policy |
| Mart.Customer.Application/Wallets/Engine/CustomerWalletResolver.cs | ICustomerWalletResolver and canonical resolution implementation |
| Mart.Customer.Application/Wallets/Engine/WalletLedgerService.cs | IWalletLedgerService and credit posting implementation |
| Mart.Customer.Application/Wallets/Engine/WalletEngineService.cs | IWalletEngineService, replay, orchestration and bounded complete retries |
| Mart.Customer.Persistence/Repositories/WalletOperationRepository.cs | Async EF operation/component persistence and result projection |
| Mart.Customer.Persistence/Repositories/WalletPostingGuard.cs | Transaction/context validation and narrow SQL conflict classification |
| Mart.Customer.Persistence/Configurations/WalletOperationConfiguration.cs | Operation table, checks and unique/look-up indexes |
| Mart.Customer.Persistence/Configurations/WalletOperationComponentConfiguration.cs | Component table, FKs and unique identities |
| Mart.Customer.Tests/Wallets/WalletEngineFoundationTests.cs | 21 SQLite/domain test cases and relational fixture |
| Mart.Customer.Tests/Wallets/WalletEngineSqlServerTests.cs | 3 opt-in LocalDB integration cases |
| docs/WalletEnginePhase2.md | Architecture, deployment, security and completion report |

Modified by this phase (pre-existing unrelated working-tree changes are not included):

| File | Phase 2 change |
| --- | --- |
| Mart.Customer.Application/DependencyInjection.cs | Register engine, ledger and resolver |
| Mart.Customer.Persistence/DependencyInjection.cs | Register operation repository and guard |
| Mart.Customer.Persistence/ApplicationDbContext.cs | New DbSets and tracked-write audit immutability guards |
| Mart.Customer.Persistence/Configurations/WalletBalanceBucketConfiguration.cs | Source uniqueness, expiry index and amount checks |
| Mart.Customer.Application/Wallets/Commands/ProvisionCustomerWallets/ProvisionCustomerWalletsCommandHandler.cs | Replace scope string with central constant |
| Mart.Customer.Application/Wallets/Commands/EnsureStoreWallet/EnsureStoreWalletCommandHandler.cs | Replace scope string with central constant |
| Mart.Customer.Persistence/Repositories/CustomerWalletRepository.cs | Replace scope strings with central constant |
| Mart.Customer.Persistence/Repositories/WalletBalanceBucketRepository.cs | Replace scope string with central constant |
| Mart.Customer.Tests/Orders/OrderCheckoutTests.cs | Seed buckets only for positive opening balances, matching the new check constraint |

No endpoints were added or changed. Existing credit API authorization remains unchanged;
its required security follow-up is documented above. No EF migrations or SQL deployment scripts
were generated. Existing CustomerWallet and CustomerWalletConfiguration working-tree edits
predate this phase and were preserved.

## Verification findings

- Solution build: succeeded, zero warnings and zero errors.
- Relevant wallet/checkout/cart/subscription suite with LocalDB enabled: 235 passed.
- Full suite without LocalDB opt-in: 322 passed, 10 failed, 3 explicitly skipped SQL Server tests.
- The same 10 failures reproduced in a separate copy with Phase 2 infrastructure removed
  (36 selected baseline tests: 26 passed, 10 failed). They concern invoice generation/download
  expectations and inventory stock-in endpoints, and are not repaired in this wallet phase.
- The new positive-bucket constraint exposed two zero-balance checkout seed fixtures;
  the shared seed helper was corrected and both checkout reward tests now pass.
- SQL Server testing exposed EF's wrapped deadlock exception; complete-operation retry
  classification was corrected and the SQL Server concurrent-posting tests now pass.

Baseline failures:

1. InvoiceServiceTests.GetInvoice_WhenGenerating_UsesHistoricalTemplateAndOrderSnapshots
2. GetOrderDetailEndpointTests.GetInvoice_WhenMobileCustomerDoesNotOwnOrder_ReturnsForbiddenWithoutDocumentGeneration
3. GetOrderDetailEndpointTests.GetInvoicePdf_Inline_StreamsTheExistingPdfForBrowserDisplay
4. GetOrderDetailEndpointTests.GetInvoicePdf_Attachment_DownloadsTheSameExistingPdf
5. InventoryStockInEndpointTests.StockIn_ClientSuppliedPreviousAndNewQuantities_AreIgnored
6. InventoryStockInEndpointTests.StockIn_DerivesContextAndQuantitiesAndCreatesMovement
7. InventoryStockInEndpointTests.StockIn_AdjustmentAdd_CreatesAdjustmentInSameTransaction
8. InventoryStockInEndpointTests.StockIn_ConcurrentRequests_DoNotLoseQuantityOrAuditMovements
9. InventoryStockInEndpointTests.StockIn_BatchManagedProduct_UpdatesStoreAndBatchTogether
10. InventoryStockInEndpointTests.StockIn_WhenSaveCompletionFails_RollsBackEveryInventoryWrite

## PHASE 2 IMPLEMENTATION STATUS

COMPLETE for the internal posting foundation. Deployment remains pending the explicitly
deferred schema/index rollout; Phase 3 calculation and payment integration remain blocked
on the business decisions listed above. The complete existing solution test suite is not green
because of the ten independently reproduced baseline failures.
