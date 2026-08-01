using KaijensonIventory_SalesMotorShopWeb.Services;
using KaijensonIventory_SalesMotorShopWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KaijensonIventory_SalesMotorShopWeb.Controllers
{
    public class ProductsController : BaseController
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        public async Task<IActionResult> Index(string? searchString, int? categoryId, int page = 1)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            var result = await _productService.GetPagedAsync(searchString, categoryId, page);

            ViewData["Page"] = page;
            ViewData["TotalPages"] = result.TotalPages;
            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryId"] = categoryId;
            ViewBag.Categories = result.Categories;

            return View(result.Items);
        }

        public async Task<IActionResult> Create()
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            return View(await _productService.PrepareCreateViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (!ModelState.IsValid)
                return View(await _productService.PrepareCreateViewModelAsync(model));

            var result = await _productService.CreateAsync(model, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Key ?? "", error.Message);
                return View(await _productService.PrepareCreateViewModelAsync(model));
            }

            TempData["SuccessMessage"] = "Product created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            var model = await _productService.PrepareEditViewModelAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductEditViewModel model)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            if (id != model.ProductId) return NotFound();

            if (!ModelState.IsValid)
                return View(await _productService.PrepareEditViewModelAsync(model));

            var result = await _productService.UpdateAsync(model, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(error.Key ?? "", error.Message);
                return View(await _productService.PrepareEditViewModelAsync(model));
            }

            TempData["SuccessMessage"] = "Product updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            var product = await _productService.GetByIdAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var redirect = RedirectIfNotAuthenticated();
            if (redirect != null)
                return redirect;

            var result = await _productService.DeleteAsync(id, GetCurrentStaffId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message
                    ?? "An error occurred while deleting the product. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Product deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
