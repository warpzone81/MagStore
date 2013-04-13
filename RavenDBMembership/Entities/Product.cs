﻿using System;
using System.Collections.Generic;
using RavenDBMembership.Entities.Enums;
using RavenDBMembership.Infrastructure.Interfaces;

namespace RavenDBMembership.Entities
{
    public class Product : IRavenEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Colour { get; set; }
        public string Size { get; set; }
        public Gender Gender { get; set; }
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
    }
}