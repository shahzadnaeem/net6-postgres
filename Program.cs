using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;
using NpgsqlTypes;

namespace postgres
{
    public class PostgresContext : DbContext
    {
        public DbSet<Thing> Things { get; set; }
        public DbSet<Owner> Owners { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<StockItem> StockItems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseNpgsql(@"Host=localhost;Username=shahzad;Database=shazdb;Password=LetMeIn007")
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging();
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>()
                .HasKey(t => new { t.OrderId, t.StockItemId });

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.StockItem)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.StockItemId);
        }
    }


    public class Thing
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public List<Owner> Owners { get; set; }
    }

    public class Owner
    {
        public int Id { get; set; }
        public string OwnerName { get; set; }
        public List<Thing> Things { get; set; }
    }


    public class Customer
    {
        public int Id { get; set; }
        public string Email { get; set; }

    }

    public class Order
    {
        public int Id { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public Customer Buyer { get; set; }
        public List<OrderItem> OrderItems { get; set; }
    }

    public class StockItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public float Price { get; set; }
        public List<OrderItem> OrderItems { get; set; }
    }

    public class OrderItem
    {
        public int Quantity { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; }

        public int StockItemId { get; set; }
        public StockItem StockItem { get; set; }
    }

    class Helpers
    {
        public static string dotNetVer()
        {
            return System.Environment.Version.ToString();
        }

        public static string hostInfo()
        {
            return System.Environment.OSVersion.ToString();
        }

        public static void Op(string msg, bool preLine = false)
        {
            if (preLine)
            {
                Console.WriteLine();
                Console.WriteLine();
            }

            Console.WriteLine($"# {msg}");
        }
        public static async Task RecreateDb(PostgresContext ctx)
        {
            Op("PostgreSQL db example ...");
            Op($"Host: {hostInfo()}");
            Op($".NET Version: {dotNetVer()}");

            await ctx.Database.EnsureDeletedAsync();
            await ctx.Database.EnsureCreatedAsync();

            Op("DB recreated", true);
        }

        public async static Task Seed(PostgresContext ctx)
        {
            // Implicit Many-Many

            var thing1 = new Thing
            {
                Name = "Bucket",
                Owners = new List<Owner>()
            };

            var owner1 = new Owner
            {
                OwnerName = "Shahzad",
                Things = new List<Thing> {
                    thing1
                }
            };

            thing1.Owners.Append<Owner>(owner1);

            ctx.Add<Thing>(thing1);
            ctx.Add<Owner>(owner1);

            await ctx.SaveChangesAsync();

            // Explicit Many-Many

            var hammer = new StockItem
            {
                Name = "Thor",
                Price = 1200F
            };

            var hat = new StockItem
            {
                Name = "Topi",
                Price = 100F
            };

            ctx.AddRange(hammer, hat);

            var buyer = new Customer
            {
                Email = "buyer@example.com"
            };

            ctx.Add<Customer>(buyer);

            var order = new Order
            {
                OrderDate = DateTimeOffset.UtcNow,
                Buyer = buyer,
                OrderItems = new List<OrderItem>()
            };

            ctx.Add<Order>(order);

            var orderItem1 = new OrderItem
            {
                Order = order,
                StockItem = hammer,
                Quantity = 1
            };

            var orderItem2 = new OrderItem
            {
                Order = order,
                StockItem = hat,
                Quantity = 2
            };

            ctx.AddRange(orderItem1, orderItem2);

            await ctx.SaveChangesAsync();

            Op("Seeding done");
        }

        public async static Task Output(PostgresContext ctx)
        {
            Op("Things...", true);
            var things = await ctx.Things.Include(t => t.Owners).ToListAsync();

            foreach (var thing in things)
            {
                Op($"Thing #{thing.Id}: Name = {thing.Name}; Owner = {thing.Owners[0].OwnerName}");
            }

            Op("Orders...", true);
            var orders = await ctx.Orders.Include(o => o.OrderItems).Include(o => o.Buyer).ToListAsync();

            foreach (var order in orders)
            {
                Op($"Order #{order.Id}: Buyer = {order.Buyer.Email}; Date = {order.OrderDate}");

                foreach (var item in order.OrderItems)
                {
                    Op($"  Item #{item.StockItemId}: Name = {item.StockItem.Name}; Qty = {item.Quantity}");
                }
            }


            Op("Bye!", true);
        }

    }

    class Program
    {

        static async Task Main(string[] args)
        {
            await using var ctx = new PostgresContext();

            await Helpers.RecreateDb(ctx);

            await Helpers.Seed(ctx);

            await Helpers.Output(ctx);
        }
    }
}
