using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using OzonEdu.StockApi.Domain.AggregationModels.StockItemAggregate;
using OzonEdu.StockApi.Domain.AggregationModels.ValueObjects;
using OzonEdu.StockApi.Infrastructure.Repositories.Infrastructure.Interfaces;

namespace OzonEdu.StockApi.Infrastructure.Repositories.Implementation
{
    public class StockItemRepository : IStockItemRepository
    {
        private readonly IDbConnectionFactory<NpgsqlConnection> _dbConnectionFactory;
        private readonly IQueryExecutor _queryExecutor;
        private const int Timeout = 5;

        public StockItemRepository(IDbConnectionFactory<NpgsqlConnection> dbConnectionFactory, IQueryExecutor queryExecutor)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _queryExecutor = queryExecutor;
        }

        public async Task<StockItem> CreateAsync(StockItem itemToCreate, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO skus (id, name, item_type_id, clothing_size)
                VALUES (@SkuId, @Name, @ItemTypeId, @ClothingSize);
                INSERT INTO stocks (sku_id, quantity, minimal_quantity)
                VALUES (@SkuId, @Quantity, @MinimalQuantity);";

            var parameters = new
            {
                SkuId = itemToCreate.Sku.Value,
                Name = itemToCreate.Name.Value,
                ItemTypeId = itemToCreate.ItemType.Type.Id,
                ClothingSize = itemToCreate.ClothingSize?.Id,
                Quantity = itemToCreate.Quantity.Value,
                MinimalQuantity = itemToCreate.MinimalQuantity.Value
            };
            var commandDefinition = new CommandDefinition(
                sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            await connection.ExecuteAsync(commandDefinition);
            return await _queryExecutor.Execute(itemToCreate, () => connection.ExecuteAsync(commandDefinition));
        }

        public async Task<StockItem> UpdateAsync(StockItem itemToUpdate, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE skus
                SET name = @Name, item_type_id = @ItemTypeId, clothing_size = @ClothingSize
                WHERE id = @SkuId;
                UPDATE stocks
                SET quantity = @Quantity, minimal_quantity = @MinimalQuantity
                WHERE sku_id = @SkuId;";

            var parameters = new
            {
                SkuId = itemToUpdate.Sku.Value,
                Name = itemToUpdate.Name.Value,
                ItemTypeId = itemToUpdate.ItemType.Type.Id,
                ClothingSize = itemToUpdate?.ClothingSize?.Id,
                Quantity = itemToUpdate.Quantity.Value,
                MinimalQuantity = itemToUpdate.MinimalQuantity.Value
            };
            var commandDefinition = new CommandDefinition(
                sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            return await _queryExecutor.Execute(itemToUpdate, () => connection.ExecuteAsync(commandDefinition));
        }

        public async Task<StockItem> FindBySkuAsync(Sku sku, CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT skus.id, skus.name, skus.item_type_id, skus.clothing_size,
                       stocks.id, stocks.sku_id, stocks.quantity, stocks.minimal_quantity,
                       item_types.id, item_types.name,
                       clothing_sizes.id, clothing_sizes.name
                FROM skus
                INNER JOIN stocks on stocks.sku_id = skus.id
                INNER JOIN item_types on item_types.id = skus.item_type_id
                LEFT JOIN clothing_sizes on clothing_sizes.id = skus.clothing_size
                WHERE skus.id = @SkuId;";
            
            var parameters = new
            {
                SkuId = sku.Value,
            };
            var commandDefinition = new CommandDefinition(
                sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            return await _queryExecutor.Execute(
                async () =>
                {
                    var stockItems = await connection.QueryAsync<
                        Models.Sku, Models.StockItem, Models.ItemType, Models.ClothingSize, StockItem>(
                        commandDefinition,
                        (skuModel, stock, itemType, clothingSize) => StockItem.CreateStockItem(stock.Id,
                            skuModel.Id,
                            skuModel.Name,
                            new ItemType(itemType.Id, itemType.Name),
                            clothingSize?.Id is not null ? new ClothingSize(clothingSize.Id.Value, clothingSize.Name) : null,
                            stock.Quantity,
                            stock.MinimalQuantity));
                    return stockItems.First();
                });
        }

        public async Task<IReadOnlyList<StockItem>> FindBySkusAsync(IReadOnlyList<Sku> skus, CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT skus.id, skus.name, skus.item_type_id, skus.clothing_size,
                       stocks.id, stocks.sku_id, stocks.quantity, stocks.minimal_quantity,
                       item_types.id, item_types.name,
                       clothing_sizes.id, clothing_sizes.name
                FROM skus
                INNER JOIN stocks on stocks.sku_id = skus.id
                INNER JOIN item_types on item_types.id = skus.item_type_id
                LEFT JOIN clothing_sizes on clothing_sizes.id = skus.clothing_size
                WHERE skus.id = ANY(@SkuIds);";

            var parameters = new
            {
                SkuIds = skus.Select(x => x.Value).ToArray(),
            };
            var commandDefinition = new CommandDefinition(
                sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            var result = await _queryExecutor.Execute(
                () =>
                    connection.QueryAsync<
                        Models.Sku, Models.StockItem, Models.ItemType, Models.ClothingSize, StockItem>(
                        commandDefinition,
                        (skuModel, stock, itemType, clothingSize) =>
                            StockItem.CreateStockItem(stock.Id,
                            skuModel.Id,
                            skuModel.Name,
                            new ItemType(itemType.Id, itemType.Name),
                            clothingSize?.Id is not null
                                ? new ClothingSize(clothingSize.Id.Value, clothingSize.Name)
                                : null,
                            stock.Quantity,
                            stock.MinimalQuantity)));
            return result.ToList();
        }

        public async Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT skus.id, skus.name, skus.item_type_id, skus.clothing_size,
                       stocks.id, stocks.sku_id, stocks.quantity, stocks.minimal_quantity,
                       item_types.id, item_types.name,
                       clothing_sizes.id, clothing_sizes.name
                FROM skus
                INNER JOIN stocks on stocks.sku_id = skus.id
                INNER JOIN item_types on item_types.id = skus.item_type_id
                LEFT JOIN clothing_sizes on clothing_sizes.id = skus.clothing_size;";
            
            var commandDefinition = new CommandDefinition(
                sql,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            var result = await _queryExecutor.Execute(
                () => connection.QueryAsync<
                    Models.Sku, Models.StockItem, Models.ItemType, Models.ClothingSize, StockItem>(commandDefinition,
                    (sku, stock, itemType, clothingSize) => StockItem.CreateStockItem(stock.Id,
                        sku.Id,
                        sku.Name,
                        new ItemType(itemType.Id, itemType.Name),
                        clothingSize?.Id is not null ? new ClothingSize(clothingSize.Id.Value, clothingSize.Name) : null,
                        stock.Quantity,
                        stock.MinimalQuantity)));
            return result.ToList();
        }

        public async Task<IReadOnlyList<StockItem>> FindByItemTypeAsync(long itemTypeId,
            CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT skus.id, skus.name, skus.item_type_id, skus.clothing_size,
                       stocks.id, stocks.sku_id, stocks.quantity, stocks.minimal_quantity,
                       item_types.id, item_types.name,
                       clothing_sizes.id, clothing_sizes.name
                FROM skus
                INNER JOIN stocks on stocks.sku_id = skus.id
                INNER JOIN item_types on item_types.id = skus.item_type_id
                LEFT JOIN clothing_sizes on clothing_sizes.id = skus.clothing_size
                WHERE item_types.id = @ItemTypeId;";
            
            var parameters = new
            {
                ItemTypeId = itemTypeId,
            };
            var commandDefinition = new CommandDefinition(
                sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);
            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            var stockItems = await _queryExecutor.Execute( () => connection.QueryAsync<
                Models.Sku, Models.StockItem, Models.ItemType, Models.ClothingSize, StockItem>(commandDefinition,
                (skuModel, stock, itemType, clothingSize) => StockItem.CreateStockItem(stock.Id,
                    skuModel.Id,
                    skuModel.Name,
                    new ItemType(itemType.Id, itemType.Name),
                    clothingSize?.Id is not null ? new ClothingSize(clothingSize.Id.Value, clothingSize.Name) : null,
                    stock.Quantity,
                    stock.MinimalQuantity)));
            
            return stockItems.ToList();
        }
    }
}