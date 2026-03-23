using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReviewsController : Controller
    {
        private readonly IReviewRepository _reviewRepository;
        private const string ErrorKey = "ErrorMessage";
        private const string SuccessKey = "SuccessMessage";

        public ReviewsController(IReviewRepository reviewRepository)
        {
            _reviewRepository = reviewRepository;
        }

        // GET: Admin/Reviews
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            var reviews = await _reviewRepository.GetAllAsync();
            return View(reviews.OrderByDescending(r => r.DateCreated));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var review = await _reviewRepository.GetByIdAsync(id);
            if (review == null)
            {
                TempData[ErrorKey] = "Không tìm thấy bình luận.";
                return RedirectToAction(nameof(Index));
            }
            review.IsApproved = true;
            try
            {
                await _reviewRepository.UpdateAsync(review);
                await _reviewRepository.SaveAsync();
            }
            catch
            {
                TempData[ErrorKey] = "Không thể duyệt bình luận.";
                return RedirectToAction(nameof(Index));
            }
            TempData[SuccessKey] = "Đã duyệt bình luận.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _reviewRepository.DeleteAsync(id);
                await _reviewRepository.SaveAsync();
            }
            catch
            {
                TempData[ErrorKey] = "Không thể xoá bình luận.";
                return RedirectToAction(nameof(Index));
            }
            TempData[SuccessKey] = "Đã xoá bình luận.";
            return RedirectToAction(nameof(Index));
        }
    }
}


