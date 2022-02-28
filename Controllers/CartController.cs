using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.DBContext;
using CartService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Threading;

namespace CartService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private CartDbContext _context;
        private readonly ILogger<CartController> _logger;
        public CartController(CartDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<string> Post([FromBody]Cart bodyData)
        {
            Cart cartObj = new Cart();
            cartObj.cartId = bodyData.cartId;
            cartObj.orderId = bodyData.orderId;
            cartObj.orderStatus = bodyData.orderStatus;
            cartObj.total = bodyData.total;

            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable("RABBITMQ_PORT"))
            };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "orders",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cartObj));

                channel.BasicPublish(exchange: "",
                                     routingKey: "orders",
                                     basicProperties: null,
                                     body: body);

                Console.WriteLine(" [x] Sent {0}", JsonConvert.SerializeObject(cartObj));
            }
            return JsonConvert.SerializeObject(cartObj);
        }

        // GET to consume 'orders' queue
        [HttpGet]
        public async Task<ActionResult<List<string>>> Get()
        {
            List<string> queueItems = new List<string>();
            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable("RABBITMQ_PORT"))
            };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                Cart record = new Cart();
                channel.QueueDeclare(queue: "orders",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var consumer = new EventingBasicConsumer(channel);
                // Consume from 'orders' queue
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    queueItems.Add(message);

                    // Store into In-Memeory DB
                    record = JsonConvert.DeserializeObject<Cart>(message);
                    Console.WriteLine(" [x] Received {0}", message);
                };
                Thread.Sleep(2000);
                if (record.orderId != null)
                {
                    _context.Add(record);
                    await _context.SaveChangesAsync();
                }

                channel.BasicConsume(queue: "orders",
                                     autoAck: true,
                                     consumer: consumer);
                
            }

            if (queueItems.Count == 0)
            {
                return NoContent();
            }

            return queueItems;
        }
    }
}
