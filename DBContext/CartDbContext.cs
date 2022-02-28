using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Models;
using Microsoft.EntityFrameworkCore;

namespace CartService.DBContext
{
    public class CartDbContext: DbContext
    {
        public CartDbContext(DbContextOptions options): base(options)
        {

        }

        public DbSet<Cart> Carts { get; set; }
    }
}
