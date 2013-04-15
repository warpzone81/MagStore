﻿using System;
using System.Collections.Generic;
using RavenDBMembership.Entities.Enums;

namespace MagStore.Web.Models.Promotion
{
    public class PromotionViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountAmount { get; set; }
        public string Exclusivity { get; set; }
        public IEnumerable<RavenDBMembership.Entities.Promotion> Restrictions { get; set; }
    }
}