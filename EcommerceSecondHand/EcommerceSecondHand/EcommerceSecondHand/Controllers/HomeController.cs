using Microsoft.AspNetCore.Mvc;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using System.Diagnostics;

namespace EcommerceSecondHand.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;

        public HomeController(ILogger<HomeController> logger, ICategoryRepository categoryRepository, IProductRepository productRepository)
        {
            _logger = logger;
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 8;
            const int days = 10;

            var categories = await _categoryRepository.GetAllAsync();
            var featured = await _productRepository.GetRecentProductsWithinDaysPagedAsync(days, page, pageSize);
            var totalFeatured = await _productRepository.CountRecentProductsWithinDaysAsync(days);

            ViewBag.FeaturedProducts = featured;
            ViewBag.FeaturedPage = page;
            ViewBag.FeaturedPageSize = pageSize;
            ViewBag.FeaturedTotal = totalFeatured;
            return View(categories);
        }

        

        [Route("/Error")]
        public IActionResult Error()
        {
            return View();
        }
    }
}