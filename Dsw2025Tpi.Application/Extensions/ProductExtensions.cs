using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Domain.Entities;

namespace Dsw2025Tpi.Application.Extensions
{
    internal static class ProductExtensions
    {
        public static ProductModel.ResponseProduct ToResponseProduct(this Product product)
        {
            return new ProductModel.ResponseProduct(
                product.Id,
                product.Sku,
                product.InternalCode,
                product.Name,
                product.Description,
                product.CurrentUnitPrice,
                product.StockQuantity,
                product.IsActive
            );
        }
    }
}
