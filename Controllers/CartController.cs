using AhmedStore.Migrations;
using AhmedStore.Models;
using AhmedStore.Repository;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AhmedStore.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartRepository cartRepository;
        private readonly ISearchRepository searchRepository;
        private readonly Microsoft.AspNetCore.Identity.UserManager<User> userManager;

        public CartController(ICartRepository cartRepository,ISearchRepository searchRepository
            ,UserManager<User> userManager)
        {
            this.cartRepository = cartRepository;
            this.searchRepository = searchRepository;
            this.userManager = userManager;
        }
        [Authorize]
        public async Task<IActionResult> AddProductToCart(string UserName, int ProductId)
        {
            if (ProductId == 0)
            {
                return BadRequest("Invalid Product ID");
            }

            User user = await userManager.FindByNameAsync(UserName);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var shop = cartRepository.GetShopByProductId(ProductId);
            if (shop == null)
            {
                return NotFound("Shop not found for the provided product");
            }

            ViewBag.ShopName = shop.Name;
            ViewBag.Categories = searchRepository.GetCategoriesOfShop(shop.Id);
            var products = searchRepository.DisplayProductsForUsers(shop.Id);

            var cart = cartRepository.GetCartByUserId(user.Id);
            if (cart == null)
            {
                // If the cart doesn't exist, create a new one
                cartRepository.CreateCart(user.Id, shop.Id);
                cart = cartRepository.GetCartByUserId(user.Id);
            }

            // Check if there are existing products in the cart
            if (cart.CartProducts != null && cart.CartProducts.Any())
            {
                var shopMatches = cartRepository.CheckShopUnity(shop.Id, cart.Id);
                if (!shopMatches)
                {
                    // Ask the user if they want to proceed and delete existing cart items from a different shop
                    TempData["ShopConflict"] = "You have items from another shop in your cart. Do you want to delete them and proceed?";
                    // You might want to store shop and product IDs in TempData to use later
                    TempData["NewShopId"] = shop.Id;
                    TempData["ProductId"] = ProductId;
                    TempData["OldCartId"] = cart.Id;

                    return View("CartShopConflict", new { ShopName = shop.Name, Products = products });
                }
            }

            // Check if the product already exists in the cart before adding
            var productExists = cartRepository.CartProductExistBefore(ProductId, cart.Id);
            if (!productExists)
            {
                cartRepository.AddProductToCart(cart.Id, ProductId);
            }

            return RedirectToAction("DisplayProductsForUsers", "Search", new { ShopName = shop.Name, id = shop.Id });
        }

        /*------------------------------- Carts ------------------------------*/
        [Authorize]
        public IActionResult Carts()
        {
            var UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Result = cartRepository.GetCart(UserId);
            return View(Result);
        }
        public IActionResult CartDetails(int CartId)
        {
            var UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.Cart = cartRepository.GetCart(UserId);
            var Cart = cartRepository.GetCartProducts(CartId);
            return View(Cart);
        }
        public IActionResult DeleteProductFromCart(int CartId,int ProductId)
        {
            cartRepository.DeleteProductFromCart(CartId, ProductId);
            return RedirectToAction("CartDetails", new {CartId});
        }
        public IActionResult DeleteCart(int CartId)
        {
            cartRepository.DeleteCart(CartId);
            return RedirectToAction("Index","Home");
        }
        [HttpPost]
        public IActionResult ConfirmCartDeletion(int NewShopId,int ProductId,int OldCartId)
        {
            cartRepository.DeleteCart(OldCartId);
            string UserName = User.Identity.Name;
            return RedirectToAction("AddProductToCart", new { UserName, ProductId });
        }
        [HttpPost]
        public IActionResult Cancel(int ShopId)
        {
            var shop = cartRepository.GetShopById(ShopId);

            return RedirectToAction("DisplayProductsForUsers", "Search", new { ShopName = shop.Name, id = shop.Id });
        }
 

    }
}
