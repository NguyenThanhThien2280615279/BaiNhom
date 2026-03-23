using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ICategoryRepository _categoryRepository;
        private const string ErrorKey = "ErrorMessage";
        private const string SuccessKey = "SuccessMessage";

        public CategoriesController(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        // GET: Admin/Categories
        [HttpGet]
        public async Task<IActionResult> Index(string? search = null, int page = 1, int pageSize = 10)
        {
            var categories = await _categoryRepository.GetCategoriesWithProductCountAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                categories = categories.Where(c =>
                    (!string.IsNullOrEmpty(c.Name) && c.Name.ToLowerInvariant().Contains(term)) ||
                    (!string.IsNullOrEmpty(c.Description) && c.Description.ToLowerInvariant().Contains(term))
                );
            }

            var list = categories.ToList();
            var totalCount = list.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            return View(paged);
        }

        // GET: Admin/Categories/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Category());
        }

        // POST: Admin/Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            if (!ModelState.IsValid)
            {
                TempData[ErrorKey] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                return View(model);
            }
            await _categoryRepository.AddAsync(model);
            await _categoryRepository.SaveAsync();
            TempData[SuccessKey] = "Đã tạo danh mục.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Categories/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.Id)
            {
                TempData[ErrorKey] = "Yêu cầu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData[ErrorKey] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.";
                return View(model);
            }

            _categoryRepository.Update(model);
            await _categoryRepository.SaveAsync();
            TempData[SuccessKey] = "Đã cập nhật danh mục.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Categories/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _categoryRepository.RemoveAsync(id);
                await _categoryRepository.SaveAsync();
                TempData[SuccessKey] = "Đã xoá danh mục.";
            }
            catch
            {
                TempData[ErrorKey] = "Không thể xoá danh mục. Có thể đang được sử dụng.";
            }
            return RedirectToAction(nameof(Index));
        }

        // helpers
        // Image upload helper removed as image support is dropped
    }
}


