using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CartService.Models
{
    public class Cart
    {
        public string cartId { get; set; }
        public string[] productIds { get; set; }
        public double[] productPrices { get; set; }
        public int total { get; set; }
        public string orderStatus { get; set; }
        public string orderId { get; set; }
    }
}
