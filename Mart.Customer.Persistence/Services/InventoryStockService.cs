using FluentValidation;
using System.Globalization;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Inventory.Commands.StockIn;
using Mart.Customer.Application.Inventory.Commands.StockOut;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Application.Inventory.Services;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Services;

public sealed class InventoryStockService : IInventoryStockService
{
    private static readonly HashSet<string> AllowedMovementTypes = new(StringComparer.Ordinal)
    {
        "PURCHASE",
        "OPENING",
        "TRANSFER_IN",
        "SALE_RETURN",
        "ADJUSTMENT_ADD"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<StockInCommand> _validator;
    private readonly IValidator<StockOutCommand> _stockOutValidator;

    public InventoryStockService(
        ApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IValidator<StockInCommand> validator,
        IValidator<StockOutCommand> stockOutValidator)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _stockOutValidator = stockOutValidator;
    }

    public async Task<StockInResultDto> StockInAsync(
        StockInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        return await _unitOfWork.ExecuteInTransactionAsync(
            token => ExecuteStockInAsync(command, token),
            cancellationToken);
    }

    public async Task EnsureCartQuantityAvailableAsync(
        long productId,
        long franchiseId,
        long martStoreId,
        decimal requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        if (requestedQuantity <= 0)
        {
            throw new DomainException("Cart quantity must be greater than zero.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.ProductId == productId)
            .Select(product => new
            {
                product.IsActive,
                product.IsStockManaged
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException($"Product {productId} was not found.");

        if (!product.IsActive)
        {
            throw new DomainException($"Product {productId} is inactive and cannot be sold.");
        }

        if (!product.IsStockManaged)
        {
            throw new DomainException($"Product {productId} is not stock managed.");
        }

        var storeStock = await _dbContext.StoreStocks
            .AsNoTracking()
            .Where(stock =>
                stock.ProductId == productId &&
                stock.FranchiseId == franchiseId &&
                stock.MartStoreId == martStoreId)
            .Select(stock => new
            {
                stock.CurrentQuantity,
                stock.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (storeStock is null || !storeStock.IsActive)
        {
            throw new DomainException($"Active store stock was not found for product {productId}.");
        }

        if (requestedQuantity <= storeStock.CurrentQuantity)
        {
            return;
        }

        var formattedQuantity = storeStock.CurrentQuantity.ToString(
            "0.###",
            CultureInfo.InvariantCulture);
        throw new DomainException(
            $"Only {formattedQuantity} units are currently available in stock.");
    }

    public Task DeductSaleStockAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default)
    {
        if (customerOrderId <= 0)
        {
            throw new DomainException("A persisted customer order is required for sale stock deduction.");
        }

        return _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await ExecuteSaleStockDeductionAsync(customerOrderId, token);
                return true;
            },
            cancellationToken);
    }

    public async Task<StockOutResultDto> StockOutAsync(
        StockOutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _stockOutValidator.ValidateAndThrowAsync(command, cancellationToken);

        return await _unitOfWork.ExecuteInTransactionAsync(
            token => ExecuteStockOutAsync(command, token),
            cancellationToken);
    }

    private async Task ExecuteSaleStockDeductionAsync(
        long customerOrderId,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(item => item.CustomerOrderId == customerOrderId)
            .Select(item => new
            {
                item.CustomerOrderId,
                item.FranchiseId,
                item.MartStoreId,
                item.CreatedBy,
                Items = item.Items.Select(orderItem => new
                {
                    orderItem.ProductId,
                    orderItem.Quantity
                }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("The persisted customer order was not found.");

        if (order.Items.Count == 0)
        {
            throw new DomainException("The persisted customer order does not contain any items.");
        }

        var requirements = order.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new SaleStockRequirement(
                group.Key,
                group.Sum(item => item.Quantity)))
            .OrderBy(item => item.ProductId)
            .ToList();

        if (requirements.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
        {
            throw new DomainException("Every persisted order item must have a valid product and quantity greater than zero.");
        }

        var existingMovements = await _dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementType == "SALE" &&
                movement.ReferenceType == "ORDER" &&
                movement.ReferenceId == customerOrderId)
            .Select(movement => new
            {
                movement.ProductId,
                movement.Quantity
            })
            .ToListAsync(cancellationToken);

        if (existingMovements.Count > 0)
        {
            var existingByProduct = existingMovements
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            var isComplete = existingByProduct.Count == requirements.Count &&
                requirements.All(item =>
                    existingByProduct.TryGetValue(item.ProductId, out var quantity) &&
                    quantity == item.Quantity);

            if (!isComplete)
            {
                throw new DomainException("The order has an incomplete or inconsistent SALE stock movement history.");
            }

            return;
        }

        var productIds = requirements.Select(item => item.ProductId).ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.ProductId))
            .Select(product => new SaleProduct(
                product.ProductId,
                product.IsActive,
                product.IsStockManaged,
                product.IsBatchApplicable,
                product.IsExpiryApplicable))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        if (products.Count != requirements.Count)
        {
            throw new DomainException("One or more persisted order products were not found.");
        }

        foreach (var requirement in requirements)
        {
            var product = products[requirement.ProductId];
            if (!product.IsActive)
            {
                throw new DomainException($"Product {requirement.ProductId} is inactive and cannot be sold.");
            }

            if (!product.IsStockManaged)
            {
                throw new DomainException($"Product {requirement.ProductId} is not stock managed.");
            }
        }

        var storeStocks = await _dbContext.StoreStocks
            .Where(stock =>
                stock.FranchiseId == order.FranchiseId &&
                stock.MartStoreId == order.MartStoreId &&
                productIds.Contains(stock.ProductId))
            .ToDictionaryAsync(stock => stock.ProductId, cancellationToken);

        foreach (var requirement in requirements)
        {
            if (!storeStocks.TryGetValue(requirement.ProductId, out var stock) || !stock.IsActive)
            {
                throw new DomainException($"Active store stock was not found for product {requirement.ProductId}.");
            }

            if (requirement.Quantity > stock.CurrentQuantity)
            {
                throw new DomainException($"Insufficient store stock for product {requirement.ProductId}.");
            }
        }

        var batchProductIds = products.Values
            .Where(product => product.IsBatchApplicable || product.IsExpiryApplicable)
            .Select(product => product.ProductId)
            .ToList();
        var batchStocks = batchProductIds.Count == 0
            ? []
            : await _dbContext.ProductBatchStocks
                .Where(stock =>
                    stock.FranchiseId == order.FranchiseId &&
                    stock.MartStoreId == order.MartStoreId &&
                    batchProductIds.Contains(stock.ProductId) &&
                    stock.IsActive &&
                    stock.Quantity > 0)
                .ToListAsync(cancellationToken);

        var allocations = new Dictionary<long, IReadOnlyList<BatchAllocation>>();
        var today = DateTime.UtcNow.Date;
        foreach (var requirement in requirements.Where(item => batchProductIds.Contains(item.ProductId)))
        {
            var product = products[requirement.ProductId];
            var eligibleBatches = batchStocks
                .Where(batch =>
                    batch.ProductId == requirement.ProductId &&
                    (!product.IsExpiryApplicable ||
                     (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date > today)))
                .OrderBy(batch => product.IsExpiryApplicable ? batch.ExpiryDate : null)
                .ThenBy(batch => batch.ManufacturingDate ?? batch.CreatedOn)
                .ThenBy(batch => batch.CreatedOn)
                .ThenBy(batch => batch.ProductBatchStockId)
                .ToList();

            if (eligibleBatches.Sum(batch => batch.Quantity) < requirement.Quantity)
            {
                throw new DomainException($"Insufficient sellable batch stock for product {requirement.ProductId}.");
            }

            var remaining = requirement.Quantity;
            var productAllocations = new List<BatchAllocation>();
            foreach (var batch in eligibleBatches)
            {
                var quantity = Math.Min(remaining, batch.Quantity);
                if (quantity > 0)
                {
                    productAllocations.Add(new BatchAllocation(batch, quantity));
                    remaining -= quantity;
                }

                if (remaining == 0)
                {
                    break;
                }
            }

            allocations[requirement.ProductId] = productAllocations;
        }

        var occurredOn = DateTime.UtcNow;
        foreach (var requirement in requirements)
        {
            var storeStock = storeStocks[requirement.ProductId];
            var previousQuantity = storeStock.CurrentQuantity;
            storeStock.Remove(requirement.Quantity, order.CreatedBy, occurredOn);

            if (allocations.TryGetValue(requirement.ProductId, out var productAllocations))
            {
                foreach (var allocation in productAllocations)
                {
                    allocation.Batch.Remove(allocation.Quantity, order.CreatedBy, occurredOn);
                }
            }

            var movement = StockMovement.CreateSale(
                order.FranchiseId,
                order.MartStoreId,
                requirement.ProductId,
                order.CustomerOrderId,
                requirement.Quantity,
                previousQuantity,
                storeStock.CurrentQuantity,
                order.CreatedBy,
                occurredOn);
            await _dbContext.StockMovements.AddAsync(movement, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<StockOutResultDto> ExecuteStockOutAsync(
        StockOutCommand command,
        CancellationToken cancellationToken)
    {
        // A transient SQL Server failure can replay the serializable transaction.
        _dbContext.ChangeTracker.Clear();

        var scope = await (
                from user in _dbContext.Users.AsNoTracking()
                join access in _dbContext.UserMartAccesses.AsNoTracking()
                    on user.UserId equals access.UserId
                join store in _dbContext.MartStores.AsNoTracking()
                    on access.MartStoreId equals (long?)store.StoreId
                where user.UserId == command.UserId &&
                      user.IsActive &&
                      access.CanAccess &&
                      access.FranchiseId.HasValue &&
                      access.FranchiseId > 0 &&
                      access.MartStoreId.HasValue &&
                      access.MartStoreId > 0 &&
                      store.IsActive &&
                      store.FranchiseId == access.FranchiseId.Value
                orderby access.UserMartAccessId
                select new
                {
                    FranchiseId = access.FranchiseId.GetValueOrDefault(),
                    MartStoreId = access.MartStoreId.GetValueOrDefault()
                })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(
                "The authenticated user does not have an active franchise and store assignment.");

        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(item => item.ProductId == command.ProductId)
            .Select(item => new
            {
                item.ProductId,
                item.IsActive,
                item.IsStockManaged,
                item.IsBatchApplicable,
                item.IsExpiryApplicable
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Product not found.");

        if (!product.IsActive)
        {
            throw new DomainException("Inactive products cannot have stock removed.");
        }

        if (!product.IsStockManaged)
        {
            throw new DomainException("The product is not stock managed.");
        }

        var movementType = command.MovementType.Trim().ToUpperInvariant();
        if (movementType is not ("DAMAGE" or "EXPIRED" or "ADJUSTMENT_REMOVE"))
        {
            throw new DomainException(
                "Manual stock-out movement type must be DAMAGE, EXPIRED, or ADJUSTMENT_REMOVE. SALE is not allowed.");
        }

        var batchNumber = Normalize(command.BatchNumber);
        var reason = command.Reason.Trim();
        var remarks = Normalize(command.Remarks);
        var requiresBatch = product.IsBatchApplicable || product.IsExpiryApplicable;
        if (requiresBatch && batchNumber is null)
        {
            throw new DomainException("Batch number is required for this product.");
        }

        if (!requiresBatch && batchNumber is not null)
        {
            throw new DomainException("Batch number is not applicable to this product.");
        }

        if (movementType == "EXPIRED" && !product.IsExpiryApplicable)
        {
            throw new DomainException("EXPIRED is only valid for expiry-managed products.");
        }

        var storeStock = await _dbContext.StoreStocks
            .SingleOrDefaultAsync(stock =>
                stock.FranchiseId == scope.FranchiseId &&
                stock.MartStoreId == scope.MartStoreId &&
                stock.ProductId == product.ProductId,
                cancellationToken);
        if (storeStock is null || !storeStock.IsActive)
        {
            throw new DomainException("Active store stock was not found for the authenticated store.");
        }

        var previousQuantity = storeStock.CurrentQuantity;
        if (command.Quantity > previousQuantity)
        {
            throw new DomainException("Insufficient store stock.");
        }

        ProductBatchStock? batchStock = null;
        decimal? previousBatchQuantity = null;
        decimal? newBatchQuantity = null;
        if (requiresBatch)
        {
            batchStock = await _dbContext.ProductBatchStocks
                .SingleOrDefaultAsync(stock =>
                    stock.FranchiseId == scope.FranchiseId &&
                    stock.MartStoreId == scope.MartStoreId &&
                    stock.ProductId == product.ProductId &&
                    stock.BatchNumber == batchNumber,
                    cancellationToken);
            if (batchStock is null || !batchStock.IsActive)
            {
                throw new DomainException(
                    "Active product batch was not found for the authenticated store.");
            }

            previousBatchQuantity = batchStock.Quantity;
            if (command.Quantity > previousBatchQuantity.Value)
            {
                throw new DomainException("Insufficient batch stock.");
            }

            if (movementType == "EXPIRED" &&
                (!batchStock.ExpiryDate.HasValue || batchStock.ExpiryDate.Value.Date > DateTime.UtcNow.Date))
            {
                throw new DomainException("The selected batch has not expired.");
            }
        }

        var occurredOn = DateTime.UtcNow;
        var newQuantity = previousQuantity - command.Quantity;
        var adjustment = StockAdjustment.CreateRemoval(
            scope.FranchiseId,
            scope.MartStoreId,
            product.ProductId,
            movementType,
            command.Quantity,
            reason,
            command.UserId,
            occurredOn);
        await _dbContext.StockAdjustments.AddAsync(adjustment, cancellationToken);

        storeStock.Remove(command.Quantity, command.UserId, occurredOn);
        if (batchStock is not null)
        {
            batchStock.Remove(command.Quantity, command.UserId, occurredOn);
            newBatchQuantity = batchStock.Quantity;
        }

        var movement = StockMovement.CreateStockOut(
            scope.FranchiseId,
            scope.MartStoreId,
            product.ProductId,
            movementType,
            command.Quantity,
            previousQuantity,
            newQuantity,
            remarks ?? reason,
            command.UserId,
            occurredOn);
        await _dbContext.StockMovements.AddAsync(movement, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StockOutResultDto(
            storeStock.StoreStockId,
            batchStock?.ProductBatchStockId,
            movement.StockMovementId,
            adjustment.StockAdjustmentId,
            product.ProductId,
            scope.FranchiseId,
            scope.MartStoreId,
            command.Quantity,
            previousQuantity,
            newQuantity,
            previousBatchQuantity,
            newBatchQuantity,
            movementType,
            occurredOn);
    }

    private async Task<StockInResultDto> ExecuteStockInAsync(
        StockInCommand command,
        CancellationToken cancellationToken)
    {
        // The SQL Server execution strategy may replay this entire operation after a
        // transient concurrency failure. Never retain entities from the failed attempt.
        _dbContext.ChangeTracker.Clear();

        var scope = await (
                from user in _dbContext.Users.AsNoTracking()
                join access in _dbContext.UserMartAccesses.AsNoTracking()
                    on user.UserId equals access.UserId
                join store in _dbContext.MartStores.AsNoTracking()
                    on access.MartStoreId equals (long?)store.StoreId
                where user.UserId == command.UserId &&
                      user.IsActive &&
                      access.CanAccess &&
                      access.FranchiseId.HasValue &&
                      access.FranchiseId > 0 &&
                      access.MartStoreId.HasValue &&
                      access.MartStoreId > 0 &&
                      store.IsActive &&
                      store.FranchiseId == access.FranchiseId.Value
                orderby access.UserMartAccessId
                select new
                {
                    FranchiseId = access.FranchiseId.GetValueOrDefault(),
                    MartStoreId = access.MartStoreId.GetValueOrDefault()
                })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(
                "The authenticated user does not have an active franchise and store assignment.");

        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(item => item.ProductId == command.ProductId)
            .Select(item => new
            {
                item.ProductId,
                item.IsActive,
                item.IsStockManaged,
                item.IsBatchApplicable,
                item.IsExpiryApplicable,
                item.DefaultPurchasePrice,
                item.DefaultSellingPrice,
                item.MRP,
                item.MaximumQuantity
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Product not found.");

        if (!product.IsActive)
        {
            throw new DomainException("Inactive products cannot receive stock.");
        }

        if (!product.IsStockManaged)
        {
            throw new DomainException("The product is not stock managed.");
        }

        var movementType = command.MovementType.Trim().ToUpperInvariant();
        if (!AllowedMovementTypes.Contains(movementType))
        {
            throw new DomainException(
                "Movement type must be PURCHASE, OPENING, TRANSFER_IN, SALE_RETURN, or ADJUSTMENT_ADD.");
        }

        var referenceType = Normalize(command.ReferenceType)?.ToUpperInvariant();
        var batchNumber = Normalize(command.BatchNumber);
        var remarks = Normalize(command.Remarks);
        var adjustmentReason = Normalize(command.AdjustmentReason);
        var today = DateTime.UtcNow.Date;

        if (product.IsBatchApplicable && batchNumber is null)
        {
            throw new DomainException("Batch number is required for this product.");
        }

        if (product.IsExpiryApplicable && !command.ExpiryDate.HasValue)
        {
            throw new DomainException("Expiry date is required for this product.");
        }

        var manufacturingDate = command.ManufacturingDate?.Date;
        var expiryDate = command.ExpiryDate?.Date;
        if (manufacturingDate > today)
        {
            throw new DomainException("Manufacturing date cannot be in the future.");
        }

        if (expiryDate.HasValue && expiryDate.Value <= today)
        {
            throw new DomainException("Expiry date must be in the future.");
        }

        if (manufacturingDate.HasValue && expiryDate.HasValue &&
            manufacturingDate.Value > expiryDate.Value)
        {
            throw new DomainException("Manufacturing date cannot be after expiry date.");
        }

        if (movementType == "ADJUSTMENT_ADD" && adjustmentReason is null)
        {
            throw new DomainException("Adjustment reason is required for ADJUSTMENT_ADD.");
        }

        if (movementType != "ADJUSTMENT_ADD" && adjustmentReason is not null)
        {
            throw new DomainException("Adjustment reason is only valid for ADJUSTMENT_ADD.");
        }

        var purchasePrice = command.PurchasePrice ?? product.DefaultPurchasePrice;
        var sellingPrice = command.SellingPrice ?? product.DefaultSellingPrice;
        var mrp = command.Mrp ?? product.MRP;
        if (sellingPrice > mrp)
        {
            throw new DomainException("Selling price cannot exceed MRP.");
        }

        var occurredOn = DateTime.UtcNow;
        var storeStock = await _dbContext.StoreStocks
            .SingleOrDefaultAsync(stock =>
                stock.FranchiseId == scope.FranchiseId &&
                stock.MartStoreId == scope.MartStoreId &&
                stock.ProductId == product.ProductId,
                cancellationToken);
        var previousQuantity = storeStock?.CurrentQuantity ?? 0m;
        decimal newQuantity;
        try
        {
            newQuantity = checked(previousQuantity + command.Quantity);
        }
        catch (OverflowException)
        {
            throw new DomainException("The resulting stock quantity is too large.");
        }

        if (newQuantity > 999999999999999.999m)
        {
            throw new DomainException("The resulting stock quantity is too large.");
        }

        if (newQuantity > product.MaximumQuantity)
        {
            throw new DomainException(
                $"Maximum stock limit exceeded. Maximum allowed quantity for this product is {product.MaximumQuantity}. " +
                $"Current stock is {previousQuantity}; requested quantity is {command.Quantity}.");
        }

        if (storeStock is null)
        {
            storeStock = StoreStock.Create(
                scope.FranchiseId,
                scope.MartStoreId,
                product.ProductId,
                command.Quantity,
                purchasePrice,
                sellingPrice,
                command.UserId,
                occurredOn);
            await _dbContext.StoreStocks.AddAsync(storeStock, cancellationToken);
        }
        else
        {
            storeStock.Add(
                command.Quantity,
                purchasePrice,
                sellingPrice,
                command.UserId,
                occurredOn);
        }

        ProductBatchStock? batchStock = null;
        decimal? previousBatchQuantity = null;
        decimal? newBatchQuantity = null;
        if (product.IsBatchApplicable || product.IsExpiryApplicable)
        {
            batchStock = await _dbContext.ProductBatchStocks
                .SingleOrDefaultAsync(stock =>
                    stock.FranchiseId == scope.FranchiseId &&
                    stock.MartStoreId == scope.MartStoreId &&
                    stock.ProductId == product.ProductId &&
                    stock.BatchNumber == batchNumber,
                    cancellationToken);

            if (batchStock is not null)
            {
                if (!batchStock.IsActive)
                {
                    throw new DomainException("The product batch is inactive.");
                }

                if (manufacturingDate.HasValue && batchStock.ManufacturingDate != manufacturingDate)
                {
                    throw new DomainException("Manufacturing date does not match the existing batch.");
                }

                if (expiryDate.HasValue && batchStock.ExpiryDate != expiryDate)
                {
                    throw new DomainException("Expiry date does not match the existing batch.");
                }

                previousBatchQuantity = batchStock.Quantity;
                batchStock.Add(
                    command.Quantity,
                    purchasePrice,
                    sellingPrice,
                    mrp,
                    command.UserId,
                    occurredOn);
                newBatchQuantity = batchStock.Quantity;
            }
            else
            {
                previousBatchQuantity = 0m;
                newBatchQuantity = command.Quantity;
                batchStock = ProductBatchStock.Create(
                    scope.FranchiseId,
                    scope.MartStoreId,
                    product.ProductId,
                    batchNumber,
                    manufacturingDate,
                    expiryDate,
                    command.Quantity,
                    purchasePrice,
                    sellingPrice,
                    mrp,
                    command.UserId,
                    occurredOn);
                await _dbContext.ProductBatchStocks.AddAsync(batchStock, cancellationToken);
            }
        }

        var movement = StockMovement.CreateStockIn(
            scope.FranchiseId,
            scope.MartStoreId,
            product.ProductId,
            movementType,
            referenceType,
            command.ReferenceId,
            command.Quantity,
            previousQuantity,
            newQuantity,
            remarks,
            command.UserId,
            occurredOn);
        await _dbContext.StockMovements.AddAsync(movement, cancellationToken);

        StockAdjustment? adjustment = null;
        if (movementType == "ADJUSTMENT_ADD")
        {
            adjustment = StockAdjustment.CreateAdd(
                scope.FranchiseId,
                scope.MartStoreId,
                product.ProductId,
                command.Quantity,
                adjustmentReason!,
                command.UserId,
                occurredOn);
            await _dbContext.StockAdjustments.AddAsync(adjustment, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StockInResultDto(
            storeStock.StoreStockId,
            batchStock?.ProductBatchStockId,
            movement.StockMovementId,
            adjustment?.StockAdjustmentId,
            product.ProductId,
            scope.FranchiseId,
            scope.MartStoreId,
            command.Quantity,
            previousQuantity,
            newQuantity,
            previousBatchQuantity,
            newBatchQuantity,
            movementType,
            occurredOn);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record SaleStockRequirement(long ProductId, decimal Quantity);

    private sealed record SaleProduct(
        long ProductId,
        bool IsActive,
        bool IsStockManaged,
        bool IsBatchApplicable,
        bool IsExpiryApplicable);

    private sealed record BatchAllocation(ProductBatchStock Batch, decimal Quantity);
}
