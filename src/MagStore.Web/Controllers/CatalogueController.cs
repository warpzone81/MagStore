﻿using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MagStore.Entities;
using MagStore.Infrastructure.Interfaces;
using MagStore.Web.Models.Catalogue;
using MagStore.Web.Models.Product;

namespace MagStore.Web.Controllers
{
    public class CatalogueController : Controller
    {
        private readonly IShop shop;

        public CatalogueController(IShop shop)
        {
            this.shop = shop;
        }

        [HttpPost]
        public ActionResult ViewProductsInCatalogue(string id)
        {
            var catalogue = shop.GetCoordinator<Catalogue>().Load(id);

            return View(new ProductsViewModel { Catalogue = catalogue, Products = OrderedProducts(id) });
        }

        private IEnumerable<Product> OrderedProducts(string id)
        {
            var products = shop.GetCoordinator<Product>()
                .List()
                .Where(x => x.Catalogue == id)
                .OrderByDescending(p => p.Code)
                .ThenBy(p => p.Id);

            var enumerator = products.GetEnumerator();

            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        public ActionResult ViewCatalogues()
        {
            var catalogues = shop.GetCoordinator<Catalogue>().List().OrderBy(c => c.Name).ToList();
            return View(new CataloguesViewModel { Catalogues = catalogues });
        }

        [HttpGet]
        public ActionResult CreateCatalogue()
        {
            return View(new CreateCatalogueViewModel());
        }

        [HttpPost]
        public ActionResult CreateCatalogue(CreateCatalogueInputModel inputModel)
        {
            shop.GetCoordinator<Catalogue>().Save(new Catalogue
            {
                Id=inputModel.Id,
                Name=inputModel.Name,
                DiscountAmount =  inputModel.DiscountAmount,
                DiscountType = inputModel.DiscountType,
                Promotions = new List<string>()
            });
            return RedirectToAction("ViewCatalogues", "Catalogue"); // View(new CreateCatalogueViewModel());
        }

        [HttpGet]
        public ActionResult EditCatalogue(EditCatalogueGetInputModel getInputModel)
        {
            var catalogue = shop.GetCoordinator<Catalogue>().Load(getInputModel.Id);

            var viewModel = new EditCatalogueViewModel
            {
                Id = catalogue.Id,
                Name = catalogue.Name,
                DiscountType = catalogue.DiscountType,
                DiscountAmount = catalogue.DiscountAmount,
                Promotions = catalogue.Promotions
            };

            return View(viewModel);
        }

        [HttpPost]
        public ActionResult EditCatalogue(EditCataloguePostInputModel postInputModel)
        {
            shop.GetCoordinator<Catalogue>()
                .Save(UpdateCatalogue(postInputModel));

            return RedirectToAction("EditCatalogue", new {postInputModel.Id });
        }

        private Catalogue UpdateCatalogue(EditCataloguePostInputModel postInputModel)
        {
            var catalogue = shop.GetCoordinator<Catalogue>().Load(postInputModel.Id);

            catalogue.Name = postInputModel.Name;
            catalogue.DiscountAmount = postInputModel.DiscountAmount;
            catalogue.DiscountType = postInputModel.DiscountType;
            catalogue.Promotions = postInputModel.Promotions;
            return catalogue;
        }

        public ActionResult DeleteCatalogue(DeleteCatalogueInputModel inputModel)
        {
            shop.GetCoordinator<Catalogue>().Delete(shop.GetCoordinator<Catalogue>().Load(inputModel.Id));
            return RedirectToAction("ViewCatalogues");
        }
    }
}
