﻿using System;
using System.Collections.Generic;
using System.Linq;
using MagStore.Entities;
using MagStore.Entities.Enums;

namespace MagStore.Web.Models.Product
{
    public class EditProductViewModel
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Specification { get; set; }
        public string Colour { get; set; }
        public string Size { get; set; }
        public string Gender { get; set; }
        public string Brand { get; set; }
        public string Supplier { get; set; }
        public int Rating { get; set; }
        public IEnumerable<string> Reviews { get; set; }
        public IEnumerable<ProductImage> Images { get; set; }
        public decimal Price { get; set; }
        public ProductType ProductType { get; set; }
        public int[] AgeRange { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountAmount { get; set; }
        public IEnumerable<string> Promotions { get; set; }
        public string Catalogue { get; set; }

        public IList<Entities.Catalogue> CatalogueList { get; set; }
        public IEnumerable<string> ImageTypes { get { return new[] { "" }.Union(Enum.GetNames(typeof(ImageType))); } }
        public string Tags { get; set; }
    }
}