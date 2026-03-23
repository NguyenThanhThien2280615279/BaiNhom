using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private const string ErrorKey = "ErrorMessage";
        private const string SuccessKey = "SuccessMessage";

        public ProductsController(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // GET: Admin/Products
        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId, string? searchQuery, string? sortOrder, decimal? minPrice = null, decimal? maxPrice = null, ProductCondition? condition = null, int page = 1)
        {
            const int pageSize = 20;

            var products = await _productRepository.GetFilteredProductsAsync(categoryId, searchQuery, sortOrder, minPrice, maxPrice, condition, page, pageSize);
            var totalCount = await _productRepository.GetFilteredProductsCountAsync(categoryId, searchQuery, minPrice, maxPrice, condition);
            var categories = await _categoryRepository.GetAllAsync();

            ViewBag.Categories = categories;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.SearchQuery = searchQuery;
            ViewBag.SortOrder = sortOrder;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Condition = condition;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Sold(int page = 1)
        {
            const int pageSize = 20;
            var products = await _productRepository.GetSoldProductsAsync(page, pageSize);
            var total = await _productRepository.CountSoldProductsAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            return View(products);
        }

        // GET: Admin/Products/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetProductWithDetailsAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // POST: Admin/Products/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                TempData[ErrorKey] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            product.IsActive = !product.IsActive;
            product.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _productRepository.UpdateAsync(product);
                await _productRepository.SaveAsync();
            }
            catch
            {
                TempData[ErrorKey] = "Không thể cập nhật sản phẩm.";
                return RedirectToAction(nameof(Details), new { id });
            }
            
            TempData[SuccessKey] = product.IsActive ? "Đã kích hoạt sản phẩm." : "Đã vô hiệu hoá sản phẩm.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}


